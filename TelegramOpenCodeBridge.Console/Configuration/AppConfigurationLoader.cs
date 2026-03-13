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

    private readonly string? _configPathOverride;

    public AppConfigurationLoader(string? configPathOverride = null)
    {
        _configPathOverride = configPathOverride;
    }

    public LoadedBridgeConfiguration Load()
    {
        string basePath = ResolveConfigPath(_configPathOverride);
        JsonObject mergedConfiguration = LoadObject(basePath);
        NormalizeAccessControlCollections(mergedConfiguration);

        BridgeOptions? options = mergedConfiguration.Deserialize<BridgeOptions>(SerializerOptions);
        if (options is null)
        {
            throw new ConfigurationException("Die JSON-Konfiguration konnte nicht gelesen werden.");
        }

        ApplyTelegramSecret(options, Path.GetDirectoryName(basePath)!);
        return new LoadedBridgeConfiguration(options, basePath);
    }

    private static string ResolveConfigPath(string? configPathOverride)
    {
        string fullPath = string.IsNullOrWhiteSpace(configPathOverride)
            ? BridgePaths.GetConfigFilePath()
            : Path.GetFullPath(configPathOverride);
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

    private static void NormalizeAccessControlCollections(JsonObject root)
    {
        NormalizeAllowedValuesOnObject(GetObject(root, "accessControl", "AccessControl"));

        JsonArray? chats = GetArray(root, "chats", "Chats");
        if (chats is null)
        {
            return;
        }

        foreach (JsonNode? node in chats)
        {
            if (node is JsonObject chatObject)
            {
                NormalizeAllowedValuesOnObject(chatObject);
            }
        }
    }

    private static void NormalizeAllowedValuesOnObject(JsonObject? jsonObject)
    {
        if (jsonObject is null)
        {
            return;
        }

        NormalizeToLongArray(jsonObject, "allowedUserIds", "AllowedUserIds");
        NormalizeToStringArray(jsonObject, "allowedUsernames", "AllowedUsernames");
    }

    private static void NormalizeToLongArray(JsonObject jsonObject, params string[] candidateNames)
    {
        foreach (string candidateName in candidateNames)
        {
            if (jsonObject[candidateName] is not JsonValue jsonValue)
            {
                continue;
            }

            if (jsonValue.TryGetValue<long>(out long longValue))
            {
                jsonObject[candidateName] = new JsonArray(longValue);
                return;
            }

            if (jsonValue.TryGetValue<string>(out string? stringValue)
                && long.TryParse(stringValue, out long parsedValue))
            {
                jsonObject[candidateName] = new JsonArray(parsedValue);
                return;
            }
        }
    }

    private static void NormalizeToStringArray(JsonObject jsonObject, params string[] candidateNames)
    {
        foreach (string candidateName in candidateNames)
        {
            if (jsonObject[candidateName] is JsonValue jsonValue
                && jsonValue.TryGetValue<string>(out string? stringValue)
                && !string.IsNullOrWhiteSpace(stringValue))
            {
                jsonObject[candidateName] = new JsonArray(stringValue);
                return;
            }
        }
    }

    private static JsonObject? GetObject(JsonObject root, params string[] candidateNames)
    {
        foreach (string candidateName in candidateNames)
        {
            if (root[candidateName] is JsonObject jsonObject)
            {
                return jsonObject;
            }
        }

        return null;
    }

    private static JsonArray? GetArray(JsonObject root, params string[] candidateNames)
    {
        foreach (string candidateName in candidateNames)
        {
            if (root[candidateName] is JsonArray jsonArray)
            {
                return jsonArray;
            }
        }

        return null;
    }
}
