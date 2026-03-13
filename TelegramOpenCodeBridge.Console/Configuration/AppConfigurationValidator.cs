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
        }

        return result;
    }

    private static string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }
}
