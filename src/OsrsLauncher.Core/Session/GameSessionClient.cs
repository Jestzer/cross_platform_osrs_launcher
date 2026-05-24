// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OsrsLauncher.Core.Session;

public sealed class GameSessionClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly GameSessionConfig _config;

    public GameSessionClient(HttpClient http, GameSessionConfig config)
    {
        _http = http;
        _config = config;
    }

    public async Task<GameSession> CreateSessionAsync(string idToken, CancellationToken ct = default)
    {
        using var content = JsonContent.Create(new { idToken });
        using var resp = await _http.PostAsync(_config.SessionsEndpoint, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new GameSessionException($"sessions returned {(int)resp.StatusCode}: {body}");

        var dto = JsonSerializer.Deserialize<SessionResponse>(body, JsonOpts)
            ?? throw new GameSessionException("Empty session response.");
        return new GameSession(dto.SessionId);
    }

    public async Task<IReadOnlyList<JagexCharacter>> ListCharactersAsync(GameSession session, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, _config.AccountsEndpoint);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.SessionId);
        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new GameSessionException($"accounts returned {(int)resp.StatusCode}: {body}");

        var dtos = JsonSerializer.Deserialize<List<AccountResponse>>(body, JsonOpts) ?? new List<AccountResponse>();
        return dtos.Select(a => new JagexCharacter(a.AccountId, a.DisplayName)).ToList();
    }

    private sealed record SessionResponse(
        [property: JsonPropertyName("sessionId")] string SessionId);

    private sealed record AccountResponse(
        [property: JsonPropertyName("accountId")] string AccountId,
        [property: JsonPropertyName("displayName")] string DisplayName);
}
