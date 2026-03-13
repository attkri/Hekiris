namespace TelegramOpenCodeBridge.Configuration;

public sealed class AppConfigurationValidator
{
    public ConfigurationValidationResult Validate(BridgeOptions options)
    {
        ConfigurationValidationResult result = new();

        if (!Uri.TryCreate(options.Telegram.ApiBaseUrl, UriKind.Absolute, out _))
        {
            result.Add("Telegram.ApiBaseUrl muss eine absolute URL sein.");
        }

        if (string.IsNullOrWhiteSpace(options.Telegram.BotToken))
        {
            result.Add("Telegram.BotToken fehlt. Entweder direkt setzen oder über Telegram.SecretSourcePath laden.");
        }

        if (!string.IsNullOrWhiteSpace(options.Telegram.SecretSourcePath))
        {
            string resolvedSecretPath = ResolvePath(options.Telegram.SecretSourcePath);
            if (!File.Exists(resolvedSecretPath) && string.IsNullOrWhiteSpace(options.Telegram.BotToken))
            {
                result.Add($"Telegram.SecretSourcePath wurde gesetzt, aber die Datei wurde nicht gefunden: {resolvedSecretPath}");
            }
        }

        if (options.Telegram.PollingTimeoutSeconds < 0)
        {
            result.Add("Telegram.PollingTimeoutSeconds darf nicht negativ sein.");
        }

        if (!Uri.TryCreate(options.OpenCode.BaseUrl, UriKind.Absolute, out _))
        {
            result.Add("OpenCode.BaseUrl muss eine absolute URL sein.");
        }

        if (options.OpenCode.RequestTimeoutSeconds <= 0)
        {
            result.Add("OpenCode.RequestTimeoutSeconds muss größer als 0 sein.");
        }

        if (options.Runtime.QueueCapacityPerChat <= 0)
        {
            result.Add("Runtime.QueueCapacityPerChat muss größer als 0 sein.");
        }

        if (options.Runtime.TelegramRetryDelaySeconds <= 0)
        {
            result.Add("Runtime.TelegramRetryDelaySeconds muss größer als 0 sein.");
        }

        if (options.Runtime.OpenCodeHealthCheckIntervalSeconds <= 0)
        {
            result.Add("Runtime.OpenCodeHealthCheckIntervalSeconds muss größer als 0 sein.");
        }

        if (options.Chats.Count == 0)
        {
            result.Add("Mindestens ein Eintrag in Chats ist erforderlich.");
        }

        IEnumerable<long> duplicateChatIds = options.Chats
            .GroupBy(item => item.TelegramChatId)
            .Where(group => group.Key != 0 && group.Count() > 1)
            .Select(group => group.Key);

        foreach (long chatId in duplicateChatIds)
        {
            result.Add($"TelegramChatId ist mehrfach konfiguriert: {chatId}");
        }

        foreach (ChatBindingOptions binding in options.Chats)
        {
            if (binding.TelegramChatId == 0)
            {
                result.Add("Chats[].TelegramChatId darf nicht 0 sein.");
            }

            if (string.IsNullOrWhiteSpace(binding.OpenCodeSessionId))
            {
                result.Add($"Für Chat {binding.TelegramChatId} fehlt die OpenCodeSessionId.");
            }
            else if (!binding.OpenCodeSessionId.StartsWith("ses", StringComparison.OrdinalIgnoreCase))
            {
                result.Add($"Die OpenCodeSessionId für Chat {binding.TelegramChatId} muss mit 'ses' beginnen.");
            }

            if (binding.AllowedUsernames.Any(username => string.IsNullOrWhiteSpace(username)))
            {
                result.Add($"Chats[{binding.TelegramChatId}].AllowedUsernames darf keine leeren Werte enthalten.");
            }
        }

        if (options.AccessControl.AllowedUsernames.Any(username => string.IsNullOrWhiteSpace(username)))
        {
            result.Add("AccessControl.AllowedUsernames darf keine leeren Werte enthalten.");
        }

        for (int index = 0; index < options.Commands.Count; index++)
        {
            ConfiguredCommandOptions command = options.Commands[index];
            int displayIndex = index + 1;

            if (string.IsNullOrWhiteSpace(command.Title))
            {
                result.Add($"Commands[{displayIndex}].Title darf nicht leer sein.");
            }

            if (string.IsNullOrWhiteSpace(command.Session))
            {
                result.Add($"Commands[{displayIndex}].Session darf nicht leer sein.");
            }
            else if (!command.Session.StartsWith("ses", StringComparison.OrdinalIgnoreCase))
            {
                result.Add($"Commands[{displayIndex}].Session muss mit 'ses' beginnen.");
            }

            if (string.IsNullOrWhiteSpace(command.Model))
            {
                result.Add($"Commands[{displayIndex}].Model darf nicht leer sein.");
            }

            if (string.IsNullOrWhiteSpace(command.Prompt))
            {
                result.Add($"Commands[{displayIndex}].Prompt darf nicht leer sein.");
            }

            if (command.TimeLoop?.Enabled == true)
            {
                if (string.IsNullOrWhiteSpace(command.TimeLoop.Interval))
                {
                    result.Add($"Commands[{displayIndex}].TimeLoop.Interval darf nicht leer sein, wenn TimeLoop.Enabled=true ist.");
                }
                else
                {
                    try
                    {
                        _ = Application.CommandTimeLoopScheduler.ParseInterval(command.TimeLoop.Interval);
                    }
                    catch (FormatException exception)
                    {
                        result.Add($"Commands[{displayIndex}].TimeLoop.Interval ist ungültig: {exception.Message}");
                    }
                }
            }
        }

        return result;
    }

    private static string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(BridgePaths.GetConfigDirectoryPath(), path));
    }
}
