using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TelegramOpenCodeBridge.Configuration;

namespace TelegramOpenCodeBridge.Telegram;

public sealed class TelegramBotClient : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly bool _disposeHttpClient;
    private readonly string _botToken;

    public TelegramBotClient(TelegramOptions options)
        : this(CreateHttpClient(options), options.BotToken, true)
    {
    }

    public TelegramBotClient(HttpClient httpClient, string botToken)
        : this(httpClient, botToken, false)
    {
    }

    private TelegramBotClient(HttpClient httpClient, string botToken, bool disposeHttpClient)
    {
        _httpClient = httpClient;
        _botToken = botToken;
        _disposeHttpClient = disposeHttpClient;
    }

    public async Task<TelegramBotIdentity> GetMeAsync(CancellationToken cancellationToken)
    {
        TelegramEnvelope<TelegramBotIdentity>? response = await PostAsync<object, TelegramEnvelope<TelegramBotIdentity>>("getMe", new { }, cancellationToken);
        return response?.Result ?? throw new TelegramException("Telegram lieferte keine Bot-Identität.");
    }

    public async Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken cancellationToken)
    {
        TelegramEnvelope<List<TelegramUpdate>>? response = await PostAsync<object, TelegramEnvelope<List<TelegramUpdate>>>(
            "getUpdates",
            new
            {
                offset,
                timeout = timeoutSeconds,
                allowed_updates = new[] { "message" },
            },
            cancellationToken);

        return response?.Result ?? new List<TelegramUpdate>();
    }

    public async Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        foreach (string chunk in SplitMessage(text))
        {
            await PostAsync<object, TelegramEnvelope<TelegramMessage>>("sendMessage", new { chat_id = chatId, text = chunk }, cancellationToken);
        }
    }

    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private async Task<TResponse?> PostAsync<TRequest, TResponse>(string method, TRequest payload, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(BuildMethodPath(method), payload, SerializerOptions, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new TelegramException("Telegram hat den Bot-Token abgelehnt.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new TelegramException($"Telegram meldet HTTP {(int)response.StatusCode}: {body}".Trim());
        }

        TResponse? envelope = JsonSerializer.Deserialize<TResponse>(body, SerializerOptions);
        if (envelope is TelegramEnvelope envelopeBase && !envelopeBase.Ok)
        {
            throw new TelegramException(envelopeBase.Description ?? "Telegram meldet einen unbekannten API-Fehler.");
        }

        return envelope;
    }

    private string BuildMethodPath(string method)
    {
        return $"/bot{_botToken}/{method}";
    }

    private static HttpClient CreateHttpClient(TelegramOptions options)
    {
        HttpClient httpClient = new()
        {
            BaseAddress = new Uri(EnsureTrailingSlash(options.ApiBaseUrl), UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(Math.Max(options.PollingTimeoutSeconds + 15, 30)),
        };

        return httpClient;
    }

    private static string EnsureTrailingSlash(string baseUrl)
    {
        return baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : baseUrl + "/";
    }

    internal static IEnumerable<string> SplitMessage(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield return string.Empty;
            yield break;
        }

        const int chunkSize = 3500;
        int offset = 0;

        while (offset < text.Length)
        {
            int length = Math.Min(chunkSize, text.Length - offset);
            int splitIndex = text.LastIndexOf('\n', offset + length - 1, length);
            if (splitIndex >= offset + (chunkSize / 2))
            {
                length = splitIndex - offset + 1;
            }

            yield return text.Substring(offset, length).TrimEnd();
            offset += length;
        }
    }
}

public sealed class TelegramException : Exception
{
    public TelegramException(string message)
        : base(message)
    {
    }
}
