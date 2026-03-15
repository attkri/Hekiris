using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hekiris.Application;
using Hekiris.Infrastructure.Configuration;

namespace Hekiris.Infrastructure.OpenCode;

public sealed class OpenCodeClient : IBridgeOpenCodeClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly bool _disposeHttpClient;

    public OpenCodeClient(OpenCodeOptions options)
        : this(CreateHttpClient(options), true)
    {
    }

    public OpenCodeClient(HttpClient httpClient)
        : this(httpClient, false)
    {
    }

    private OpenCodeClient(HttpClient httpClient, bool disposeHttpClient)
    {
        _httpClient = httpClient;
        _disposeHttpClient = disposeHttpClient;
    }

    public async Task<OpenCodeHealth> GetHealthAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync("global/health", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        OpenCodeHealth health = (await response.Content.ReadFromJsonAsync<OpenCodeHealth>(SerializerOptions, cancellationToken))
            ?? throw new OpenCodeException("OpenCode health check returned an invalid response.");

        if (!health.Healthy)
        {
            throw new OpenCodeException("OpenCode reported unhealthy=false.");
        }

        return health;
    }

    public async Task<bool> SessionExistsAsync(string sessionId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync($"session/{Uri.EscapeDataString(sessionId)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return true;
    }

    public async Task<string?> GetLastUsedAgentAsync(string sessionId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync($"session/{Uri.EscapeDataString(sessionId)}/message", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        SessionMessageEnvelope[]? messages = await response.Content.ReadFromJsonAsync<SessionMessageEnvelope[]>(SerializerOptions, cancellationToken);
        if (messages is null || messages.Length == 0)
        {
            return null;
        }

        SessionMessageEnvelope? latestMessage = messages
            .Where(message => message.Info?.Time?.Created is not null)
            .OrderByDescending(message => message.Info!.Time!.Created)
            .FirstOrDefault();

        return latestMessage?.Info?.Agent;
    }

    public async Task<OpenCodeMessageResponse> SendPromptAsync(
        string sessionId,
        string prompt,
        string agent,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        PromptRequest request = new()
        {
            Parts =
            [
                new PromptPart
                {
                    Type = "text",
                    Text = prompt,
                },
            ],
            Agent = agent,
        };

        string requestUri = $"session/{Uri.EscapeDataString(sessionId)}/message?directory={Uri.EscapeDataString(workingDirectory)}";

        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(requestUri, request, SerializerOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return OpenCodeMessageResponse.Empty;
        }

        PromptResponse? promptResponse = JsonSerializer.Deserialize<PromptResponse>(responseBody, SerializerOptions);
        if (promptResponse is null)
        {
            throw new OpenCodeException("OpenCode returned no usable response.");
        }

        OpenCodeMessageResponse formattedResponse = BuildMessageResponse(promptResponse.Parts);
        if (!string.IsNullOrWhiteSpace(formattedResponse.Text))
        {
            return formattedResponse;
        }

        if (!string.IsNullOrWhiteSpace(promptResponse.Info?.Error))
        {
            throw new OpenCodeException(promptResponse.Info.Error);
        }

        return OpenCodeMessageResponse.Empty;
    }

    public async Task AbortSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.PostAsync($"session/{Uri.EscapeDataString(sessionId)}/abort", content: null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        await EnsureSuccessAsync(response, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static HttpClient CreateHttpClient(OpenCodeOptions options)
    {
        HttpClient httpClient = new()
        {
            BaseAddress = new Uri(EnsureTrailingSlash(options.BaseUrl), UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds),
        };

        if (!string.IsNullOrWhiteSpace(options.Password))
        {
            string username = string.IsNullOrWhiteSpace(options.Username) ? "opencode" : options.Username;
            string rawValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{options.Password}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", rawValue);
        }

        return httpClient;
    }

    private static string EnsureTrailingSlash(string baseUrl)
    {
        return baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : baseUrl + "/";
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        _ = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new OpenCodeException($"HTTP {(int)response.StatusCode} returned by OpenCode.");
    }

    private static OpenCodeMessageResponse BuildMessageResponse(IEnumerable<TextPart> parts)
    {
        List<TextPart> relevantParts = parts
            .Where(part => !string.IsNullOrWhiteSpace(part.Text))
            .Where(part => string.Equals(part.Type, "text", StringComparison.OrdinalIgnoreCase)
                || string.Equals(part.Type, "html", StringComparison.OrdinalIgnoreCase)
                || string.Equals(part.Type, "json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (relevantParts.Count == 0)
        {
            return OpenCodeMessageResponse.Empty;
        }

        bool isPureHtml = relevantParts.All(part => string.Equals(part.Type, "html", StringComparison.OrdinalIgnoreCase));
        bool isPureJson = relevantParts.All(part => string.Equals(part.Type, "json", StringComparison.OrdinalIgnoreCase));
        string text = string.Join(Environment.NewLine + Environment.NewLine, relevantParts.Select(part => part.Text));

        BridgeMessageFormat format = isPureHtml
            ? BridgeMessageFormat.Html
            : isPureJson
                ? BridgeMessageFormat.Json
                : BridgeMessageFormat.PlainText;

        return new OpenCodeMessageResponse(text, format);
    }

    private sealed class PromptRequest
    {
        [JsonPropertyName("parts")]
        public List<PromptPart> Parts { get; set; } = new();

        [JsonPropertyName("agent")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Agent { get; set; }
    }

    private sealed class PromptPart
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "text";

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private sealed class PromptResponse
    {
        [JsonPropertyName("info")]
        public PromptInfo? Info { get; set; }

        [JsonPropertyName("parts")]
        public List<TextPart> Parts { get; set; } = new();
    }

    private sealed class PromptInfo
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    private sealed class TextPart
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private sealed class SessionMessageEnvelope
    {
        [JsonPropertyName("info")]
        public SessionMessageInfo? Info { get; set; }
    }

    private sealed class SessionMessageInfo
    {
        [JsonPropertyName("agent")]
        public string? Agent { get; set; }

        [JsonPropertyName("time")]
        public SessionMessageTime? Time { get; set; }
    }

    private sealed class SessionMessageTime
    {
        [JsonPropertyName("created")]
        public long? Created { get; set; }
    }
}

