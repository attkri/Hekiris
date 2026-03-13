using System.Collections.Concurrent;
using System.Threading.Channels;

namespace TelegramOpenCodeBridge.Application;

public sealed class ChatRequestQueue
{
    private readonly int _queueCapacityPerChat;
    private readonly Func<ChatRequest, CancellationToken, Task> _processor;
    private readonly Func<ChatRequest, string, CancellationToken, Task> _rejector;
    private readonly Func<ChatRequest, CancellationToken, Task> _aborter;
    private readonly bool _notifyWhenStopping;
    private readonly ConcurrentDictionary<long, ChatQueueState> _queues = new();
    private readonly ConcurrentDictionary<long, ActiveChatRequest> _activeRequests = new();
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

    public async Task EnqueueAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        if (IsShuttingDown)
        {
            if (_notifyWhenStopping)
            {
                await _rejector(request, "Die Bridge wird gerade beendet. Neue Nachrichten werden aktuell nicht mehr angenommen.", cancellationToken);
            }

            return;
        }

        ChatQueueState queue = _queues.GetOrAdd(request.ChatId, CreateQueueState);
        if (!queue.Writer.TryWrite(request))
        {
            await _rejector(request, "Die Warteschlange für diesen Chat ist voll. Bitte versuche es gleich erneut.", cancellationToken);
        }
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
            activeRequest.CancellationTokenSource.Cancel();
            await _aborter(activeRequest.Request, cancellationToken);
        }

        await Task.WhenAll(_queues.Values.Select(queue => queue.Worker));
    }

    private bool IsShuttingDown => Volatile.Read(ref _shuttingDown) == 1;

    private ChatQueueState CreateQueueState(long chatId)
    {
        Channel<ChatRequest> channel = Channel.CreateBounded<ChatRequest>(new BoundedChannelOptions(_queueCapacityPerChat)
        {
            SingleReader = true,
            SingleWriter = false,
        });

        Task worker = Task.Run(() => ProcessQueueAsync(chatId, channel.Reader));
        return new ChatQueueState(channel.Writer, worker);
    }

    private async Task ProcessQueueAsync(long chatId, ChannelReader<ChatRequest> reader)
    {
        await foreach (ChatRequest request in reader.ReadAllAsync())
        {
            if (IsShuttingDown)
            {
                if (_notifyWhenStopping)
                {
                    await _rejector(request, "Die Bridge wird gerade beendet. Diese Nachricht wurde nicht mehr verarbeitet.", CancellationToken.None);
                }

                continue;
            }

            using CancellationTokenSource requestCancellationTokenSource = new();
            _activeRequests[chatId] = new ActiveChatRequest(request, requestCancellationTokenSource);
            try
            {
                await _processor(request, requestCancellationTokenSource.Token);
            }
            finally
            {
                _activeRequests.TryRemove(chatId, out _);
            }
        }
    }

    private sealed record ChatQueueState(ChannelWriter<ChatRequest> Writer, Task Worker);

    private sealed record ActiveChatRequest(ChatRequest Request, CancellationTokenSource CancellationTokenSource);
}

public sealed record ChatRequest(
    long ChatId,
    long? UserId,
    string? Username,
    string Text,
    string OpenCodeSessionId);
