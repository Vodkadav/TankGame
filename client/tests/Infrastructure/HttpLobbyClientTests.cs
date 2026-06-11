using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TankGame.Infrastructure.Net;
using Xunit;

namespace TankGame.Tests.Infrastructure;

// Exercises HttpLobbyClient against a canned HttpMessageHandler — no live Worker. The route shapes
// mirror server/worker/src/index.ts: POST /lobby → 201 {code,wsUrl}; POST /lobby/:code/join → 200 or 404.
public class HttpLobbyClientTests
{
    private sealed class CannedHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body),
            });
        }
    }

    [Fact]
    public async Task CreateLobbyAsync_PostsToLobby_AndReturnsTheMintedCode()
    {
        var handler = new CannedHandler(HttpStatusCode.Created, "{\"code\":\"CDEFGH\",\"wsUrl\":\"/room/CDEFGH\"}");
        var client = new HttpLobbyClient("https://lobby.test", handler);

        var code = await client.CreateLobbyAsync();

        Assert.Equal("CDEFGH", code);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://lobby.test/lobby", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateLobbyAsync_ReturnsNull_WhenTheServiceFails()
    {
        var handler = new CannedHandler(HttpStatusCode.InternalServerError, "boom");
        var client = new HttpLobbyClient("https://lobby.test", handler);

        Assert.Null(await client.CreateLobbyAsync());
    }

    [Fact]
    public async Task JoinLobbyAsync_PostsToTheJoinRoute_AndReportsAKnownLobby()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, "{\"code\":\"CDEFGH\",\"wsUrl\":\"/room/CDEFGH\"}");
        var client = new HttpLobbyClient("https://lobby.test", handler);

        var known = await client.JoinLobbyAsync("CDEFGH");

        Assert.True(known);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://lobby.test/lobby/CDEFGH/join", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task JoinLobbyAsync_ReportsAnUnknownLobby()
    {
        var handler = new CannedHandler(HttpStatusCode.NotFound, "no such lobby");
        var client = new HttpLobbyClient("https://lobby.test", handler);

        Assert.False(await client.JoinLobbyAsync("ZZZZZZ"));
    }
}
