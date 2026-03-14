using Hekiris.ConsoleOutput;

namespace Hekiris.Application;

public sealed class OpenCodeAvailabilityTracker
{
    private readonly Func<string, CancellationToken, Task> _notificationSender;
    private readonly Action<string> _infoLogger;
    private readonly Action<string> _warningLogger;
    private int _availabilityState = 1;

    public OpenCodeAvailabilityTracker(
        Func<string, CancellationToken, Task> notificationSender,
        Action<string> infoLogger,
        Action<string> warningLogger)
    {
        _notificationSender = notificationSender;
        _infoLogger = infoLogger;
        _warningLogger = warningLogger;
    }

    public async Task ReportAvailableAsync(CancellationToken cancellationToken)
    {
        int previousState = Interlocked.Exchange(ref _availabilityState, 1);
        if (previousState == 1)
        {
            return;
        }

        const string message = "OpenCode server is reachable again.";
        _infoLogger(message);
        await _notificationSender(message, cancellationToken);
    }

    public void SetAvailability(bool isAvailable)
    {
        Interlocked.Exchange(ref _availabilityState, isAvailable ? 1 : 0);
    }

    public bool IsAvailable => Volatile.Read(ref _availabilityState) == 1;

    public async Task ReportUnavailableAsync(string reason, CancellationToken cancellationToken)
    {
        int previousState = Interlocked.Exchange(ref _availabilityState, 0);
        if (previousState == 0)
        {
            return;
        }

        string logMessage = string.IsNullOrWhiteSpace(reason)
            ? "OpenCode server is currently unreachable."
            : $"OpenCode server is currently unreachable: {reason}";

        const string notificationMessage = "Warning: the OpenCode server is currently unreachable. New requests may fail.";

        _warningLogger(logMessage);
        await _notificationSender(notificationMessage, cancellationToken);
    }
}
