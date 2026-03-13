using TelegramOpenCodeBridge.Cli;
using TelegramOpenCodeBridge.Configuration;
using TelegramOpenCodeBridge.ConsoleOutput;
using TelegramOpenCodeBridge.OpenCode;
using TelegramOpenCodeBridge.Telegram;

namespace TelegramOpenCodeBridge.Application;

public sealed class BridgeApplication
{
    private readonly ConsoleTranscriptWriter _console = new();

    public async Task<int> RunAsync(string[] args)
    {
        ParsedCommand parsedCommand;

        try
        {
            parsedCommand = CommandLineParser.Parse(args);
        }
        catch (CommandLineException exception)
        {
            _console.WriteError(exception.Message);
            System.Console.WriteLine();
            System.Console.WriteLine(CommandLineParser.GetHelpText());
            return 1;
        }

        if (parsedCommand.Command == BridgeCommand.Help)
        {
            System.Console.WriteLine(CommandLineParser.GetHelpText());
            return 0;
        }

        LoadedBridgeConfiguration loadedConfiguration;

        try
        {
            loadedConfiguration = new AppConfigurationLoader().Load(parsedCommand.ConfigPath);
        }
        catch (ConfigurationException exception)
        {
            _console.WriteError(exception.Message);
            return 1;
        }

        ConfigurationValidationResult validation = new AppConfigurationValidator().Validate(loadedConfiguration.Options);

        if (parsedCommand.Command == BridgeCommand.ConfigShow)
        {
            System.Console.WriteLine(ConfigMasker.ToSanitizedJson(loadedConfiguration.Options));

            if (!validation.IsValid)
            {
                System.Console.WriteLine();
                WriteValidationErrors(validation);
                return 1;
            }

            return 0;
        }

        if (parsedCommand.Command == BridgeCommand.Check)
        {
            return await RunCheckAsync(loadedConfiguration.Options, validation, CancellationToken.None);
        }

        if (!validation.IsValid)
        {
            WriteValidationErrors(validation);
            return 1;
        }

        return await RunStartAsync(loadedConfiguration.Options, CancellationToken.None);
    }

    private async Task<int> RunStartAsync(BridgeOptions options, CancellationToken cancellationToken)
    {
        using TelegramBotClient telegramClient = new(options.Telegram);
        using OpenCodeClient openCodeClient = new(options.OpenCode);

        if (options.Runtime.StartupSessionValidation)
        {
            int checkExitCode = await RunCheckAsync(options, new ConfigurationValidationResult(), cancellationToken);
            if (checkExitCode != 0)
            {
                return checkExitCode;
            }
        }

        BridgeRunner runner = new(options, telegramClient, openCodeClient, _console);
        return await runner.RunAsync(cancellationToken);
    }

    private async Task<int> RunCheckAsync(
        BridgeOptions options,
        ConfigurationValidationResult validation,
        CancellationToken cancellationToken)
    {
        if (!validation.IsValid)
        {
            WriteValidationErrors(validation);
            return 1;
        }

        bool failed = false;

        using TelegramBotClient telegramClient = new(options.Telegram);
        using OpenCodeClient openCodeClient = new(options.OpenCode);

        WriteSecurityWarnings(options);

        try
        {
            TelegramBotIdentity identity = await telegramClient.GetMeAsync(cancellationToken);
            _console.WriteInfo($"Telegram: OK ({FormatTelegramIdentity(identity)})");
        }
        catch (Exception exception)
        {
            failed = true;
            _console.WriteError($"Telegram-Check fehlgeschlagen: {exception.Message}");
        }

        try
        {
            OpenCodeHealth health = await openCodeClient.GetHealthAsync(cancellationToken);
            _console.WriteInfo($"OpenCode: OK (Version {health.Version})");
        }
        catch (Exception exception)
        {
            failed = true;
            _console.WriteError($"OpenCode-Healthcheck fehlgeschlagen: {exception.Message}");
        }

        foreach (string sessionId in options.Chats.Select(binding => binding.OpenCodeSessionId).Distinct(StringComparer.Ordinal))
        {
            try
            {
                bool exists = await openCodeClient.SessionExistsAsync(sessionId, cancellationToken);
                if (!exists)
                {
                    failed = true;
                    _console.WriteError($"OpenCode-Session nicht gefunden: {sessionId}");
                    continue;
                }

                _console.WriteInfo($"OpenCode-Session gefunden: {sessionId}");
            }
            catch (Exception exception)
            {
                failed = true;
                _console.WriteError($"Session-Check für {sessionId} fehlgeschlagen: {exception.Message}");
            }
        }

        return failed ? 1 : 0;
    }

