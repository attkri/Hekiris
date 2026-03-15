namespace Hekiris.Application;

public interface IBridgeConsole
{
    void WriteTranscript(string role, string text, string? logMessage = null, BridgeLogSeverity severity = BridgeLogSeverity.Info);

    void WriteInfo(string message);

    void WriteStatus(IEnumerable<string> lines);

    void WritePlainInfo(string message);

    void WriteWarning(string message);

    void WriteError(string message);
}

public interface IBridgeTelegramClient : IDisposable
{
    Task<TelegramBotIdentity> GetMeAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken cancellationToken);

    Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken, BridgeMessageFormat format = BridgeMessageFormat.PlainText);
}

public interface IBridgeOpenCodeClient : IDisposable
{
    Task<OpenCodeHealth> GetHealthAsync(CancellationToken cancellationToken);

    Task<bool> SessionExistsAsync(string sessionId, CancellationToken cancellationToken);

    Task<string?> GetLastUsedAgentAsync(string sessionId, CancellationToken cancellationToken);

    Task<OpenCodeMessageResponse> SendPromptAsync(string sessionId, string prompt, string agent, string workingDirectory, CancellationToken cancellationToken);

    Task AbortSessionAsync(string sessionId, CancellationToken cancellationToken);
}

public interface ICommandTimeLoopStateStore
{
    Task UpdateLastRunAsync(int commandIndex, DateTime lastRun, CancellationToken cancellationToken);
}
