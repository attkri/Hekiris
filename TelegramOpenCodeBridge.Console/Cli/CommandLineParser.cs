namespace TelegramOpenCodeBridge.Cli;

public static class CommandLineParser
{
    public static ParsedCommand Parse(string[] args)
    {
        List<string> tokens = new(args);
        string? configPath = null;

        for (int index = 0; index < tokens.Count;)
        {
            if (!string.Equals(tokens[index], "--config", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            if (index + 1 >= tokens.Count)
            {
                throw new CommandLineException("Der Parameter --config erwartet einen Dateipfad.");
            }

            configPath = tokens[index + 1];
            tokens.RemoveAt(index + 1);
            tokens.RemoveAt(index);
        }

        if (tokens.Count == 0 || IsHelpToken(tokens[0]))
        {
            return new ParsedCommand(BridgeCommand.Help, configPath);
        }

        if (tokens.Count == 1 && string.Equals(tokens[0], "start", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedCommand(BridgeCommand.Start, configPath);
        }

        if (tokens.Count == 1 && string.Equals(tokens[0], "check", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedCommand(BridgeCommand.Check, configPath);
        }

        if (tokens.Count == 2
            && string.Equals(tokens[0], "config", StringComparison.OrdinalIgnoreCase)
            && string.Equals(tokens[1], "show", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedCommand(BridgeCommand.ConfigShow, configPath);
        }

        throw new CommandLineException($"Unbekanntes Kommando: {string.Join(' ', tokens)}");
    }

    public static string GetHelpText()
    {
        return """
tocb - Telegram OpenCode Bridge

Verwendung:
  tocb [--config <pfad>] start
  tocb [--config <pfad>] check
  tocb [--config <pfad>] config show
  tocb help

Kommandos:
  start       Startet die Bridge und beginnt mit dem Empfang von Telegram-Nachrichten.
  check       Prüft Konfiguration, Telegram-Zugriff, OpenCode-Health und Session-Mapping.
  config show Zeigt die geladene Konfiguration mit maskierten Secrets an.
  help        Zeigt diese Hilfe an.
""";
    }

    private static bool IsHelpToken(string token)
    {
        return string.Equals(token, "help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "-h", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record ParsedCommand(BridgeCommand Command, string? ConfigPath);

public enum BridgeCommand
{
    Help,
    Start,
    Check,
    ConfigShow,
}

public sealed class CommandLineException : Exception
{
    public CommandLineException(string message)
        : base(message)
    {
    }
}
