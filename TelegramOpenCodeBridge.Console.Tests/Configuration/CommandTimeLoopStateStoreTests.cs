using System.Text.Json.Nodes;
using TelegramOpenCodeBridge.Configuration;

namespace TelegramOpenCodeBridge.Tests.Configuration;

public sealed class CommandTimeLoopStateStoreTests
{
    [Fact]
    public async Task UpdateLastRunAsync_UpdatesPascalCaseKey_AndRemovesLowercaseDuplicate()
    {
        string directory = CreateTempDirectory();
        string configPath = Path.Combine(directory, "config.json");

        try
        {
            await File.WriteAllTextAsync(
                configPath,
                """
                {
                  "Commands": [
                    {
                      "Title": "Ping Test",
                      "TimeLoop": {
                        "Enabled": true,
                        "Interval": "1h",
                        "LastRun": "2026-03-13T21:06:49",
                        "lastRun": "2026-03-13T23:27:00"
                      }
                    }
                  ]
                }
                """);

            CommandTimeLoopStateStore store = new(configPath);

            await store.UpdateLastRunAsync(0, new DateTime(2026, 3, 14, 8, 15, 0), CancellationToken.None);

            JsonObject root = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!.AsObject();
            JsonObject timeLoop = root["Commands"]![0]!["TimeLoop"]!.AsObject();

            Assert.Equal("2026-03-14T08:15:00", timeLoop["LastRun"]!.GetValue<string>());
            Assert.False(timeLoop.ContainsKey("lastRun"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ocbridge-store-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
