using TelegramOpenCodeBridge.Application;

namespace TelegramOpenCodeBridge.Tests.Application;

public sealed class OpenCodeAvailabilityTrackerTests
{
    [Fact]
    public async Task ReportUnavailableAsync_SendsNotificationOnlyOnStateChange()
    {
        List<string> notifications = new();
        List<string> warnings = new();

        OpenCodeAvailabilityTracker tracker = new(
            (message, _) =>
            {
                notifications.Add(message);
                return Task.CompletedTask;
            },
            _ => { },
            message => warnings.Add(message));

        await tracker.ReportUnavailableAsync("offline", CancellationToken.None);
        await tracker.ReportUnavailableAsync("offline", CancellationToken.None);

        Assert.Single(notifications);
        Assert.Single(warnings);
    }

    [Fact]
    public async Task ReportAvailableAsync_SendsRecoveryNotificationAfterOutage()
    {
        List<string> notifications = new();
        List<string> infos = new();

        OpenCodeAvailabilityTracker tracker = new(
            (message, _) =>
            {
                notifications.Add(message);
                return Task.CompletedTask;
            },
            message => infos.Add(message),
            _ => { });

        await tracker.ReportUnavailableAsync("offline", CancellationToken.None);
        await tracker.ReportAvailableAsync(CancellationToken.None);

        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, message => message.Contains("wieder erreichbar", StringComparison.Ordinal));
        Assert.Single(infos);
    }
}
