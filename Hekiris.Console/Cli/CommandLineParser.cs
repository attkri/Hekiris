namespace Hekiris.Cli;

public static class CommandLineParser
{
    public static ParsedCommand Parse(string[] args)
    {
        List<string> tokens = new(args);

        if (tokens.Count == 0 || IsHelpToken(tokens[0]))
        {
            return new ParsedCommand(BridgeCommand.Help);
        }

        if (tokens.Count == 1 && string.Equals(tokens[0], "start", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedCommand(BridgeCommand.Start);
        }

        if (tokens.Count == 1 && string.Equals(tokens[0], "check", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedCommand(BridgeCommand.Check);
        }

        if (tokens.Count == 2
            && string.Equals(tokens[0], "config", StringComparison.OrdinalIgnoreCase)
            && string.Equals(tokens[1], "show", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedCommand(BridgeCommand.ConfigShow);
        }

        throw new CommandLineException($"Unknown command: {string.Join(' ', tokens)}");
    }

    public static string GetHelpText()
    {
        return """
Hekiris

Usage:
  Hekiris start
  Hekiris check
  Hekiris config show
  Hekiris help

Commands:
  start       Starts Hekiris and begins receiving Telegram messages.
  check       Validates configuration, Telegram access, OpenCode health, and session mappings.
  config show Shows the loaded configuration with masked secrets.
  help        Shows this help.
""";
    }

    private static bool IsHelpToken(string token)
    {
        return string.Equals(token, "help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "-h", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record ParsedCommand(BridgeCommand Command);

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
