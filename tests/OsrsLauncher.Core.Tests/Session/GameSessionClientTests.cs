// SPDX-License-Identifier: GPL-3.0-or-later
using OsrsLauncher.Core.Session;
using Xunit;

namespace OsrsLauncher.Core.Tests.Session;

public class GameSessionClientTests
{
    private static readonly GameSessionConfig Config = new(
        SessionsEndpoint: "https://auth.jagex.com/game-session/v1/sessions",
        AccountsEndpoint: "https://auth.jagex.com/game-session/v1/accounts");

    [Fact]
    public async Task CreateSessionAsync_ReturnsSessionId()
    {
        var handler = StubHttpMessageHandler.Json("""{"sessionId":"SESS-1"}""");
        var client = new GameSessionClient(new HttpClient(handler), Config);

        var session = await client.CreateSessionAsync("the-id-token");

        Assert.Equal("SESS-1", session.SessionId);
        Assert.Contains("the-id-token", handler.LastRequestBody);
    }

    [Fact]
    public async Task ListCharactersAsync_ReturnsCharactersAndSendsBearer()
    {
        var json = """[{"accountId":"ACC-1","displayName":"Zezima"},{"accountId":"ACC-2","displayName":"Woox"}]""";
        var handler = StubHttpMessageHandler.Json(json);
        var client = new GameSessionClient(new HttpClient(handler), Config);

        var chars = await client.ListCharactersAsync(new GameSession("SESS-1"));

        Assert.Equal(2, chars.Count);
        Assert.Equal("ACC-1", chars[0].AccountId);
        Assert.Equal("Zezima", chars[0].DisplayName);
        Assert.Equal("Bearer SESS-1", handler.LastRequest!.Headers.Authorization!.ToString());
    }
}
