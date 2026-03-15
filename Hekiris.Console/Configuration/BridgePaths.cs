namespace Hekiris.Infrastructure.Configuration;

public static class BridgePaths
{
    public static string GetConfigDirectoryPath()
    {
        return Path.Combine(GetUserHomePath(), ".config", "Hekiris");
    }

    public static string GetConfigFilePath()
    {
        return Path.Combine(GetConfigDirectoryPath(), "config.json");
    }

    public static string GetLogDirectoryPath()
    {
        return Path.Combine(GetUserHomePath(), ".logs", "Hekiris");
    }

    private static string GetUserHomePath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