    private static string FormatTelegramIdentity(TelegramBotIdentity identity)
    {
        if (!string.IsNullOrWhiteSpace(identity.Username))
        {
            return $"@{identity.Username}";
        }

        return identity.FirstName;
    }

    private void WriteValidationErrors(ConfigurationValidationResult validation)
    {
        _console.WriteError("Konfiguration ungültig:");

        foreach (string error in validation.Errors)
        {
            _console.WriteInfo($"- {error}");
        }
    }

    private void WriteSecurityWarnings(BridgeOptions options)
    {
        if (!Uri.TryCreate(options.OpenCode.BaseUrl, UriKind.Absolute, out Uri? openCodeUri))
        {
            return;
        }

        bool isLoopback = openCodeUri.IsLoopback || string.Equals(openCodeUri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
        bool insecureRemoteHttp = string.Equals(openCodeUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && !isLoopback;

        if (insecureRemoteHttp && string.IsNullOrWhiteSpace(options.OpenCode.Password))
        {
            _console.WriteWarning("Warnung: OpenCode wird unverschlüsselt über HTTP ohne Passwort auf einem entfernten Host angesprochen.");
        }
    }

    private sealed class BridgeRunner
    {
        private readonly BridgeOptions _options;
        private readonly TelegramBotClient _telegramClient;
        private readonly OpenCodeClient _openCodeClient;
        private readonly ConsoleTranscriptWriter _console;
        private readonly ChatRequestQueue _chatRequestQueue;
        private readonly CancellationTokenSource _pollingCancellationTokenSource = new();
        private readonly OpenCodeAvailabilityTracker _openCodeAvailabilityTracker;
        private int _stopRequested;
        private Task? _shutdownTask;

        public BridgeRunner(
            BridgeOptions options,
            TelegramBotClient telegramClient,
            OpenCodeClient openCodeClient,
            ConsoleTranscriptWriter console)
        {
            _options = options;
            _telegramClient = telegramClient;
            _openCodeClient = openCodeClient;
            _console = console;
            _openCodeAvailabilityTracker = new OpenCodeAvailabilityTracker(SendBridgeStatusNotificationAsync, _console.WriteInfo, _console.WriteWarning);
            _chatRequestQueue = new ChatRequestQueue(
                options.Runtime.QueueCapacityPerChat,
                ProcessChatRequestAsync,
                RejectChatRequestAsync,
                AbortActiveRequestAsync,
                options.Runtime.RejectMessagesWhenStopping);
        }

        public async Task<int> RunAsync(CancellationToken cancellationToken)
        {
            _console.WriteInfo("Bridge gestartet. Mit CTRL+C beenden.");

            long offset = 0;
            Task healthMonitorTask = MonitorOpenCodeAvailabilityAsync(_pollingCancellationTokenSource.Token);

            if (_options.Runtime.SkipPendingUpdatesOnStart)
            {
                offset = await ResolveStartupOffsetAsync(cancellationToken);
            }

            string startupNotification = await DetermineStartupNotificationAsync(cancellationToken);
            await SendBridgeStatusNotificationAsync(startupNotification, CancellationToken.None);

            ConsoleCancelEventHandler handler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                _shutdownTask ??= RequestStopAsync();
            };

            System.Console.CancelKeyPress += handler;

            try
            {
                while (!IsStopping)
                {
                    IReadOnlyList<TelegramUpdate> updates;

                    try
                    {
                        updates = await _telegramClient.GetUpdatesAsync(
                            offset,
                            _options.Telegram.PollingTimeoutSeconds,
                            _pollingCancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException) when (IsStopping || _pollingCancellationTokenSource.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        _console.WriteError($"Telegram-Polling fehlgeschlagen: {exception.Message}");
                        await DelayBeforeRetryAsync();
                        continue;
                    }

                    foreach (TelegramUpdate update in updates.OrderBy(item => item.UpdateId))
                    {
                        offset = Math.Max(offset, update.UpdateId + 1L);

                        if (update.Message is null || string.IsNullOrWhiteSpace(update.Message.Text))
                        {
                            continue;
                        }

                        try
                        {
                            await HandleIncomingMessageAsync(update.Message);
                        }
                        catch (Exception exception)
                        {
                            _console.WriteError($"Telegram-Update {update.UpdateId} konnte nicht verarbeitet werden: {exception.Message}");
                        }
                    }
                }
            }
            finally
            {
                System.Console.CancelKeyPress -= handler;
                await RequestStopAsync();
                try
                {
                    await healthMonitorTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            return 0;
        }

        private bool IsStopping => Volatile.Read(ref _stopRequested) == 1;

        private async Task<long> ResolveStartupOffsetAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<TelegramUpdate> pendingUpdates = await _telegramClient.GetUpdatesAsync(0, 0, cancellationToken);
            if (pendingUpdates.Count == 0)
            {
                return 0;
            }

            long nextOffset = pendingUpdates.Max(update => update.UpdateId) + 1L;
            _console.WriteInfo($"{pendingUpdates.Count} ausstehende Telegram-Updates werden beim Start übersprungen.");
            return nextOffset;
        }

        private async Task HandleIncomingMessageAsync(TelegramMessage message)
        {
            if (IsStopping)
            {
                await RejectStoppingMessageAsync(message.Chat.Id);
                return;
            }

            if (!string.Equals(message.Chat.Type, "private", StringComparison.OrdinalIgnoreCase))
            {
                _console.WriteInfo($"Chat {message.Chat.Id} wird ignoriert, da nur private Chats unterstützt werden.");
                return;
            }

            ChatBindingOptions? chatBinding = _options.Chats.FirstOrDefault(item => item.TelegramChatId == message.Chat.Id);
            if (chatBinding is null)
            {
                _console.WriteInfo($"Chat {message.Chat.Id} ist nicht freigegeben und wird ignoriert.");
                return;
            }

            if (!IsUserAllowed(message, chatBinding))
            {
                _console.WriteInfo($"Benutzer {message.From?.Id} ist für Chat {message.Chat.Id} nicht freigegeben.");
                await _telegramClient.SendMessageAsync(message.Chat.Id, "Du bist für diese Bridge nicht freigeschaltet.", CancellationToken.None);
                return;
            }

            ChatRequest request = new(
                message.Chat.Id,
                message.From?.Id,
                message.From?.Username,
                message.Text!,
                chatBinding.OpenCodeSessionId);

            await _chatRequestQueue.EnqueueAsync(request, CancellationToken.None);
        }

        private bool IsUserAllowed(TelegramMessage message, ChatBindingOptions binding)
        {
            if (message.From?.Id is null)
            {
                return !_options.AccessControl.AllowedUserIds.Any() && !binding.AllowedUserIds.Any();
            }

            long userId = message.From.Id.Value;

            bool globallyAllowed = !_options.AccessControl.AllowedUserIds.Any() || _options.AccessControl.AllowedUserIds.Contains(userId);
            bool locallyAllowed = !binding.AllowedUserIds.Any() || binding.AllowedUserIds.Contains(userId);
            return globallyAllowed && locallyAllowed;
        }

        private async Task ProcessChatRequestAsync(ChatRequest request, CancellationToken cancellationToken)
        {
            _console.WriteTranscript(
                "USER",
                request.Text,
                $"Telegram-Nachricht in Chat {request.ChatId} empfangen.");
            _console.WriteTranscript(
                "BRIDGE",
                $"Anfrage an OpenCode gesendet: \"{request.Text}\"",
                $"OpenCode-Anfrage für Chat {request.ChatId} an Session {request.OpenCodeSessionId} gesendet.");

            try
            {
                string response = await _openCodeClient.SendPromptAsync(request.OpenCodeSessionId, request.Text, cancellationToken);
                await _openCodeAvailabilityTracker.ReportAvailableAsync(CancellationToken.None);

                if (string.IsNullOrWhiteSpace(response))
                {
                    response = "OpenCode hat keine Textantwort geliefert.";
                }

                _console.WriteTranscript(
                    "AGENT",
                    response,
                    $"OpenCode-Antwort für Chat {request.ChatId} empfangen.");
                await _telegramClient.SendMessageAsync(request.ChatId, response, CancellationToken.None);
                _console.WriteInfo($"Telegram-Antwort an Chat {request.ChatId} gesendet.");
            }
            catch (Exception exception)
            {
                string userMessage = IsStopping
                    ? "Die Bridge wird beendet. Der laufende Auftrag wurde abgebrochen."
                    : "Die Anfrage an OpenCode ist fehlgeschlagen. Bitte versuche es erneut.";

                if (IsPotentialOpenCodeOutage(exception, cancellationToken))
                {
                    await _openCodeAvailabilityTracker.ReportUnavailableAsync(exception.Message, CancellationToken.None);
                }

                _console.WriteError($"Fehler bei Chat {request.ChatId}: {exception.Message}");

                if (!IsStopping || _options.Runtime.RejectMessagesWhenStopping)
                {
                    await _telegramClient.SendMessageAsync(request.ChatId, userMessage, CancellationToken.None);
                    _console.WriteInfo($"Fehlermeldung an Chat {request.ChatId} gesendet.");
                }
            }
        }

        private async Task RejectChatRequestAsync(ChatRequest request, string reason, CancellationToken cancellationToken)
        {
            _console.WriteInfo($"Chat {request.ChatId}: {reason}");
            await _telegramClient.SendMessageAsync(request.ChatId, reason, cancellationToken);
            _console.WriteInfo($"Systemmeldung an Chat {request.ChatId} gesendet.");
        }

        private async Task AbortActiveRequestAsync(ChatRequest request, CancellationToken cancellationToken)
        {
            _console.WriteInfo($"Sende Stop-Signal an OpenCode-Session {request.OpenCodeSessionId}.");
            await _openCodeClient.AbortSessionAsync(request.OpenCodeSessionId, cancellationToken);
        }

        private async Task RejectStoppingMessageAsync(long chatId)
        {
            if (_options.Runtime.RejectMessagesWhenStopping)
            {
                await _telegramClient.SendMessageAsync(
                    chatId,
                    "Die Bridge wird gerade beendet. Neue Nachrichten werden aktuell nicht mehr angenommen.",
                    CancellationToken.None);
                _console.WriteInfo($"Stopp-Hinweis an Chat {chatId} gesendet.");
            }
        }

        private async Task MonitorOpenCodeAvailabilityAsync(CancellationToken cancellationToken)
        {
            using PeriodicTimer timer = new(TimeSpan.FromSeconds(_options.Runtime.OpenCodeHealthCheckIntervalSeconds));

            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    try
                    {
                        await _openCodeClient.GetHealthAsync(cancellationToken);
                        await _openCodeAvailabilityTracker.ReportAvailableAsync(CancellationToken.None);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        await _openCodeAvailabilityTracker.ReportUnavailableAsync(exception.Message, CancellationToken.None);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task<string> DetermineStartupNotificationAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _openCodeClient.GetHealthAsync(cancellationToken);
                _openCodeAvailabilityTracker.SetAvailability(true);
                return "Die Bridge wurde gestartet und ist betriebsbereit.";
            }
            catch (Exception exception)
            {
                _openCodeAvailabilityTracker.SetAvailability(false);
                _console.WriteWarning($"OpenCode-Server ist beim Start nicht erreichbar: {exception.Message}");
                return "Die Bridge wurde gestartet, aber der OpenCode-Server ist aktuell nicht erreichbar.";
            }
        }

        private async Task SendBridgeStatusNotificationAsync(string message, CancellationToken cancellationToken)
        {
            foreach (long chatId in _options.Chats.Select(chat => chat.TelegramChatId).Distinct())
            {
                try
                {
                    await _telegramClient.SendMessageAsync(chatId, message, cancellationToken);
                    _console.WriteInfo($"Bridge-Statusmeldung an Chat {chatId} gesendet.");
                }
                catch (Exception exception)
                {
                    _console.WriteWarning($"Bridge-Statusmeldung an Chat {chatId} konnte nicht gesendet werden: {exception.Message}");
                }
            }
        }

        private static bool IsPotentialOpenCodeOutage(Exception exception, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            return exception is HttpRequestException
                or TimeoutException
                or TaskCanceledException
                || exception is OpenCodeException openCodeException
                && (openCodeException.Message.StartsWith("HTTP 5", StringComparison.Ordinal)
                    || openCodeException.Message.StartsWith("HTTP 408", StringComparison.Ordinal));
        }

        private async Task DelayBeforeRetryAsync()
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.Runtime.TelegramRetryDelaySeconds), _pollingCancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task RequestStopAsync()
        {
            if (Interlocked.Exchange(ref _stopRequested, 1) == 1)
            {
                return;
            }

            _console.WriteInfo("Stopp angefordert. Laufende Aufträge werden beendet.");
            _pollingCancellationTokenSource.Cancel();
            await SendBridgeStatusNotificationAsync("Die Bridge wird beendet. Neue Nachrichten werden nicht mehr angenommen.", CancellationToken.None);
            await _chatRequestQueue.BeginShutdownAsync(CancellationToken.None);
        }
    }
}
