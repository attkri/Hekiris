using System.Text.Json;

namespace TelegramOpenCodeBridge.Configuration;

public static class ConfigMasker
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static string ToSanitizedJson(BridgeOptions options)
    {
        BridgeOptions copy = DeepCopy(options);
        copy.Telegram.BotToken = Mask(copy.Telegram.BotToken);
        copy.OpenCode.Password = Mask(copy.OpenCode.Password);
        return JsonSerializer.Serialize(copy, SerializerOptions);
    }

    private static BridgeOptions DeepCopy(BridgeOptions options)
    {
        return JsonSerializer.Deserialize<BridgeOptions>(JsonSerializer.Serialize(options, SerializerOptions), SerializerOptions)
            ?? new BridgeOptions();
    }

    private static string Mask(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : "[REDACTED]";
    }
}
