using System.Text.Json;
using System.Text.Json.Nodes;

namespace TelegramOpenCodeBridge.Configuration;

public sealed class AppConfigurationLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        AllowTrailingCommas = true,
    };

    public LoadedBridgeConfiguration Load(string? explicitConfigPath)
    {
        string basePath = ResolveConfigPath(explicitConfigPath);
        string? localConfigPath = ResolveLocalConfigPath(basePath);

        JsonObject mergedConfiguration = LoadObject(basePath);
        if (localConfigPath is not null)
        {
            MergeObjects(mergedConfiguration, LoadObject(localConfigPath));
        }

        BridgeOptions? options = mergedConfiguration.Deserialize<BridgeOptions>(SerializerOptions);
        if (options is null)
        {
            throw new ConfigurationException("Die JSON-Konfiguration konnte nicht gelesen werden.");
        }

        ApplyTelegramSecret(options, Path.GetDirectoryName(basePath)!);
        return new LoadedBridgeConfiguration(options, basePath, localConfigPath);
    }

    private static string ResolveConfigPath(string? explicitConfigPath)
    {
        string? configuredPath = explicitConfigPath;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = Environment.GetEnvironmentVariable("TOCB_CONFIG_PATH");
        }

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        }

        string fullPath = Path.GetFullPath(configuredPath);
        if (!File.Exists(fullPath))
        {
            throw new ConfigurationException($"Konfigurationsdatei nicht gefunden: {fullPath}");
        }

        return fullPath;
    }

    private static string? ResolveLocalConfigPath(string configPath)
    {
        string localPath = Path.Combine(Path.GetDirectoryName(configPath)!, "appsettings.Local.json");
        return File.Exists(localPath) ? localPath : null;
    }

    private static JsonObject LoadObject(string path)
    {
        try
        {
            JsonNode? node = JsonNode.Parse(File.ReadAllText(path));
            if (node is JsonObject jsonObject)
            {
                return jsonObject;
            }
        }
        catch (JsonException exception)
        {
            throw new ConfigurationException($"Ungültiges JSON in {path}: {exception.Message}");
        }

        throw new ConfigurationException($"Die Datei {path} enthält kein JSON-Objekt.");
    }

    private static void MergeObjects(JsonObject target, JsonObject source)
    {
        foreach ((string key, JsonNode? sourceValue) in source)
        {
            if (sourceValue is JsonObject sourceObject
                && target[key] is JsonObject targetObject)
            {
                MergeObjects(targetObject, sourceObject);
                continue;
            }

            target[key] = sourceValue?.DeepClone();
        }
    }

    private static void ApplyTelegramSecret(BridgeOptions options, string configDirectory)
    {
        string secretPath = options.Telegram.SecretSourcePath;
        if (string.IsNullOrWhiteSpace(secretPath))
        {
            return;
        }

        if (!Path.IsPathRooted(secretPath))
        {
            secretPath = Path.GetFullPath(Path.Combine(configDirectory, secretPath));
        }

        if (!File.Exists(secretPath))
        {
            return;
        }

        TelegramSecretFile? telegramSecret = JsonSerializer.Deserialize<TelegramSecretFile>(File.ReadAllText(secretPath), SerializerOptions);
        if (telegramSecret is null)
        {
            throw new ConfigurationException($"Die Telegram-Secret-Datei ist leer oder ungültig: {secretPath}");
        }

        if (string.IsNullOrWhiteSpace(options.Telegram.BotToken))
        {
            options.Telegram.BotToken = telegramSecret.AccessToken ?? string.Empty;
        }
    }

    private sealed class TelegramSecretFile
    {
        public string? AccessToken { get; set; }
    }
}
