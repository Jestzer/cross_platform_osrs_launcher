// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using OsrsLauncher.Core.Auth;
using Xunit;

namespace OsrsLauncher.Core.Tests.Auth;

public class OAuthClientTests
{
    private static readonly JagexOAuthConfig Config = new(
        AuthorizeEndpoint: "https://account.jagex.com/oauth2/auth",
        TokenEndpoint: "https://account.jagex.com/oauth2/token",
        ClientId: "test-client",
        RedirectUri: "https://example.test/callback",
        Scope: "openid offline");

    [Fact]
    public async Task ExchangeCodeAsync_ParsesTokensAndSendsAuthCodeGrant()
    {
        var json = """{"access_token":"AT","refresh_token":"RT","id_token":"IT","expires_in":3600}""";
        var handler = StubHttpMessageHandler.Json(json);
        var client = new OAuthClient(new HttpClient(handler), Config);

        var tokens = await client.ExchangeCodeAsync("the-code", "the-verifier");

        Assert.Equal("AT", tokens.AccessToken);
        Assert.Equal("RT", tokens.RefreshToken);
        Assert.Equal("IT", tokens.IdToken);
        Assert.Equal(3600, tokens.ExpiresIn);
        Assert.Contains("grant_type=authorization_code", handler.LastRequestBody);
        Assert.Contains("code=the-code", handler.LastRequestBody);
        Assert.Contains("code_verifier=the-verifier", handler.LastRequestBody);
    }

    [Fact]
    public async Task RefreshAsync_SendsRefreshGrant()
    {
        var json = """{"access_token":"AT2","refresh_token":"RT2","expires_in":3600}""";
        var handler = StubHttpMessageHandler.Json(json);
        var client = new OAuthClient(new HttpClient(handler), Config);

        var tokens = await client.RefreshAsync("old-refresh");

        Assert.Equal("AT2", tokens.AccessToken);
        Assert.Contains("grant_type=refresh_token", handler.LastRequestBody);
        Assert.Contains("refresh_token=old-refresh", handler.LastRequestBody);
        Assert.Contains("client_id=test-client", handler.LastRequestBody);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ThrowsOnErrorStatus()
    {
        var handler = StubHttpMessageHandler.Json("""{"error":"invalid_grant"}""", HttpStatusCode.BadRequest);
        var client = new OAuthClient(new HttpClient(handler), Config);

        await Assert.ThrowsAsync<OAuthException>(() => client.ExchangeCodeAsync("bad", "v"));
    }
}
