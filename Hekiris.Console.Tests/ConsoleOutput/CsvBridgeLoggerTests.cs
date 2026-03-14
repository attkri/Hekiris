using Hekiris.ConsoleOutput;

namespace Hekiris.Tests.ConsoleOutput;

public sealed class CsvBridgeLoggerTests
{
    [Fact]
    public void Write_CreatesDailyCsvLogWithHeaderAndFormattedSeverity()
    {
        string directory = CreateTempDirectory();

        try
        {
            CsvBridgeLogger logger = new(directory, () => new DateTime(2026, 3, 13, 17, 45, 30));

            logger.Write(BridgeLogSeverity.Warning, "Line 1; Line 2\nNew line");

            string path = Path.Combine(directory, "2026-03-13-Hekiris.csv");
            string[] lines = File.ReadAllLines(path);

            Assert.Equal("Timestamp; severity; Message", lines[0]);
            Assert.Equal("2026-03-13 17:45_30; WARNING   ; Line 1, Line 2 New line", lines[1]);
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
                logger.Write(BridgeLogSeverity.Info, $"Day {day}");
            }

            CsvBridgeLogger pruneLogger = new(directory, () => new DateTime(2026, 3, 13, 9, 0, 0));
            pruneLogger.Write(BridgeLogSeverity.Info, "Current day");

            string[] files = Directory.GetFiles(directory, "*-Hekiris.csv", SearchOption.TopDirectoryOnly);

            Assert.Equal(10, files.Length);
            Assert.DoesNotContain(files, file => file.EndsWith("2026-03-01-Hekiris.csv", StringComparison.Ordinal));
            Assert.DoesNotContain(files, file => file.EndsWith("2026-03-02-Hekiris.csv", StringComparison.Ordinal));
            Assert.DoesNotContain(files, file => file.EndsWith("2026-03-03-Hekiris.csv", StringComparison.Ordinal));
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

            writer.WriteTranscript("USER", "secret content", "Telegram message received.");

            string path = Path.Combine(directory, "2026-03-13-Hekiris.csv");
            string content = File.ReadAllText(path);

            Assert.Contains("Telegram message received.", content, StringComparison.Ordinal);
            Assert.DoesNotContain("secret content", content, StringComparison.Ordinal);
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
