namespace TelegramOpenCodeBridge.Configuration;

public static class BridgePaths
{
    public static string GetConfigDirectoryPath()
    {
        return Path.Combine(GetUserHomePath(), ".config", "TelegramOpenCodeBridge");
    }

    public static string GetConfigFilePath()
    {
        return Path.Combine(GetConfigDirectoryPath(), "config.json");
    }

    public static string GetLogDirectoryPath()
    {
        return Path.Combine(GetUserHomePath(), ".logs", "TelegramOpenCodeBridge");
    }

    private static string GetUserHomePath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
