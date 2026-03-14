using Hekiris.Configuration;

namespace Hekiris.Tests.Configuration;

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
            AccessControl = new AccessControlOptions
            {
                AllowedUsernames = [""]
            },
            Chats =
            [
                new ChatBindingOptions { TelegramChatId = 0, OpenCodeSessionId = "", AllowedUsernames = [""] },
            ],
            Commands =
            [
                new ConfiguredCommandOptions
                {
                    Title = "",
                    Session = "abc",
                    Model = "",
                    Prompt = "",
                    TimeLoop = new CommandTimeLoopOptions
                    {
                        Enabled = true,
                        Interval = "xx",
                    },
                },
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
        Assert.Contains(result.Errors, error => error.Contains("AccessControl.AllowedUsernames", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("AllowedUsernames", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Commands[1].Title", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Commands[1].Session", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Commands[1].Model", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Commands[1].Prompt", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("TimeLoop.Interval", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ReturnsError_ForDuplicateChatIds()
    {
        BridgeOptions options = CreateValidOptions();
        options.Chats.Add(new ChatBindingOptions { TelegramChatId = 1, OpenCodeSessionId = "ses_other" });

        ConfigurationValidationResult result = new AppConfigurationValidator().Validate(options);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("more than once", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_Succeeds_ForValidConfiguration()
    {
        ConfigurationValidationResult result = new AppConfigurationValidator().Validate(CreateValidOptions());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_AllowsRelativeSecretPathFromFixedConfigDirectory()
    {
        string configDirectory = BridgePaths.GetConfigDirectoryPath();
        Directory.CreateDirectory(configDirectory);
        string secretFileName = $"validator-secret-{Guid.NewGuid():N}.json";
        string secretFilePath = Path.Combine(configDirectory, secretFileName);

        try
        {
            File.WriteAllText(secretFilePath, "{}");

            BridgeOptions options = CreateValidOptions();
            options.Telegram.SecretSourcePath = secretFileName;

            ConfigurationValidationResult result = new AppConfigurationValidator().Validate(options);

            Assert.True(result.IsValid);
        }
        finally
        {
            if (File.Exists(secretFilePath))
            {
                File.Delete(secretFilePath);
            }
        }
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
