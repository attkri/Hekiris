using TelegramOpenCodeBridge.Configuration;

namespace TelegramOpenCodeBridge.Tests.Configuration;

public sealed class ConfigMaskerTests
{
    [Fact]
    public void ToSanitizedJson_RedactsSecrets()
    {
        BridgeOptions options = new()
        {
            Telegram = new TelegramOptions
            {
                BotToken = "secret-token",
            },
            OpenCode = new OpenCodeOptions
            {
                Password = "secret-password",
            },
        };

        string sanitized = ConfigMasker.ToSanitizedJson(options);

        Assert.Contains("[REDACTED]", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-password", sanitized, StringComparison.Ordinal);
    }
}
