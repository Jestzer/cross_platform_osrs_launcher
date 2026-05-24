// SPDX-License-Identifier: GPL-3.0-or-later
namespace OsrsLauncher.Core.Auth;

public static class AuthorizeUrlBuilder
{
    public static string Build(JagexOAuthConfig config, string codeChallenge, string state, string nonce)
    {
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = config.ClientId,
            ["redirect_uri"] = config.RedirectUri,
            ["scope"] = config.Scope,
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["prompt"] = "login",
        };

        var encoded = string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return $"{config.AuthorizeEndpoint}?{encoded}";
    }
}
