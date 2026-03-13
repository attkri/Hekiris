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

    public LoadedBridgeConfiguration Load()
    {
        string basePath = ResolveConfigPath();
        JsonObject mergedConfiguration = LoadObject(basePath);

        BridgeOptions? options = mergedConfiguration.Deserialize<BridgeOptions>(SerializerOptions);
        if (options is null)
        {
            throw new ConfigurationException("Die JSON-Konfiguration konnte nicht gelesen werden.");
        }

        ApplyTelegramSecret(options, Path.GetDirectoryName(basePath)!);
        return new LoadedBridgeConfiguration(options, basePath);
    }

    private static string ResolveConfigPath()
    {
        string fullPath = BridgePaths.GetConfigFilePath();
        if (!File.Exists(fullPath))
        {
            throw new ConfigurationException($"Konfigurationsdatei nicht gefunden: {fullPath}");
        }

        return fullPath;
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
