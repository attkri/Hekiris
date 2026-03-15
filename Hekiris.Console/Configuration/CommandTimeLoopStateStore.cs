using System.Text.Json;
using System.Text.Json.Nodes;
using Hekiris.Application;

namespace Hekiris.Infrastructure.Configuration;

public sealed class CommandTimeLoopStateStore : ICommandTimeLoopStateStore
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
                throw new ConfigurationException($"Commands[{commandIndex + 1}] could not be found for LastRun.");
            }

            JsonObject timeLoop = GetOrCreateObject(commandObject, "timeLoop", "TimeLoop");
            SetLastRun(timeLoop, lastRun);

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

        throw new ConfigurationException($"The file {_configPath} does not contain a JSON object.");
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

    private static void SetLastRun(JsonObject timeLoop, DateTime lastRun)
    {
        string formatted = lastRun.ToString("yyyy-MM-ddTHH:mm:ss");

        if (timeLoop.ContainsKey("LastRun"))
        {
            timeLoop["LastRun"] = formatted;
            timeLoop.Remove("lastRun");
            return;
        }

        if (timeLoop.ContainsKey("lastRun"))
        {
            timeLoop["lastRun"] = formatted;
            return;
        }

        timeLoop["LastRun"] = formatted;
    }
}
