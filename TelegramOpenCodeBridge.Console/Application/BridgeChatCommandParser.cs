namespace TelegramOpenCodeBridge.Application;

public static class BridgeChatCommandParser
{
    public static BridgeChatCommand Parse(string? text)
    {
        string normalized = text?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || !normalized.StartsWith("/", StringComparison.Ordinal))
        {
            return new BridgeChatCommand(BridgeChatCommandType.None, null);
        }

        if (string.Equals(normalized, "/help", StringComparison.OrdinalIgnoreCase))
        {
            return new BridgeChatCommand(BridgeChatCommandType.Help, null);
        }

        if (string.Equals(normalized, "/stop", StringComparison.OrdinalIgnoreCase))
        {
            return new BridgeChatCommand(BridgeChatCommandType.Stop, null);
        }

        if (string.Equals(normalized, "/ss", StringComparison.OrdinalIgnoreCase))
        {
            return new BridgeChatCommand(BridgeChatCommandType.ShowStatus, null);
        }

        if (string.Equals(normalized, "/sc", StringComparison.OrdinalIgnoreCase))
        {
            return new BridgeChatCommand(BridgeChatCommandType.ShowCommands, null);
        }

        if (normalized.Length >= 3
            && string.Equals(normalized[..2], "/c", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(normalized[2..], out int commandNumber)
            && commandNumber > 0)
        {
            return new BridgeChatCommand(BridgeChatCommandType.RunConfiguredCommand, commandNumber - 1);
        }

        return new BridgeChatCommand(BridgeChatCommandType.Unknown, null);
    }
}

public sealed record BridgeChatCommand(BridgeChatCommandType Type, int? CommandIndex);

public enum BridgeChatCommandType
{
    None,
    Help,
    Stop,
    ShowStatus,
    ShowCommands,
    RunConfiguredCommand,
    Unknown,
}
