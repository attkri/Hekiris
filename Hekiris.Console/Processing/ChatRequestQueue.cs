using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Hekiris.Application;

public sealed class ChatRequestQueue
{
    private readonly int _queueCapacityPerChat;
    private readonly Func<ChatRequest, CancellationToken, Task> _processor;
    private readonly Func<ChatRequest, string, CancellationToken, Task> _rejector;
    private readonly Func<ChatRequest, CancellationToken, Task> _aborter;
    private readonly bool _notifyWhenStopping;
    private readonly ConcurrentDictionary<string, ChatQueueState> _queues = new();
    private readonly ConcurrentDictionary<string, ActiveChatRequest> _activeRequests = new();
    private readonly ConcurrentDictionary<string, int> _pendingCounts = new();
    private int _shuttingDown;

    public ChatRequestQueue(
        int queueCapacityPerChat,
        Func<ChatRequest, CancellationToken, Task> processor,
        Func<ChatRequest, string, CancellationToken, Task> rejector,
        Func<ChatRequest, CancellationToken, Task> aborter,
        bool notifyWhenStopping)
    {
        _queueCapacityPerChat = queueCapacityPerChat;
        _processor = processor;
        _rejector = rejector;
        _aborter = aborter;
        _notifyWhenStopping = notifyWhenStopping;
    }

    public async Task<bool> EnqueueAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        if (IsShuttingDown)
        {
            if (_notifyWhenStopping)
            {
                await _rejector(request, "Hekiris wird gerade beendet. Neue Nachrichten werden aktuell nicht mehr angenommen.", cancellationToken);
            }

            return false;
        }

        string queueKey = GetQueueKey(request);
        ChatQueueState queue = _queues.GetOrAdd(queueKey, CreateQueueState);
        if (!queue.Writer.TryWrite(request))
        {
            await _rejector(request, "Die Warteschlange für diesen Chat ist voll. Bitte versuche es gleich erneut.", cancellationToken);
            return false;
        }

