// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OsrsLauncher.Core.Auth;

public sealed class OAuthClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly JagexOAuthConfig _config;

    public OAuthClient(HttpClient http, JagexOAuthConfig config)
    {
        _http = http;
        _config = config;
    }

    public Task<OAuthTokens> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct = default)
        => PostTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _config.RedirectUri,
            ["client_id"] = _config.ClientId,
            ["code_verifier"] = codeVerifier,
        }, ct);

    public Task<OAuthTokens> RefreshAsync(string refreshToken, CancellationToken ct = default)
        => PostTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = _config.ClientId,
        }, ct);

    private async Task<OAuthTokens> PostTokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(form);
        using var resp = await _http.PostAsync(_config.TokenEndpoint, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new OAuthException($"Token endpoint returned {(int)resp.StatusCode}: {body}");

        var dto = JsonSerializer.Deserialize<TokenResponse>(body, JsonOpts)
            ?? throw new OAuthException("Empty token response.");
        return new OAuthTokens(dto.AccessToken, dto.RefreshToken, dto.IdToken, dto.ExpiresIn);
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("id_token")] string? IdToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
