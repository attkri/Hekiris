using TelegramOpenCodeBridge.ConsoleOutput;

namespace TelegramOpenCodeBridge.Tests.ConsoleOutput;

public sealed class CsvBridgeLoggerTests
{
    [Fact]
    public void Write_CreatesDailyCsvLogWithHeaderAndFormattedSeverity()
    {
        string directory = CreateTempDirectory();

        try
        {
            CsvBridgeLogger logger = new(directory, () => new DateTime(2026, 3, 13, 17, 45, 30));

            logger.Write(BridgeLogSeverity.Warning, "Zeile 1; Zeile 2\nNeue Zeile");

            string path = Path.Combine(directory, "2026-03-13-OCBridge.csv");
            string[] lines = File.ReadAllLines(path);

            Assert.Equal("Timestamp; severity; Message", lines[0]);
            Assert.Equal("2026-03-13 17:45_30; WARNING   ; Zeile 1, Zeile 2 Neue Zeile", lines[1]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Write_DeletesFilesOlderThanTenDays()
    {
        string directory = CreateTempDirectory();

        try
        {
            for (int day = 1; day <= 13; day++)
            {
                DateTime current = new(2026, 3, day, 8, 0, 0);
                CsvBridgeLogger logger = new(directory, () => current);
                logger.Write(BridgeLogSeverity.Info, $"Tag {day}");
            }

            CsvBridgeLogger pruneLogger = new(directory, () => new DateTime(2026, 3, 13, 9, 0, 0));
            pruneLogger.Write(BridgeLogSeverity.Info, "Aktueller Tag");

            string[] files = Directory.GetFiles(directory, "*-OCBridge.csv", SearchOption.TopDirectoryOnly);

            Assert.Equal(10, files.Length);
            Assert.DoesNotContain(files, file => file.EndsWith("2026-03-01-OCBridge.csv", StringComparison.Ordinal));
            Assert.DoesNotContain(files, file => file.EndsWith("2026-03-02-OCBridge.csv", StringComparison.Ordinal));
            Assert.DoesNotContain(files, file => file.EndsWith("2026-03-03-OCBridge.csv", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ConsoleTranscriptWriter_LogsOnlySanitizedMessage()
    {
        string directory = CreateTempDirectory();

        try
        {
            CsvBridgeLogger logger = new(directory, () => new DateTime(2026, 3, 13, 18, 0, 0));
            ConsoleTranscriptWriter writer = new(logger);

            writer.WriteTranscript("USER", "geheimer inhalt", "Telegram-Nachricht eingegangen.");

            string path = Path.Combine(directory, "2026-03-13-OCBridge.csv");
            string content = File.ReadAllText(path);

            Assert.Contains("Telegram-Nachricht eingegangen.", content, StringComparison.Ordinal);
            Assert.DoesNotContain("geheimer inhalt", content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ocbridge-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