        IncrementPending(request);
        return true;
    }

    public async Task BeginShutdownAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _shuttingDown, 1) == 1)
        {
            return;
        }

        foreach (ChatQueueState queue in _queues.Values)
        {
            queue.Writer.TryComplete();
        }

        foreach (ActiveChatRequest activeRequest in _activeRequests.Values)
        {
            try
            {
                activeRequest.CancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            await _aborter(activeRequest.Request, cancellationToken);
        }

        await Task.WhenAll(_queues.Values.Select(queue => queue.Worker));
    }

    public async Task<bool> TryAbortActiveConfiguredCommandAsync(int commandNumber, CancellationToken cancellationToken)
    {
        if (!_activeRequests.TryGetValue(GetCommandQueueKey(commandNumber), out ActiveChatRequest? activeRequest))
        {
            return false;
        }

        if (activeRequest.Request.ConfiguredCommandNumber != commandNumber)
        {
            return false;
        }

        activeRequest.CancellationTokenSource.Cancel();
        await _aborter(activeRequest.Request, cancellationToken);
        return true;
    }

    public ChatRuntimeStatusSnapshot GetChatRuntimeStatus(long chatId, int configuredCommandCount)
    {
        _activeRequests.TryGetValue(GetBaseQueueKey(chatId), out ActiveChatRequest? baseActiveRequest);

        Dictionary<int, RequestRuntimeState> commandStates = new();
        for (int commandNumber = 1; commandNumber <= configuredCommandCount; commandNumber++)
        {
            _activeRequests.TryGetValue(GetCommandQueueKey(commandNumber), out ActiveChatRequest? commandActiveRequest);
            bool isRunning = commandActiveRequest is not null;
            int pendingCount = GetPendingCount(GetCommandQueueKey(commandNumber));
            commandStates[commandNumber] = ToRuntimeState(isRunning, pendingCount);
        }

        bool baseRunning = baseActiveRequest is not null;
        int pendingBaseCount = GetPendingCount(GetBaseQueueKey(chatId));

        ChatRequest? activeRequest = baseActiveRequest?.Request
            ?? _activeRequests.Values
                .Select(item => item.Request)
                .FirstOrDefault(item => item.ResponseChatIds.Contains(chatId));

        return new ChatRuntimeStatusSnapshot(
            ToRuntimeState(baseRunning, pendingBaseCount),
            activeRequest,
            commandStates);
    }

    public RequestRuntimeState GetCommandRuntimeState(int commandNumber)
    {
        bool isRunning = _activeRequests.ContainsKey(GetCommandQueueKey(commandNumber));
        int pendingCount = GetPendingCount(GetCommandQueueKey(commandNumber));
        return ToRuntimeState(isRunning, pendingCount);
    }

    private bool IsShuttingDown => Volatile.Read(ref _shuttingDown) == 1;

    private ChatQueueState CreateQueueState(string queueKey)
    {
        Channel<ChatRequest> channel = Channel.CreateBounded<ChatRequest>(new BoundedChannelOptions(_queueCapacityPerChat)
        {
            SingleReader = true,
            SingleWriter = false,
        });

        Task worker = Task.Run(() => ProcessQueueAsync(queueKey, channel.Reader));
        return new ChatQueueState(channel.Writer, worker);
    }

    private async Task ProcessQueueAsync(string queueKey, ChannelReader<ChatRequest> reader)
    {
        await foreach (ChatRequest request in reader.ReadAllAsync())
        {
            if (IsShuttingDown)
            {
                if (_notifyWhenStopping)
                {
                    await _rejector(request, "Hekiris wird gerade beendet. Diese Nachricht wurde nicht mehr verarbeitet.", CancellationToken.None);
                }

                continue;
            }

            DecrementPending(request);
            using CancellationTokenSource requestCancellationTokenSource = new();
            _activeRequests[queueKey] = new ActiveChatRequest(request, requestCancellationTokenSource);
            try
            {
                await _processor(request, requestCancellationTokenSource.Token);
            }
            finally
            {
                _activeRequests.TryRemove(queueKey, out _);
            }
        }
    }

    private sealed record ChatQueueState(ChannelWriter<ChatRequest> Writer, Task Worker);

    private sealed record ActiveChatRequest(ChatRequest Request, CancellationTokenSource CancellationTokenSource);

    private void IncrementPending(ChatRequest request)
    {
        string key = GetQueueKey(request);
        _pendingCounts.AddOrUpdate(key, 1, static (_, current) => current + 1);
    }

    private void DecrementPending(ChatRequest request)
    {
        string key = GetQueueKey(request);
        while (true)
        {
            if (!_pendingCounts.TryGetValue(key, out int current))
            {
                return;
            }

            if (current <= 1)
            {
                if (_pendingCounts.TryRemove(key, out _))
                {
                    return;
                }

                continue;
            }

            if (_pendingCounts.TryUpdate(key, current - 1, current))
            {
                return;
            }
        }
    }

    private int GetPendingCount(string queueKey)
    {
        return _pendingCounts.TryGetValue(queueKey, out int count) ? count : 0;
    }

    private static string GetQueueKey(ChatRequest request)
    {
        return request.ConfiguredCommandNumber is null
            ? GetBaseQueueKey(request.ChatId)
            : GetCommandQueueKey(request.ConfiguredCommandNumber.Value);
    }

    private static string GetBaseQueueKey(long chatId)
    {
        return $"base:{chatId}";
    }

    private static string GetCommandQueueKey(int commandNumber)
    {
        return $"command:{commandNumber}";
    }

    private static RequestRuntimeState ToRuntimeState(bool isRunning, int pendingCount)
    {
        if (isRunning)
        {
            return RequestRuntimeState.Running;
        }

        return pendingCount > 0 ? RequestRuntimeState.Queued : RequestRuntimeState.Free;
    }
}

public sealed record ChatRequest(
    long ChatId,
    IReadOnlyList<long> ResponseChatIds,
    long? UserId,
    string? Username,
    string Text,
    string OpenCodeSessionId,
    string? ConfiguredModel,
    string? ConfiguredCommandTitle,
    int? ConfiguredCommandNumber,
    bool IsAutomatic = false);

public sealed record ChatRuntimeStatusSnapshot(
    RequestRuntimeState BaseSessionState,
    ChatRequest? ActiveRequest,
    IReadOnlyDictionary<int, RequestRuntimeState> CommandStates);

public enum RequestRuntimeState
{
    Free,
    Running,
    Queued,
}
