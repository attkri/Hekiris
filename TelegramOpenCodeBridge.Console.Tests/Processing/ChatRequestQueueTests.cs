using TelegramOpenCodeBridge.Application;

namespace TelegramOpenCodeBridge.Tests.Processing;

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

        await queue.EnqueueAsync(new ChatRequest(1, 1, "user", "eins", "ses_a", null, null, null), CancellationToken.None);
        await queue.EnqueueAsync(new ChatRequest(1, 1, "user", "zwei", "ses_a", null, null, null), CancellationToken.None);
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

        await queue.EnqueueAsync(new ChatRequest(1, 1, "user", "laufend", "ses_active", null, null, null), CancellationToken.None);
        await started.Task;
        await queue.EnqueueAsync(new ChatRequest(1, 1, "user", "wartend", "ses_active", null, null, null), CancellationToken.None);

        Task shutdownTask = queue.BeginShutdownAsync(CancellationToken.None);
        await cancellationObserved.Task;
        await shutdownTask;
        await queue.EnqueueAsync(new ChatRequest(1, 1, "user", "neu", "ses_active", null, null, null), CancellationToken.None);

        Assert.Contains("ses_active", abortedSessions);
        Assert.Contains(rejections, entry => entry.StartsWith("wartend:", StringComparison.Ordinal));
        Assert.Contains(rejections, entry => entry.StartsWith("neu:", StringComparison.Ordinal));
    }
}
