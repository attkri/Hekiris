using System.Text;
using Hekiris.Cli;
using Hekiris.Configuration;
using Hekiris.ConsoleOutput;
using Hekiris.OpenCode;
using Hekiris.Telegram;

namespace Hekiris.Application;

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
            _console.WriteError($"Telegram check failed: {exception.Message}");
        }

        try
        {
            OpenCodeHealth health = await openCodeClient.GetHealthAsync(cancellationToken);
            _console.WriteInfo($"OpenCode: OK (Version {health.Version})");
        }
        catch (Exception exception)
        {
            failed = true;
            _console.WriteError($"OpenCode health check failed: {exception.Message}");
        }

        IEnumerable<string> sessionIds = new[] { options.Chat.OpenCodeSessionId }
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
                    _console.WriteError($"OpenCode session not found: {sessionId}");
                    continue;
                }

                _console.WriteInfo($"OpenCode session found: {sessionId}");
            }
            catch (Exception exception)
            {
                failed = true;
                _console.WriteError($"Session check failed for {sessionId}: {exception.Message}");
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
        _console.WriteError("Configuration is invalid:");

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
            _console.WriteWarning("Warning: OpenCode is reached over unencrypted HTTP without a password on a remote host.");
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
            _console.WriteInfo("Hekiris started. Press CTRL+C to stop.");

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
                await EvaluateScheduledCommandsAsync("at startup", cancellationToken);
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
                        _console.WriteError($"Telegram polling failed: {exception.Message}");
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
                            _console.WriteError($"Telegram update {update.UpdateId} could not be processed: {exception.Message}");
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
            _console.WriteInfo($"Skipping {pendingUpdates.Count} pending Telegram updates at startup.");
            return nextOffset;
        }

        private async Task HandleIncomingMessageAsync(TelegramMessage message)
        {
            string incomingText = message.Text!.Trim();
            _console.WriteInfo($"Incoming message: chat {message.Chat.Id}, userId {FormatUserId(message.From?.Id)}, username {FormatUsername(message.From?.Username)}.");

            if (IsStopping)
            {
                await RejectStoppingMessageAsync(message.Chat.Id);
                return;
            }

            if (!string.Equals(message.Chat.Type, "private", StringComparison.OrdinalIgnoreCase))
            {
                _console.WriteInfo($"Chat {message.Chat.Id} is ignored because only private chats are supported.");
                return;
            }

            ChatBindingOptions chatBinding = _options.Chat;
            if (chatBinding.TelegramChatId != message.Chat.Id)
            {
                _console.WriteInfo($"Chat {message.Chat.Id} is not allowed and will be ignored.");
                return;
            }

            if (!IsUserAllowed(message, chatBinding))
            {
                _console.WriteInfo($"Unauthorized message discarded: chat {message.Chat.Id}, userId {FormatUserId(message.From?.Id)}, username {FormatUsername(message.From?.Username)}.");
                return;
            }

            BridgeChatCommand chatCommand = BridgeChatCommandParser.Parse(incomingText);
            switch (chatCommand.Type)
            {
                case BridgeChatCommandType.Help:
                    await _telegramClient.SendMessageAsync(message.Chat.Id, BridgeChatHelpText.GetText(), CancellationToken.None);
                    _console.WriteInfo($"Help sent to chat {message.Chat.Id}.");
                    return;
                case BridgeChatCommandType.Stop:
                    await RequestStopAsync();
                    return;
                case BridgeChatCommandType.ShowStatus:
                    await _telegramClient.SendMessageAsync(message.Chat.Id, BuildStatusText(message.Chat.Id), CancellationToken.None);
                    _console.WriteInfo($"Status sent to chat {message.Chat.Id}.");
                    return;
                case BridgeChatCommandType.ShowCommands:
                    await _telegramClient.SendMessageAsync(message.Chat.Id, BuildConfiguredCommandsText(), CancellationToken.None);
                    _console.WriteInfo($"Command list sent to chat {message.Chat.Id}.");
                    return;
                case BridgeChatCommandType.StopConfiguredCommand:
                    if (chatCommand.CommandIndex is null || chatCommand.CommandIndex.Value >= _options.Commands.Count)
                    {
                        await _telegramClient.SendMessageAsync(message.Chat.Id, "This configured command does not exist. Use /sc for the list.", CancellationToken.None);
                        _console.WriteWarning($"Invalid stop command /c{(chatCommand.CommandIndex ?? 0) + 1}s requested in chat {message.Chat.Id}.");
                        return;
                    }

                    int commandNumber = chatCommand.CommandIndex.Value + 1;
                    bool stopped = await _chatRequestQueue.TryAbortActiveConfiguredCommandAsync(commandNumber, CancellationToken.None);
                    if (stopped)
                    {
                        await _telegramClient.SendMessageAsync(message.Chat.Id, $"Command /c{commandNumber} was stopped.", CancellationToken.None);
                        _console.WriteInfo($"Configured command /c{commandNumber} in chat {message.Chat.Id} was stopped.");
                    }
                    else
                    {
                        await _telegramClient.SendMessageAsync(message.Chat.Id, $"Command /c{commandNumber} is not running right now.", CancellationToken.None);
                        _console.WriteInfo($"Configured command /c{commandNumber} in chat {message.Chat.Id} was not active.");
                    }

                    return;
                case BridgeChatCommandType.RunConfiguredCommand:
                    if (chatCommand.CommandIndex is null || chatCommand.CommandIndex.Value >= _options.Commands.Count)
                    {
                        await _telegramClient.SendMessageAsync(message.Chat.Id, "This configured command does not exist. Use /sc for the list.", CancellationToken.None);
                        _console.WriteWarning($"Invalid command /c{(chatCommand.CommandIndex ?? 0) + 1} requested in chat {message.Chat.Id}.");
                        return;
                    }

                    ConfiguredCommandOptions configuredCommand = _options.Commands[chatCommand.CommandIndex.Value];
                    string effectiveSessionId = ResolveCommandSessionId(chatBinding, configuredCommand);
                    ChatRequest configuredRequest = new(
                        message.Chat.Id,
                        new[] { message.Chat.Id },
                        message.From?.Id,
                        message.From?.Username,
                        configuredCommand.Prompt,
                        effectiveSessionId,
                        ResolveCommandAgent(chatBinding, configuredCommand),
                        configuredCommand.Title,
                        chatCommand.CommandIndex.Value + 1,
                        false);

                    await _chatRequestQueue.EnqueueAsync(configuredRequest, CancellationToken.None);
                    _console.WriteInfo($"Configured command /c{chatCommand.CommandIndex.Value + 1} queued for chat {message.Chat.Id}.");
                    return;
                case BridgeChatCommandType.Unknown:
                    await _telegramClient.SendMessageAsync(message.Chat.Id, "Unknown Hekiris command. Use /help.", CancellationToken.None);
                    _console.WriteWarning($"Unknown Hekiris command received in chat {message.Chat.Id}.");
                    return;
            }

            ChatRequest request = new(
                message.Chat.Id,
                new[] { message.Chat.Id },
                message.From?.Id,
                message.From?.Username,
                incomingText,
                chatBinding.OpenCodeSessionId,
                string.IsNullOrWhiteSpace(chatBinding.Agent) ? null : chatBinding.Agent,
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
                ? $"Configured command {commandLabel}"
                : request.Text;
            string outboundConsoleText = isConfiguredCommand
                ? $"Configured command {commandLabel} sent to OpenCode."
                : $"Request sent to OpenCode: \"{request.Text}\"";

            _console.WriteTranscript(
                "USER",
                inboundConsoleText,
                isConfiguredCommand
                    ? $"Configured command /c{request.ConfiguredCommandNumber} received in chat {request.ChatId}."
                    : $"Telegram message received in chat {request.ChatId}.");
            _console.WriteTranscript(
                "BRIDGE",
                outboundConsoleText,
                isConfiguredCommand
                    ? $"OpenCode command /c{request.ConfiguredCommandNumber} for chat {request.ChatId} sent to session {request.OpenCodeSessionId}."
                    : $"OpenCode request for chat {request.ChatId} sent to session {request.OpenCodeSessionId}.");

            try
            {
                string response = await _openCodeClient.SendPromptAsync(request.OpenCodeSessionId, request.Text, request.ConfiguredAgent, cancellationToken);
                await _openCodeAvailabilityTracker.ReportAvailableAsync(CancellationToken.None);

                if (string.IsNullOrWhiteSpace(response))
                {
                    response = "OpenCode returned no text response.";
                }

                string telegramResponse = isConfiguredCommand
                    ? $"[{commandLabel}]\n\n{response}"
                    : response;

                _console.WriteTranscript(
                    "AGENT",
                    telegramResponse,
                    isConfiguredCommand
                        ? $"OpenCode response for {commandLabel} received in chat {request.ChatId}."
                        : $"OpenCode response received in chat {request.ChatId}.");
                await SendMessageToRequestTargetsAsync(request, telegramResponse, CancellationToken.None);
                _console.WriteInfo($"Telegram response sent for request from chat {request.ChatId}.");
            }
            catch (Exception exception)
            {
                string userMessage = IsStopping
                    ? "Hekiris is shutting down. The running job was aborted."
                    : "The request to OpenCode failed. Please try again.";

                if (isConfiguredCommand)
                {
                    userMessage = $"[{commandLabel}]\n\n{userMessage}";
                }

                if (IsPotentialOpenCodeOutage(exception, cancellationToken))
                {
                    await _openCodeAvailabilityTracker.ReportUnavailableAsync(exception.Message, CancellationToken.None);
                }

                _console.WriteError($"Error in chat {request.ChatId}: {exception.Message}");

                if (!IsStopping || _options.Runtime.RejectMessagesWhenStopping)
                {
                    await SendMessageToRequestTargetsAsync(request, userMessage, CancellationToken.None);
                    _console.WriteInfo($"Error message sent for request from chat {request.ChatId}.");
                }
            }
        }

        private string BuildStatusText(long chatId)
        {
            ChatRuntimeStatusSnapshot runtimeStatus = _chatRequestQueue.GetChatRuntimeStatus(chatId, _options.Commands.Count);
            StringBuilder builder = new();
            builder.AppendLine("Hekiris status:");
            builder.AppendLine($"- Hekiris: {(IsStopping ? "stopping" : "running")}");
            builder.AppendLine($"- OpenCode: {(_openCodeAvailabilityTracker.IsAvailable ? "reachable" : "unreachable")}");
            builder.AppendLine($"- Base session: {FormatRuntimeState(runtimeStatus.BaseSessionState)}");
            builder.AppendLine($"- Configured base agent: {FormatConfiguredAgent(_options.Chat.Agent)}");

            if (runtimeStatus.ActiveRequest is not null)
            {
                builder.AppendLine(runtimeStatus.ActiveRequest.ConfiguredCommandNumber is null
                    ? "- Active job: base session"
                    : $"- Active job: /c{runtimeStatus.ActiveRequest.ConfiguredCommandNumber} {runtimeStatus.ActiveRequest.ConfiguredCommandTitle}");
            }
            else
            {
                builder.AppendLine("- Active job: none");
            }

            if (_options.Commands.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Commands:");

                for (int index = 0; index < _options.Commands.Count; index++)
                {
                    int commandNumber = index + 1;
                    RequestRuntimeState state = runtimeStatus.CommandStates[commandNumber];
                    ConfiguredCommandOptions command = _options.Commands[index];
                    CommandTimeLoopOptions? timeLoop = command.TimeLoop;
                    string configuredAgent = FormatConfiguredAgent(ResolveCommandAgent(_options.Chat, command));

                    string loopStatus = timeLoop?.Enabled == true ? "on" : "off";
                    string interval = string.IsNullOrWhiteSpace(timeLoop?.Interval) ? "-" : timeLoop!.Interval;
                    string lastRun = timeLoop?.LastRun is null ? "-" : timeLoop.LastRun.Value.ToString("yyyy-MM-dd HH:mm:ss");

                    builder.AppendLine(
                        $"- /c{commandNumber} {command.Title}: {FormatRuntimeState(state)} | Agent {configuredAgent} | Loop {loopStatus} | Interval {interval} | LastRun {lastRun}");
                }
            }

            builder.AppendLine();
            builder.AppendLine("Note: /sc shows the available commands.");
            return builder.ToString().TrimEnd();
        }

        private string BuildConfiguredCommandsText()
        {
            if (_options.Commands.Count == 0)
            {
                return "There are currently no configured commands.";
            }

            StringBuilder builder = new();
            builder.AppendLine("Available commands:");

            for (int index = 0; index < _options.Commands.Count; index++)
            {
                builder.AppendLine($"{index + 1}. {_options.Commands[index].Title} (/c{index + 1})");
            }

            builder.AppendLine();
            builder.AppendLine("/cNs stops a running command");

            return builder.ToString().TrimEnd();
        }

        private static string ResolveCommandSessionId(ChatBindingOptions chatBinding, ConfiguredCommandOptions command)
        {
            return string.IsNullOrWhiteSpace(command.Session)
                ? chatBinding.OpenCodeSessionId
                : command.Session;
        }

        private static string? ResolveCommandAgent(ChatBindingOptions chatBinding, ConfiguredCommandOptions command)
        {
            if (!string.IsNullOrWhiteSpace(command.Agent))
            {
                return command.Agent;
            }

            return string.IsNullOrWhiteSpace(chatBinding.Agent) ? null : chatBinding.Agent;
        }

        private static string FormatRuntimeState(RequestRuntimeState state)
        {
            return state switch
            {
                RequestRuntimeState.Running => "running",
                RequestRuntimeState.Queued => "queued",
                _ => "free",
            };
        }

        private static string FormatConfiguredAgent(string? agent)
        {
            return string.IsNullOrWhiteSpace(agent) ? "session default" : agent;
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

                    await EvaluateScheduledCommandsAsync("on schedule", cancellationToken);
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
                long[] targetChatIds = [_options.Chat.TelegramChatId];
                if (targetChatIds.Length == 0)
                {
                    return;
                }

                ChatBindingOptions defaultChatBinding = _options.Chat;

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
                        ResolveCommandSessionId(defaultChatBinding, command),
                        ResolveCommandAgent(defaultChatBinding, command),
                        command.Title,
                        index + 1,
                        true);

                    bool enqueued = await _chatRequestQueue.EnqueueAsync(scheduledRequest, cancellationToken);
                    if (!enqueued)
                    {
                        _console.WriteWarning($"Scheduled command /c{index + 1} {command.Title} could not be queued.");
                        continue;
                    }

                    DateTime lastRun = _commandTimeLoopScheduler.GetCurrentTime();
                    await _commandTimeLoopStateStore.UpdateLastRunAsync(index, lastRun, cancellationToken);
                    command.TimeLoop ??= new CommandTimeLoopOptions();
                    command.TimeLoop.LastRun = lastRun;
                    _console.WriteInfo($"TimeLoop LastRun for /c{index + 1} {command.Title} updated to {lastRun:yyyy-MM-dd HH:mm:ss}.");

                    string notification = $"Command /c{index + 1} {command.Title} was triggered automatically ({reason}).";
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
            _console.WriteInfo($"System message sent for request from chat {request.ChatId}.");
        }

        private async Task AbortActiveRequestAsync(ChatRequest request, CancellationToken cancellationToken)
        {
            _console.WriteInfo($"Sending stop signal to OpenCode session {request.OpenCodeSessionId}.");
            await _openCodeClient.AbortSessionAsync(request.OpenCodeSessionId, cancellationToken);
        }

        private async Task RejectStoppingMessageAsync(long chatId)
        {
            if (_options.Runtime.RejectMessagesWhenStopping)
            {
                await _telegramClient.SendMessageAsync(
                    chatId,
                    "Hekiris is shutting down. New messages are no longer accepted.",
                    CancellationToken.None);
                _console.WriteInfo($"Shutdown notice sent to chat {chatId}.");
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
                            await EvaluateScheduledCommandsAsync("after reconnection", cancellationToken);
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
                return $"Hekiris started and is ready. (/help) Base agent: {FormatConfiguredAgent(_options.Chat.Agent)}";
            }
            catch (Exception exception)
            {
                _openCodeAvailabilityTracker.SetAvailability(false);
                _console.WriteWarning($"OpenCode server is unreachable at startup: {exception.Message}");
                return $"Hekiris started, but the OpenCode server is currently unreachable. (/help) Base agent: {FormatConfiguredAgent(_options.Chat.Agent)}";
            }
        }

        private async Task SendBridgeStatusNotificationAsync(string message, CancellationToken cancellationToken)
        {
            foreach (long chatId in new[] { _options.Chat.TelegramChatId }.Distinct())
            {
                try
                {
                    await _telegramClient.SendMessageAsync(chatId, message, cancellationToken);
                    _console.WriteInfo($"Hekiris status message sent to chat {chatId}.");
                }
                catch (Exception exception)
                {
                    _console.WriteWarning($"Hekiris status message could not be sent to chat {chatId}: {exception.Message}");
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

            _console.WriteInfo("Shutdown requested. Running jobs are being stopped.");
            await SendBridgeStatusNotificationAsync("Hekiris is shutting down. New messages are no longer accepted.", CancellationToken.None);
            _pollingCancellationTokenSource.Cancel();
            await _chatRequestQueue.BeginShutdownAsync(CancellationToken.None);
        }
    }
}
