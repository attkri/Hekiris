using TelegramOpenCodeBridge.Configuration;

namespace TelegramOpenCodeBridge.Tests.Configuration;

public sealed class AppConfigurationValidatorTests
{
    [Fact]
    public void Validate_ReturnsErrors_ForInvalidConfiguration()
    {
        BridgeOptions options = new()
        {
            Telegram = new TelegramOptions
            {
                ApiBaseUrl = "not-a-url",
                BotToken = string.Empty,
                SecretSourcePath = "missing.json",
                PollingTimeoutSeconds = -1,
            },
            OpenCode = new OpenCodeOptions
            {
                BaseUrl = "invalid",
                RequestTimeoutSeconds = 0,
            },
            Runtime = new RuntimeOptions
            {
                QueueCapacityPerChat = 0,
                TelegramRetryDelaySeconds = 0,
                OpenCodeHealthCheckIntervalSeconds = 0,
            },
            Chats =
            [
                new ChatBindingOptions { TelegramChatId = 0, OpenCodeSessionId = "" },
            ],
        };

        ConfigurationValidationResult result = new AppConfigurationValidator().Validate(options);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Telegram.ApiBaseUrl", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Telegram.BotToken", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("OpenCode.BaseUrl", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("QueueCapacityPerChat", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("OpenCodeHealthCheckIntervalSeconds", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("OpenCodeSessionId", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ReturnsError_ForDuplicateChatIds()
    {
        BridgeOptions options = CreateValidOptions();
        options.Chats.Add(new ChatBindingOptions { TelegramChatId = 1, OpenCodeSessionId = "ses_other" });

        ConfigurationValidationResult result = new AppConfigurationValidator().Validate(options);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("mehrfach", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_Succeeds_ForValidConfiguration()
    {
        ConfigurationValidationResult result = new AppConfigurationValidator().Validate(CreateValidOptions());

        Assert.True(result.IsValid);
    }

    private static BridgeOptions CreateValidOptions()
    {
        return new BridgeOptions
        {
            Telegram = new TelegramOptions
            {
                ApiBaseUrl = "https://api.telegram.org",
                BotToken = "token",
                PollingTimeoutSeconds = 20,
            },
            OpenCode = new OpenCodeOptions
            {
                BaseUrl = "http://localhost:4096/",
                RequestTimeoutSeconds = 30,
            },
            Runtime = new RuntimeOptions
            {
                QueueCapacityPerChat = 3,
                TelegramRetryDelaySeconds = 1,
                OpenCodeHealthCheckIntervalSeconds = 30,
            },
            Chats =
            [
                new ChatBindingOptions { TelegramChatId = 1, OpenCodeSessionId = "ses_valid" },
            ],
        };
    }
}
