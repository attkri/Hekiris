using TelegramOpenCodeBridge.Configuration;

namespace TelegramOpenCodeBridge.Application;

public sealed class CommandTimeLoopScheduler
{
    private readonly Func<DateTime> _nowProvider;

    public CommandTimeLoopScheduler(Func<DateTime>? nowProvider = null)
    {
        _nowProvider = nowProvider ?? (() => DateTime.Now);
    }

    public DateTime GetCurrentTime()
    {
        return _nowProvider();
    }

    public bool IsDue(ConfiguredCommandOptions command, RequestRuntimeState state)
    {
        if (state != RequestRuntimeState.Free)
        {
            return false;
        }

        CommandTimeLoopOptions? timeLoop = command.TimeLoop;
        if (timeLoop is null || !timeLoop.Enabled)
        {
            return false;
        }

        TimeSpan interval = ParseInterval(timeLoop.Interval);
        DateTime now = _nowProvider();

        if (timeLoop.LastRun is null)
        {
            return true;
        }

        return now - timeLoop.LastRun.Value >= interval;
    }

    public static TimeSpan ParseInterval(string configuredInterval)
    {
        if (string.IsNullOrWhiteSpace(configuredInterval))
        {
            throw new FormatException("Das TimeLoop-Intervall darf nicht leer sein.");
        }

        string value = configuredInterval.Trim();
        char unit = char.ToLowerInvariant(value[^1]);
        if (!int.TryParse(value[..^1], out int amount) || amount <= 0)
        {
            throw new FormatException($"Ungültiges TimeLoop-Intervall: {configuredInterval}");
        }

        return unit switch
        {
            'm' => TimeSpan.FromMinutes(amount),
            'h' => TimeSpan.FromHours(amount),
            _ => throw new FormatException($"Ungültige TimeLoop-Einheit in {configuredInterval}. Erlaubt sind m oder h."),
        };
    }
}
