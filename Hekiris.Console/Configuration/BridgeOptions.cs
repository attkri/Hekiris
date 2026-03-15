namespace Hekiris.Infrastructure.Configuration;

public sealed class BridgeOptions
{
    public TelegramOptions Telegram { get; set; } = new();

    public OpenCodeOptions OpenCode { get; set; } = new();

    public AccessControlOptions AccessControl { get; set; } = new();

    public RuntimeOptions Runtime { get; set; } = new();

    public ChatBindingOptions Chat { get; set; } = new();

    public List<ConfiguredCommandOptions> Commands { get; set; } = new();
}

public sealed class TelegramOptions
{
    public string ApiBaseUrl { get; set; } = "https://api.telegram.org";

    public string SecretSourcePath { get; set; } = string.Empty;

    public string BotToken { get; set; } = string.Empty;

    public int PollingTimeoutSeconds { get; set; } = 20;
}

public sealed class OpenCodeOptions
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:4096/";

    public string Username { get; set; } = "opencode";

    public string Password { get; set; } = string.Empty;

    public int RequestTimeoutSeconds { get; set; } = 300;
}

public sealed class AccessControlOptions
{
    public long AllowedUserId { get; set; }

    public string AllowedUsername { get; set; } = string.Empty;
}

public sealed class RuntimeOptions
{
    public int QueueCapacityPerChat { get; set; } = 20;

    public int TelegramRetryDelaySeconds { get; set; } = 5;

    public int OpenCodeHealthCheckIntervalSeconds { get; set; } = 30;

    public bool RejectMessagesWhenStopping { get; set; } = true;

    public bool SkipPendingUpdatesOnStart { get; set; } = true;
}

public sealed class ChatBindingOptions
{
    public long TelegramChatId { get; set; }

    public string OpenCodeSessionId { get; set; } = string.Empty;

    public string Agent { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;
}

public sealed class ConfiguredCommandOptions
{
    public string Title { get; set; } = string.Empty;

    public string Session { get; set; } = string.Empty;

    public string Agent { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    public CommandTimeLoopOptions? TimeLoop { get; set; }
}

public sealed class CommandTimeLoopOptions
{
    public bool Enabled { get; set; }

    public string Interval { get; set; } = string.Empty;

    public DateTime? LastRun { get; set; }
}

public sealed record LoadedBridgeConfiguration(BridgeOptions Options, string ConfigPath);

public sealed class ConfigurationValidationResult
{
    public List<string> Errors { get; } = new();

    public bool IsValid => Errors.Count == 0;

    public void Add(string message)
    {
        Errors.Add(message);
    }
}

public sealed class ConfigurationException : Exception
{
    public ConfigurationException(string message)
        : base(message)
    {
    }
}
