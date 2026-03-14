namespace Hekiris.ConsoleOutput;

public sealed class ConsoleTranscriptWriter
{
    private readonly object _sync = new();
    private readonly CsvBridgeLogger _csvLogger;

    public ConsoleTranscriptWriter()
        : this(new CsvBridgeLogger())
    {
    }

    public ConsoleTranscriptWriter(CsvBridgeLogger csvLogger)
    {
        _csvLogger = csvLogger;
    }

    public void WriteTranscript(string role, string text, string? logMessage = null, BridgeLogSeverity severity = BridgeLogSeverity.Info)
    {
        lock (_sync)
        {
            System.Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {role}:");
            System.Console.WriteLine(text);
            System.Console.WriteLine();
            _csvLogger.Write(severity, logMessage ?? $"Transkript-Ausgabe für Rolle {role}.");
        }
    }

    public void WriteInfo(string message)
    {
        lock (_sync)
        {
            System.Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] BRIDGE:");
            System.Console.WriteLine(message);
            System.Console.WriteLine();
            _csvLogger.Write(BridgeLogSeverity.Info, message);
        }
    }

    public void WriteWarning(string message)
    {
        lock (_sync)
        {
            System.Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] WARNING:");
            System.Console.WriteLine(message);
            System.Console.WriteLine();
            _csvLogger.Write(BridgeLogSeverity.Warning, message);
        }
    }

    public void WriteError(string message)
    {
        lock (_sync)
        {
            System.Console.Error.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR:");
            System.Console.Error.WriteLine(message);
            System.Console.Error.WriteLine();
            _csvLogger.Write(BridgeLogSeverity.Error, message);
        }
    }
}
