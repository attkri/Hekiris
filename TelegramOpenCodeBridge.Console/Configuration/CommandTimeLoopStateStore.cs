using System.Text.Json;
using System.Text.Json.Nodes;

namespace TelegramOpenCodeBridge.Configuration;

public sealed class CommandTimeLoopStateStore
{
    private readonly string _configPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CommandTimeLoopStateStore(string? configPath = null)
    {
        _configPath = configPath ?? BridgePaths.GetConfigFilePath();
    }

    public async Task UpdateLastRunAsync(int commandIndex, DateTime lastRun, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            JsonObject root = LoadRoot();
            JsonArray commands = GetOrCreateArray(root, "commands", "Commands");

            if (commandIndex < 0 || commandIndex >= commands.Count || commands[commandIndex] is not JsonObject commandObject)
            {
                throw new ConfigurationException($"Commands[{commandIndex + 1}] konnte für LastRun nicht gefunden werden.");
            }

            JsonObject timeLoop = GetOrCreateObject(commandObject, "timeLoop", "TimeLoop");
            timeLoop["lastRun"] = lastRun.ToString("yyyy-MM-ddTHH:mm:ss");

            JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
            {
                WriteIndented = true,
            };

            await File.WriteAllTextAsync(_configPath, root.ToJsonString(options), cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private JsonObject LoadRoot()
    {
        JsonNode? node = JsonNode.Parse(File.ReadAllText(_configPath));
        if (node is JsonObject root)
        {
            return root;
        }

        throw new ConfigurationException($"Die Datei {_configPath} enthält kein JSON-Objekt.");
    }

    private static JsonArray GetOrCreateArray(JsonObject parent, params string[] candidateNames)
    {
        foreach (string candidateName in candidateNames)
        {
            if (parent[candidateName] is JsonArray existingArray)
            {
                return existingArray;
            }
        }

        JsonArray created = new();
        parent[candidateNames[0]] = created;
        return created;
    }

    private static JsonObject GetOrCreateObject(JsonObject parent, params string[] candidateNames)
    {
        foreach (string candidateName in candidateNames)
        {
            if (parent[candidateName] is JsonObject existingObject)
            {
                return existingObject;
            }
        }

        JsonObject created = new();
        parent[candidateNames[0]] = created;
        return created;
    }
}
