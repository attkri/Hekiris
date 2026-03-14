namespace Hekiris.Configuration;

public sealed class AppConfigurationValidator
{
    public ConfigurationValidationResult Validate(BridgeOptions options)
    {
        ConfigurationValidationResult result = new();

        if (!Uri.TryCreate(options.Telegram.ApiBaseUrl, UriKind.Absolute, out _))
        {
            result.Add("Telegram.ApiBaseUrl must be an absolute URL.");
        }

        if (string.IsNullOrWhiteSpace(options.Telegram.BotToken))
        {
            result.Add("Telegram.BotToken is missing. Set it directly or load it through Telegram.SecretSourcePath.");
        }

        if (!string.IsNullOrWhiteSpace(options.Telegram.SecretSourcePath))
        {
            string resolvedSecretPath = ResolvePath(options.Telegram.SecretSourcePath);
            if (!File.Exists(resolvedSecretPath) && string.IsNullOrWhiteSpace(options.Telegram.BotToken))
            {
                result.Add($"Telegram.SecretSourcePath is set, but the file was not found: {resolvedSecretPath}");
            }
        }

        if (options.Telegram.PollingTimeoutSeconds < 0)
        {
            result.Add("Telegram.PollingTimeoutSeconds must not be negative.");
        }

        if (!Uri.TryCreate(options.OpenCode.BaseUrl, UriKind.Absolute, out _))
        {
            result.Add("OpenCode.BaseUrl must be an absolute URL.");
        }

        if (options.OpenCode.RequestTimeoutSeconds <= 0)
        {
            result.Add("OpenCode.RequestTimeoutSeconds must be greater than 0.");
        }

        if (options.Runtime.QueueCapacityPerChat <= 0)
        {
            result.Add("Runtime.QueueCapacityPerChat must be greater than 0.");
        }

        if (options.Runtime.TelegramRetryDelaySeconds <= 0)
        {
            result.Add("Runtime.TelegramRetryDelaySeconds must be greater than 0.");
        }

        if (options.Runtime.OpenCodeHealthCheckIntervalSeconds <= 0)
        {
            result.Add("Runtime.OpenCodeHealthCheckIntervalSeconds must be greater than 0.");
        }

        ChatBindingOptions binding = options.Chat;
        if (binding is null)
        {
            result.Add("Chat configuration is required.");
            return result;
        }

        if (binding.TelegramChatId == 0)
        {
            result.Add("Chat.TelegramChatId must not be 0.");
        }

        if (string.IsNullOrWhiteSpace(binding.OpenCodeSessionId))
        {
            result.Add($"OpenCodeSessionId is missing for chat {binding.TelegramChatId}.");
        }
        else if (!binding.OpenCodeSessionId.StartsWith("ses", StringComparison.OrdinalIgnoreCase))
        {
            result.Add($"OpenCodeSessionId for chat {binding.TelegramChatId} must start with 'ses'.");
        }

        if (binding.AllowedUsernames.Any(username => string.IsNullOrWhiteSpace(username)))
        {
            result.Add($"Chat.AllowedUsernames must not contain empty values.");
        }

        if (!string.IsNullOrWhiteSpace(binding.WorkingDirectory) && !Path.IsPathRooted(binding.WorkingDirectory))
        {
            result.Add("Chat.WorkingDirectory must be an absolute path.");
        }

        if (options.AccessControl.AllowedUsernames.Any(username => string.IsNullOrWhiteSpace(username)))
        {
            result.Add("AccessControl.AllowedUsernames must not contain empty values.");
        }

        for (int index = 0; index < options.Commands.Count; index++)
        {
            ConfiguredCommandOptions command = options.Commands[index];
            int displayIndex = index + 1;

            if (string.IsNullOrWhiteSpace(command.Title))
            {
                result.Add($"Commands[{displayIndex}].Title must not be empty.");
            }

            if (!string.IsNullOrWhiteSpace(command.Session)
                && !command.Session.StartsWith("ses", StringComparison.OrdinalIgnoreCase))
            {
                result.Add($"Commands[{displayIndex}].Session must start with 'ses'.");
            }

            if (string.IsNullOrWhiteSpace(command.Prompt))
            {
                result.Add($"Commands[{displayIndex}].Prompt must not be empty.");
            }

            if (!string.IsNullOrWhiteSpace(command.WorkingDirectory) && !Path.IsPathRooted(command.WorkingDirectory))
            {
                result.Add($"Commands[{displayIndex}].WorkingDirectory must be an absolute path.");
            }

            if (command.TimeLoop?.Enabled == true)
            {
                if (string.IsNullOrWhiteSpace(command.TimeLoop.Interval))
                {
                    result.Add($"Commands[{displayIndex}].TimeLoop.Interval must not be empty when TimeLoop.Enabled=true.");
                }
                else
                {
                    try
                    {
                        _ = Application.CommandTimeLoopScheduler.ParseInterval(command.TimeLoop.Interval);
                    }
                    catch (FormatException exception)
                    {
                        result.Add($"Commands[{displayIndex}].TimeLoop.Interval is invalid: {exception.Message}");
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
