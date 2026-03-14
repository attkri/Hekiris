using System.Net;
using System.Text.Json;
using Hekiris.OpenCode;

namespace Hekiris.Tests.OpenCode;

public sealed class OpenCodeClientTests
{
    [Fact]
    public async Task SendPromptAsync_ReturnsCombinedTextParts()
    {
        StubHttpMessageHandler handler = new(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "info": {},
                  "parts": [
                    { "type": "text", "text": "Hallo" },
                    { "type": "tool", "text": "ignorieren" },
                    { "type": "text", "text": "Welt" }
                  ]
                }
                """)
            });

        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("http://localhost:4096/"),
        };

        using OpenCodeClient client = new(httpClient);

        string result = await client.SendPromptAsync("ses_test", "Ping", null, CancellationToken.None);

        Assert.Equal("Hallo" + Environment.NewLine + Environment.NewLine + "Welt", result);
        Assert.Equal(HttpMethod.Post, handler.Requests.Single().Method);
        Assert.EndsWith("/session/ses_test/message", handler.Requests.Single().RequestUri!.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendPromptAsync_SendsConfiguredModel()
    {
        StubHttpMessageHandler handler = new(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "info": {},
                  "parts": [
                    { "type": "text", "text": "OK" }
                  ]
                }
                """),
            });

        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("http://localhost:4096/"),
        };

        using OpenCodeClient client = new(httpClient);

        await client.SendPromptAsync("ses_test", "Ping", OpenCodeModelSelection.Parse("openai/gpt-4.1"), CancellationToken.None);

        string body = await handler.Requests.Single().Content!.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(body);

        Assert.Equal("openai", document.RootElement.GetProperty("model").GetProperty("providerID").GetString());
        Assert.Equal("gpt-4.1", document.RootElement.GetProperty("model").GetProperty("modelID").GetString());
    }

    [Fact]
    public async Task GetHealthAsync_ThrowsForHttpErrors()
    {
        StubHttpMessageHandler handler = new(
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("boom"),
            });

        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("http://localhost:4096/"),
        };

        using OpenCodeClient client = new(httpClient);

        await Assert.ThrowsAsync<OpenCodeException>(() => client.GetHealthAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetHealthAsync_ThrowsWhenServerReportsUnhealthy()
    {
        StubHttpMessageHandler handler = new(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "healthy": false,
                  "version": "1.2.3"
                }
                """),
            });

        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("http://localhost:4096/"),
        };

        using OpenCodeClient client = new(httpClient);

        await Assert.ThrowsAsync<OpenCodeException>(() => client.GetHealthAsync(CancellationToken.None));
    }

    [Fact]
    public void Parse_DefaultsToOpenAiProvider()
    {
        OpenCodeModelSelection selection = OpenCodeModelSelection.Parse("gpt-4");

        Assert.Equal("openai", selection.ProviderId);
        Assert.Equal("gpt-4", selection.ModelId);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responder(request));
        }
    }
}
