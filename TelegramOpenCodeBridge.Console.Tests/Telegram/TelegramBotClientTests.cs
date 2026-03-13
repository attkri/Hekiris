using System.Net;
using TelegramOpenCodeBridge.Telegram;

namespace TelegramOpenCodeBridge.Tests.Telegram;

public sealed class TelegramBotClientTests
{
    [Fact]
    public async Task GetMeAsync_UsesTokenAsPathSegment()
    {
        StubHttpMessageHandler handler = new(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "ok": true,
                  "result": {
                    "id": 1,
                    "first_name": "Nova",
                    "username": "nova"
                  }
                }
                """),
            });

        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://api.telegram.org/"),
        };

        using TelegramBotClient client = new(httpClient, "123:abc");

        TelegramBotIdentity result = await client.GetMeAsync(CancellationToken.None);

        Assert.Equal("Nova", result.FirstName);
        Assert.Equal("https://api.telegram.org/bot123:abc/getMe", handler.Requests.Single().RequestUri!.AbsoluteUri);
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
