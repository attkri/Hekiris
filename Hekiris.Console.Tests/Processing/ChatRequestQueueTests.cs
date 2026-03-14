using Hekiris.Application;

namespace Hekiris.Tests.Processing;

public sealed class ChatRequestQueueTests
{
    [Fact]
    public async Task EnqueueAsync_ProcessesMessagesSequentiallyPerChat()
    {
        List<string> order = new();
        int concurrentExecutions = 0;
        int maxConcurrency = 0;

        ChatRequestQueue queue = new(
            10,
            async (request, _) =>
            {
                int current = Interlocked.Increment(ref concurrentExecutions);
                maxConcurrency = Math.Max(maxConcurrency, current);
                order.Add(request.Text);
                await Task.Delay(50);
                Interlocked.Decrement(ref concurrentExecutions);
            },
            (_, _, _) => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            true);

        await queue.EnqueueAsync(new ChatRequest(1, new[] { 1L }, 1, "user", "eins", "ses_a", null, null, null, null, false), CancellationToken.None);
        await queue.EnqueueAsync(new ChatRequest(1, new[] { 1L }, 1, "user", "zwei", "ses_a", null, null, null, null, false), CancellationToken.None);
        await Task.Delay(150);
        await queue.BeginShutdownAsync(CancellationToken.None);

        Assert.Equal(new[] { "eins", "zwei" }, order);
        Assert.Equal(1, maxConcurrency);
    }

    [Fact]
    public async Task BeginShutdownAsync_AbortsActiveRequest_AndRejectsQueuedRequests()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource cancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> rejections = new();
        List<string> abortedSessions = new();

        ChatRequestQueue queue = new(
            10,
            async (request, cancellationToken) =>
            {
                started.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    cancellationObserved.TrySetResult();
                }
            },
            (request, reason, _) =>
            {
                rejections.Add($"{request.Text}:{reason}");
                return Task.CompletedTask;
            },
            (request, _) =>
            {
                abortedSessions.Add(request.OpenCodeSessionId);
                return Task.CompletedTask;
            },
            true);

        await queue.EnqueueAsync(new ChatRequest(1, new[] { 1L }, 1, "user", "laufend", "ses_active", null, null, null, null, false), CancellationToken.None);
        await started.Task;
        await queue.EnqueueAsync(new ChatRequest(1, new[] { 1L }, 1, "user", "wartend", "ses_active", null, null, null, null, false), CancellationToken.None);

        Task shutdownTask = queue.BeginShutdownAsync(CancellationToken.None);
        await cancellationObserved.Task;
        await shutdownTask;
        await queue.EnqueueAsync(new ChatRequest(1, new[] { 1L }, 1, "user", "neu", "ses_active", null, null, null, null, false), CancellationToken.None);

        Assert.Contains("ses_active", abortedSessions);
        Assert.Contains(rejections, entry => entry.StartsWith("wartend:", StringComparison.Ordinal) || entry.StartsWith("queued:", StringComparison.Ordinal));
        Assert.Contains(rejections, entry => entry.StartsWith("neu:", StringComparison.Ordinal) || entry.StartsWith("new:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetChatRuntimeStatus_ReportsBaseAndCommandStates()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        ChatRequestQueue queue = new(
            10,
            async (_, _) =>
            {
                started.TrySetResult();
                await release.Task;
            },
            (_, _, _) => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            true);

        await queue.EnqueueAsync(new ChatRequest(1, new[] { 1L }, 1, "user", "kommando", "ses_cmd", null, null, "Titel", 1, false), CancellationToken.None);
        await started.Task;
        await queue.EnqueueAsync(new ChatRequest(1, new[] { 1L }, 1, "user", "basis", "ses_base", null, null, null, null, false), CancellationToken.None);

        ChatRuntimeStatusSnapshot status = queue.GetChatRuntimeStatus(1, 2);

        Assert.Equal(RequestRuntimeState.Running, status.BaseSessionState);
        Assert.Equal(RequestRuntimeState.Running, status.CommandStates[1]);
        Assert.Equal(RequestRuntimeState.Free, status.CommandStates[2]);

        release.TrySetResult();
        await queue.BeginShutdownAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TryAbortActiveConfiguredCommandAsync_AbortsMatchingCommand()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource cancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> abortedSessions = new();

        ChatRequestQueue queue = new(
            10,
            async (_, cancellationToken) =>
            {
                started.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    cancellationObserved.TrySetResult();
                }
            },
            (_, _, _) => Task.CompletedTask,
            (request, _) =>
            {
                abortedSessions.Add(request.OpenCodeSessionId);
                return Task.CompletedTask;
            },
            true);

        await queue.EnqueueAsync(new ChatRequest(1, new[] { 1L }, 1, "user", "kommando", "ses_cmd", null, null, "Titel", 1, false), CancellationToken.None);
        await started.Task;

        bool stopped = await queue.TryAbortActiveConfiguredCommandAsync(1, CancellationToken.None);

        await cancellationObserved.Task;
        Assert.True(stopped);
        Assert.Contains("ses_cmd", abortedSessions);

        await queue.BeginShutdownAsync(CancellationToken.None);
    }
}
