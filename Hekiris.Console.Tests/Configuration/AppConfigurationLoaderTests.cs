using Hekiris.Configuration;

namespace Hekiris.Tests.Configuration;

public sealed class AppConfigurationLoaderTests
{
    [Fact]
    public void Load_NormalizesSingleAllowedUserValues()
    {
        string directory = CreateTempDirectory();
        string configPath = Path.Combine(directory, "config.json");

        try
        {
            File.WriteAllText(
                configPath,
                """
                {
                  "AccessControl": {
                    "AllowedUserIds": "1700580252",
                    "AllowedUsernames": "attkri"
                  },
                  "Telegram": {
                    "ApiBaseUrl": "https://api.telegram.org",
                    "BotToken": "token"
                  },
                  "OpenCode": {
                    "BaseUrl": "http://localhost:4096/",
                    "RequestTimeoutSeconds": 30
                  },
                  "Runtime": {
                    "QueueCapacityPerChat": 1,
                    "TelegramRetryDelaySeconds": 1,
                    "OpenCodeHealthCheckIntervalSeconds": 30
                  },
                  "Chat": {
                    "TelegramChatId": 1,
                    "OpenCodeSessionId": "ses_test"
                  }
                }
                """);

            LoadedBridgeConfiguration loaded = new AppConfigurationLoader(configPath).Load();

            Assert.Equal([1700580252L], loaded.Options.AccessControl.AllowedUserIds);
            Assert.Equal(["attkri"], loaded.Options.AccessControl.AllowedUsernames);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ocbridge-loader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
