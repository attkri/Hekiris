using System.Reflection;

namespace Hekiris.Application;

public static class BridgeVersion
{
    public static string GetDisplayVersion()
    {
        Assembly assembly = typeof(BridgeVersion).Assembly;
        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        string? version = informationalVersion;
        int plusIndex = version?.IndexOf('+') ?? -1;
        if (plusIndex >= 0)
        {
            version = version![..plusIndex];
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            version = assembly.GetName().Version?.ToString(3);
        }

        return string.IsNullOrWhiteSpace(version) ? "0.0.0" : version;
    }
}
