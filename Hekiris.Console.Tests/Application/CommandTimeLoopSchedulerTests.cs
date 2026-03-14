using Hekiris.Application;
using Hekiris.Configuration;

namespace Hekiris.Tests.Application;

public sealed class CommandTimeLoopSchedulerTests
{
    [Fact]
    public void ParseInterval_ParsesMinutesAndHours()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), CommandTimeLoopScheduler.ParseInterval("5m"));
        Assert.Equal(TimeSpan.FromHours(2), CommandTimeLoopScheduler.ParseInterval("2h"));
    }

    [Fact]
    public void IsDue_ReturnsTrue_WhenEnabledAndOverdue()
    {
        CommandTimeLoopScheduler scheduler = new(() => new DateTime(2026, 3, 13, 21, 0, 0));
        ConfiguredCommandOptions command = new()
        {
            Title = "Ping",
            Session = "ses_test",
            Model = "openai/gpt-5.4",
            Prompt = "ping",
            TimeLoop = new CommandTimeLoopOptions
            {
                Enabled = true,
                Interval = "5m",
                LastRun = new DateTime(2026, 3, 13, 20, 30, 0),
            },
        };

        bool due = scheduler.IsDue(command, RequestRuntimeState.Free);

        Assert.True(due);
    }

    [Fact]
    public void IsDue_ReturnsFalse_WhenCommandIsRunning()
    {
        CommandTimeLoopScheduler scheduler = new(() => new DateTime(2026, 3, 13, 21, 0, 0));
        ConfiguredCommandOptions command = new()
        {
            TimeLoop = new CommandTimeLoopOptions
            {
                Enabled = true,
                Interval = "5m",
                LastRun = new DateTime(2026, 3, 13, 20, 0, 0),
            },
        };

        bool due = scheduler.IsDue(command, RequestRuntimeState.Running);

        Assert.False(due);
    }
}
