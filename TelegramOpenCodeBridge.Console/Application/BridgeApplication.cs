using System.Text;
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
            loadedConfiguration = new AppConfigurationLoader().Load();
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

        IEnumerable<string> sessionIds = options.Chats
            .Select(binding => binding.OpenCodeSessionId)
            .Concat(options.Commands.Select(command => command.Session))
            .Where(sessionId => !string.IsNullOrWhiteSpace(sessionId))
            .Distinct(StringComparer.Ordinal);

        foreach (string sessionId in sessionIds)
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
        private readonly CommandTimeLoopScheduler _commandTimeLoopScheduler = new();
        private readonly CommandTimeLoopStateStore _commandTimeLoopStateStore = new();
        private readonly SemaphoreSlim _scheduledCommandGate = new(1, 1);
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
            Task scheduledCommandsTask = MonitorScheduledCommandsAsync(_pollingCancellationTokenSource.Token);

            if (_options.Runtime.SkipPendingUpdatesOnStart)
            {
                offset = await ResolveStartupOffsetAsync(cancellationToken);
            }

            string startupNotification = await DetermineStartupNotificationAsync(cancellationToken);
            await SendBridgeStatusNotificationAsync(startupNotification, CancellationToken.None);

            if (_openCodeAvailabilityTracker.IsAvailable)
            {
                await EvaluateScheduledCommandsAsync("beim Start", cancellationToken);
            }

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

                try
                {
                    await scheduledCommandsTask;
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
            string incomingText = message.Text!.Trim();
            _console.WriteInfo($"Eingehende Nachricht: Chat {message.Chat.Id}, UserId {FormatUserId(message.From?.Id)}, Username {FormatUsername(message.From?.Username)}.");

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
                _console.WriteInfo($"Nicht freigegebene Nachricht verworfen: Chat {message.Chat.Id}, UserId {FormatUserId(message.From?.Id)}, Username {FormatUsername(message.From?.Username)}.");
                return;
            }

            BridgeChatCommand chatCommand = BridgeChatCommandParser.Parse(incomingText);
            switch (chatCommand.Type)
            {
                case BridgeChatCommandType.Help:
                    await _telegramClient.SendMessageAsync(message.Chat.Id, BridgeChatHelpText.GetText(), CancellationToken.None);
                    _console.WriteInfo($"Chat-Hilfe an Chat {message.Chat.Id} gesendet.");
                    return;
                case BridgeChatCommandType.Stop:
                    await RequestStopAsync();
                    return;
                case BridgeChatCommandType.ShowStatus:
                    await _telegramClient.SendMessageAsync(message.Chat.Id, BuildStatusText(message.Chat.Id), CancellationToken.None);
                    _console.WriteInfo($"Status an Chat {message.Chat.Id} gesendet.");
                    return;
                case BridgeChatCommandType.ShowCommands:
                    await _telegramClient.SendMessageAsync(message.Chat.Id, BuildConfiguredCommandsText(), CancellationToken.None);
                    _console.WriteInfo($"Kommandoübersicht an Chat {message.Chat.Id} gesendet.");
                    return;
                case BridgeChatCommandType.StopConfiguredCommand:
                    if (chatCommand.CommandIndex is null || chatCommand.CommandIndex.Value >= _options.Commands.Count)
                    {
                        await _telegramClient.SendMessageAsync(message.Chat.Id, "Dieses konfigurierte Kommando gibt es nicht. Nutze /sc für die Liste.", CancellationToken.None);
                        _console.WriteWarning($"Ungültiges Stopp-Kommando /c{(chatCommand.CommandIndex ?? 0) + 1}s in Chat {message.Chat.Id} angefordert.");
                        return;
                    }

                    int commandNumber = chatCommand.CommandIndex.Value + 1;
                    bool stopped = await _chatRequestQueue.TryAbortActiveConfiguredCommandAsync(commandNumber, CancellationToken.None);
                    if (stopped)
                    {
                        await _telegramClient.SendMessageAsync(message.Chat.Id, $"Kommando /c{commandNumber} wurde gestoppt.", CancellationToken.None);
                        _console.WriteInfo($"Konfiguriertes Kommando /c{commandNumber} in Chat {message.Chat.Id} gestoppt.");
                    }
                    else
                    {
                        await _telegramClient.SendMessageAsync(message.Chat.Id, $"Kommando /c{commandNumber} läuft aktuell nicht.", CancellationToken.None);
                        _console.WriteInfo($"Konfiguriertes Kommando /c{commandNumber} in Chat {message.Chat.Id} war nicht aktiv.");
                    }

                    return;
                case BridgeChatCommandType.RunConfiguredCommand:
                    if (chatCommand.CommandIndex is null || chatCommand.CommandIndex.Value >= _options.Commands.Count)
                    {
                        await _telegramClient.SendMessageAsync(message.Chat.Id, "Dieses konfigurierte Kommando gibt es nicht. Nutze /sc für die Liste.", CancellationToken.None);
                        _console.WriteWarning($"Ungültiges Kommando /c{(chatCommand.CommandIndex ?? 0) + 1} in Chat {message.Chat.Id} angefordert.");
                        return;
                    }

                    ConfiguredCommandOptions configuredCommand = _options.Commands[chatCommand.CommandIndex.Value];
                    ChatRequest configuredRequest = new(
                        message.Chat.Id,
                        new[] { message.Chat.Id },
                        message.From?.Id,
                        message.From?.Username,
                        configuredCommand.Prompt,
                        configuredCommand.Session,
                        configuredCommand.Model,
                        configuredCommand.Title,
                        chatCommand.CommandIndex.Value + 1,
                        false);

                    await _chatRequestQueue.EnqueueAsync(configuredRequest, CancellationToken.None);
                    _console.WriteInfo($"Konfiguriertes Kommando /c{chatCommand.CommandIndex.Value + 1} für Chat {message.Chat.Id} eingeplant.");
                    return;
                case BridgeChatCommandType.Unknown:
                    await _telegramClient.SendMessageAsync(message.Chat.Id, "Unbekanntes Bridge-Kommando. Nutze /help.", CancellationToken.None);
                    _console.WriteWarning($"Unbekanntes Bridge-Kommando in Chat {message.Chat.Id} empfangen.");
                    return;
            }

            ChatRequest request = new(
                message.Chat.Id,
                new[] { message.Chat.Id },
                message.From?.Id,
                message.From?.Username,
                incomingText,
                chatBinding.OpenCodeSessionId,
                null,
                null,
                null,
                false);

            await _chatRequestQueue.EnqueueAsync(request, CancellationToken.None);
        }

        private bool IsUserAllowed(TelegramMessage message, ChatBindingOptions binding)
        {
            if (message.From?.Id is null)
            {
                return !_options.AccessControl.AllowedUserIds.Any()
                    && !_options.AccessControl.AllowedUsernames.Any()
                    && !binding.AllowedUserIds.Any()
                    && !binding.AllowedUsernames.Any();
            }

            long userId = message.From.Id.Value;
            string? username = NormalizeUsername(message.From?.Username);

            bool globallyAllowed = !_options.AccessControl.AllowedUserIds.Any() || _options.AccessControl.AllowedUserIds.Contains(userId);
            bool locallyAllowed = !binding.AllowedUserIds.Any() || binding.AllowedUserIds.Contains(userId);

            bool globallyAllowedByUsername = !_options.AccessControl.AllowedUsernames.Any()
                || (username is not null && _options.AccessControl.AllowedUsernames.Any(item => string.Equals(NormalizeUsername(item), username, StringComparison.OrdinalIgnoreCase)));
            bool locallyAllowedByUsername = !binding.AllowedUsernames.Any()
                || (username is not null && binding.AllowedUsernames.Any(item => string.Equals(NormalizeUsername(item), username, StringComparison.OrdinalIgnoreCase)));

            return globallyAllowed && locallyAllowed && globallyAllowedByUsername && locallyAllowedByUsername;
        }

        private static string FormatUserId(long? userId)
        {
            return userId?.ToString() ?? "-";
        }

        private static string FormatUsername(string? username)
        {
            return NormalizeUsername(username) ?? "-";
        }

        private static string? NormalizeUsername(string? username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            return username.Trim().TrimStart('@');
        }

        private async Task ProcessChatRequestAsync(ChatRequest request, CancellationToken cancellationToken)
        {
            bool isConfiguredCommand = request.ConfiguredCommandNumber is not null;
            string commandLabel = isConfiguredCommand
                ? $"/c{request.ConfiguredCommandNumber} {request.ConfiguredCommandTitle}"
                : string.Empty;
            string inboundConsoleText = isConfiguredCommand
                ? $"Konfiguriertes Kommando {commandLabel}"
                : request.Text;
            string outboundConsoleText = isConfiguredCommand
                ? $"Konfiguriertes Kommando {commandLabel} an OpenCode gesendet."
                : $"Anfrage an OpenCode gesendet: \"{request.Text}\"";

            _console.WriteTranscript(
                "USER",
                inboundConsoleText,
                isConfiguredCommand
                    ? $"Konfiguriertes Kommando /c{request.ConfiguredCommandNumber} in Chat {request.ChatId} empfangen."
                    : $"Telegram-Nachricht in Chat {request.ChatId} empfangen.");
            _console.WriteTranscript(
                "BRIDGE",
                outboundConsoleText,
                isConfiguredCommand
                    ? $"OpenCode-Kommando /c{request.ConfiguredCommandNumber} für Chat {request.ChatId} an Session {request.OpenCodeSessionId} mit Modell {request.ConfiguredModel} gesendet."
                    : $"OpenCode-Anfrage für Chat {request.ChatId} an Session {request.OpenCodeSessionId} gesendet.");

            try
            {
                OpenCodeModelSelection? modelSelection = string.IsNullOrWhiteSpace(request.ConfiguredModel)
                    ? null
                    : OpenCodeModelSelection.Parse(request.ConfiguredModel);

                string response = await _openCodeClient.SendPromptAsync(request.OpenCodeSessionId, request.Text, modelSelection, cancellationToken);
                await _openCodeAvailabilityTracker.ReportAvailableAsync(CancellationToken.None);

                if (string.IsNullOrWhiteSpace(response))
                {
                    response = "OpenCode hat keine Textantwort geliefert.";
                }

                string telegramResponse = isConfiguredCommand
                    ? $"[{commandLabel}]\n\n{response}"
                    : response;

                _console.WriteTranscript(
                    "AGENT",
                    telegramResponse,
                    isConfiguredCommand
                        ? $"OpenCode-Antwort für {commandLabel} in Chat {request.ChatId} empfangen."
                        : $"OpenCode-Antwort für Chat {request.ChatId} empfangen.");
                await SendMessageToRequestTargetsAsync(request, telegramResponse, CancellationToken.None);
                _console.WriteInfo($"Telegram-Antwort für Auftrag aus Chat {request.ChatId} gesendet.");
            }
            catch (Exception exception)
            {
                string userMessage = IsStopping
                    ? "Die Bridge wird beendet. Der laufende Auftrag wurde abgebrochen."
                    : "Die Anfrage an OpenCode ist fehlgeschlagen. Bitte versuche es erneut.";

                if (isConfiguredCommand)
                {
                    userMessage = $"[{commandLabel}]\n\n{userMessage}";
                }

                if (IsPotentialOpenCodeOutage(exception, cancellationToken))
                {
                    await _openCodeAvailabilityTracker.ReportUnavailableAsync(exception.Message, CancellationToken.None);
                }

                _console.WriteError($"Fehler bei Chat {request.ChatId}: {exception.Message}");

                if (!IsStopping || _options.Runtime.RejectMessagesWhenStopping)
                {
                    await SendMessageToRequestTargetsAsync(request, userMessage, CancellationToken.None);
                    _console.WriteInfo($"Fehlermeldung für Auftrag aus Chat {request.ChatId} gesendet.");
                }
            }
        }

        private string BuildStatusText(long chatId)
        {
            ChatRuntimeStatusSnapshot runtimeStatus = _chatRequestQueue.GetChatRuntimeStatus(chatId, _options.Commands.Count);
            StringBuilder builder = new();
            builder.AppendLine("Bridge-Status:");
            builder.AppendLine($"- Bridge: {(IsStopping ? "wird beendet" : "läuft")}");
            builder.AppendLine($"- OpenCode: {(_openCodeAvailabilityTracker.IsAvailable ? "erreichbar" : "nicht erreichbar")}");
            builder.AppendLine($"- Grund-Session: {FormatRuntimeState(runtimeStatus.BaseSessionState)}");

            if (runtimeStatus.ActiveRequest is not null)
            {
                builder.AppendLine(runtimeStatus.ActiveRequest.ConfiguredCommandNumber is null
                    ? "- Aktiver Auftrag: Grund-Session"
                    : $"- Aktiver Auftrag: /c{runtimeStatus.ActiveRequest.ConfiguredCommandNumber} {runtimeStatus.ActiveRequest.ConfiguredCommandTitle}");
            }
            else
            {
                builder.AppendLine("- Aktiver Auftrag: keiner");
            }

            if (_options.Commands.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Kommandos:");

                for (int index = 0; index < _options.Commands.Count; index++)
                {
                    int commandNumber = index + 1;
                    RequestRuntimeState state = runtimeStatus.CommandStates[commandNumber];
                    ConfiguredCommandOptions command = _options.Commands[index];
                    CommandTimeLoopOptions? timeLoop = command.TimeLoop;

                    string loopStatus = timeLoop?.Enabled == true ? "an" : "aus";
                    string interval = string.IsNullOrWhiteSpace(timeLoop?.Interval) ? "-" : timeLoop!.Interval;
                    string lastRun = timeLoop?.LastRun is null ? "-" : timeLoop.LastRun.Value.ToString("yyyy-MM-dd HH:mm:ss");

                    builder.AppendLine(
                        $"- /c{commandNumber} {command.Title}: {FormatRuntimeState(state)} | Loop {loopStatus} | Intervall {interval} | LastRun {lastRun}");
                }
            }

            builder.AppendLine();
            builder.AppendLine("Hinweis: /sc zeigt die verfügbaren Kommandos an.");
            return builder.ToString().TrimEnd();
        }

        private string BuildConfiguredCommandsText()
        {
            if (_options.Commands.Count == 0)
            {
                return "Es sind aktuell keine konfigurierten Kommandos hinterlegt.";
            }

            StringBuilder builder = new();
            builder.AppendLine("Verfügbare Kommandos:");

            for (int index = 0; index < _options.Commands.Count; index++)
            {
                builder.AppendLine($"{index + 1}. {_options.Commands[index].Title} (/c{index + 1})");
            }

            builder.AppendLine();
            builder.AppendLine("/cNs stoppt ein laufendes Commando");

            return builder.ToString().TrimEnd();
        }

        private static string FormatRuntimeState(RequestRuntimeState state)
        {
            return state switch
            {
                RequestRuntimeState.Running => "läuft",
                RequestRuntimeState.Queued => "wartet",
                _ => "frei",
            };
        }

        private async Task SendMessageToRequestTargetsAsync(ChatRequest request, string message, CancellationToken cancellationToken)
        {
            await SendMessageToChatsAsync(request.ResponseChatIds, message, cancellationToken);
        }

        private async Task SendMessageToChatsAsync(IEnumerable<long> chatIds, string message, CancellationToken cancellationToken)
        {
            foreach (long chatId in chatIds.Distinct())
            {
                await _telegramClient.SendMessageAsync(chatId, message, cancellationToken);
            }
        }

        private async Task MonitorScheduledCommandsAsync(CancellationToken cancellationToken)
        {
            using PeriodicTimer timer = new(TimeSpan.FromSeconds(Math.Min(_options.Runtime.OpenCodeHealthCheckIntervalSeconds, 30)));

            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    if (!_openCodeAvailabilityTracker.IsAvailable || IsStopping)
                    {
                        continue;
                    }

                    await EvaluateScheduledCommandsAsync("nach Zeitplan", cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task EvaluateScheduledCommandsAsync(string reason, CancellationToken cancellationToken)
        {
            await _scheduledCommandGate.WaitAsync(cancellationToken);

            try
            {
                long[] targetChatIds = _options.Chats.Select(chat => chat.TelegramChatId).Distinct().ToArray();
                if (targetChatIds.Length == 0)
                {
                    return;
                }

                for (int index = 0; index < _options.Commands.Count; index++)
                {
                    ConfiguredCommandOptions command = _options.Commands[index];
                    RequestRuntimeState state = _chatRequestQueue.GetCommandRuntimeState(index + 1);

                    if (!_commandTimeLoopScheduler.IsDue(command, state))
                    {
                        continue;
                    }

                    ChatRequest scheduledRequest = new(
                        targetChatIds[0],
                        targetChatIds,
                        null,
                        "scheduler",
                        command.Prompt,
                        command.Session,
                        command.Model,
                        command.Title,
                        index + 1,
                        true);

                    bool enqueued = await _chatRequestQueue.EnqueueAsync(scheduledRequest, cancellationToken);
                    if (!enqueued)
                    {
                        _console.WriteWarning($"Geplantes Kommando /c{index + 1} {command.Title} konnte nicht eingeplant werden.");
                        continue;
                    }

                    DateTime lastRun = _commandTimeLoopScheduler.GetCurrentTime();
                    await _commandTimeLoopStateStore.UpdateLastRunAsync(index, lastRun, cancellationToken);
                    command.TimeLoop ??= new CommandTimeLoopOptions();
                    command.TimeLoop.LastRun = lastRun;
                    _console.WriteInfo($"TimeLoop-LastRun für /c{index + 1} {command.Title} auf {lastRun:yyyy-MM-dd HH:mm:ss} aktualisiert.");

                    string notification = $"Kommando /c{index + 1} {command.Title} wurde automatisch abgesetzt ({reason}).";
                    await SendMessageToChatsAsync(targetChatIds, notification, cancellationToken);
                    _console.WriteInfo(notification);
                }
            }
            finally
            {
                _scheduledCommandGate.Release();
            }
        }

        private async Task RejectChatRequestAsync(ChatRequest request, string reason, CancellationToken cancellationToken)
        {
            _console.WriteInfo($"Chat {request.ChatId}: {reason}");
            await SendMessageToRequestTargetsAsync(request, reason, cancellationToken);
            _console.WriteInfo($"Systemmeldung für Auftrag aus Chat {request.ChatId} gesendet.");
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
                        bool wasAvailable = _openCodeAvailabilityTracker.IsAvailable;
                        await _openCodeClient.GetHealthAsync(cancellationToken);
                        await _openCodeAvailabilityTracker.ReportAvailableAsync(CancellationToken.None);

                        if (!wasAvailable)
                        {
                            await EvaluateScheduledCommandsAsync("nach Wiederverbindung", cancellationToken);
                        }
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
                return "Die Bridge wurde gestartet und ist betriebsbereit. (/help)";
            }
            catch (Exception exception)
            {
                _openCodeAvailabilityTracker.SetAvailability(false);
                _console.WriteWarning($"OpenCode-Server ist beim Start nicht erreichbar: {exception.Message}");
                return "Die Bridge wurde gestartet, aber der OpenCode-Server ist aktuell nicht erreichbar. (/help)";
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
            await SendBridgeStatusNotificationAsync("Die Bridge wird beendet. Neue Nachrichten werden nicht mehr angenommen.", CancellationToken.None);
            _pollingCancellationTokenSource.Cancel();
            await _chatRequestQueue.BeginShutdownAsync(CancellationToken.None);
        }
    }
}
