// SPDX-License-Identifier: GPL-3.0-or-later
using OsrsLauncher.Core.Auth;
using Xunit;

namespace OsrsLauncher.Core.Tests.Auth;

public class AuthorizeUrlBuilderTests
{
    private static readonly JagexOAuthConfig Config = new(
        AuthorizeEndpoint: "https://account.jagex.com/oauth2/auth",
        TokenEndpoint: "https://account.jagex.com/oauth2/token",
        ClientId: "test-client",
        RedirectUri: "https://example.test/callback",
        Scope: "openid offline");

    [Fact]
    public void Build_IncludesPkceAndClientParams()
    {
        var url = AuthorizeUrlBuilder.Build(Config, "CHALLENGE", "STATE", "NONCE");

        Assert.StartsWith("https://account.jagex.com/oauth2/auth?", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains("client_id=test-client", url);
        Assert.Contains("code_challenge=CHALLENGE", url);
        Assert.Contains("code_challenge_method=S256", url);
        Assert.Contains("state=STATE", url);
        Assert.Contains("scope=openid%20offline", url);
    }
}
