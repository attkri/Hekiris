using System.Text;
using Hekiris.Application;
using Hekiris.Infrastructure.Configuration;

namespace Hekiris.Infrastructure.Logging;

public sealed class CsvBridgeLogger
{
    private const string Header = "Timestamp; severity; Message";
    private readonly object _sync = new();
    private readonly string _directoryPath;
    private readonly Func<DateTime> _nowProvider;

    public CsvBridgeLogger(string? directoryPath = null, Func<DateTime>? nowProvider = null)
    {
        _directoryPath = directoryPath ?? BridgePaths.GetLogDirectoryPath();
        _nowProvider = nowProvider ?? (() => DateTime.Now);
        Directory.CreateDirectory(_directoryPath);
    }

    public void Write(BridgeLogSeverity severity, string message)
    {
        lock (_sync)
        {
            DateTime now = _nowProvider();
            string filePath = Path.Combine(_directoryPath, $"{now:yyyy-MM-dd}-Hekiris.csv");
            bool writeHeader = !File.Exists(filePath) || new FileInfo(filePath).Length == 0;

            using StreamWriter writer = new(filePath, append: true, Encoding.UTF8);
            if (writeHeader)
            {
                writer.WriteLine(Header);
            }

            writer.WriteLine($"{now:yyyy-MM-dd HH:mm_ss}; {FormatSeverity(severity)}; {Sanitize(message)}");
            writer.Flush();
            PruneLogFiles();
        }
    }

    private void PruneLogFiles()
    {
        DateTime cutoffDate = _nowProvider().Date.AddDays(-9);
        IEnumerable<string> staleFiles = Directory
            .GetFiles(_directoryPath, "*-Hekiris.csv", SearchOption.TopDirectoryOnly)
            .Where(filePath => ShouldDelete(filePath, cutoffDate));

        foreach (string staleFile in staleFiles)
        {
            File.Delete(staleFile);
        }
    }

    private static bool ShouldDelete(string filePath, DateTime cutoffDate)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        if (fileName.Length < 10)
        {
            return false;
        }

        bool parsed = DateTime.TryParseExact(
            fileName[..10],
            "yyyy-MM-dd",
            provider: null,
            System.Globalization.DateTimeStyles.None,
            out DateTime fileDate);

        return parsed && fileDate.Date < cutoffDate;
    }

    private static string FormatSeverity(BridgeLogSeverity severity)
    {
        string value = severity switch
        {
            BridgeLogSeverity.Info => "INFO",
            BridgeLogSeverity.Warning => "WARNING",
            BridgeLogSeverity.Error => "ERROR",
            _ => "INFO",
        };

        return value.PadRight(10, ' ');
    }

    private static string Sanitize(string message)
    {
        return message
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace(";", ",", StringComparison.Ordinal)
            .Trim();
    }
}
