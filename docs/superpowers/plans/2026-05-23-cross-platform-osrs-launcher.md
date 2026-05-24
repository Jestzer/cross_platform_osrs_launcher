# Cross-Platform OSRS Launcher Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a native Apple Silicon app that performs the Jagex-account OAuth login and launches RuneLite with the `JX_*` credentials it needs — replacing the Intel-only Jagex Launcher for this task.

**Architecture:** Pure-logic core (`OsrsLauncher.Core`) holding the OAuth, game-session, and launch logic — fully unit-testable with mocked HTTP/process. A throwaway spike de-risks the Avalonia WebView redirect capture. A console harness wires the core into a real, manually-verified authenticated launch (the Phase 1 milestone). The Avalonia GUI, OS-keychain persistence, and `.app` packaging are later phases (roadmap at the end).

**Tech Stack:** C# / .NET 8, xUnit, Avalonia 12 (spike + later GUI), System.Text.Json, macOS `Process` + `open`.

---

## Scope of this plan

This plan covers **Phase 1 only: Tasks 0–8**, ending at a working, manually-verified "log in → launch RuneLite authenticated" milestone using a console harness. Phases 2–4 (keychain persistence + fast path, Avalonia GUI, packaging/signing) are deliberately deferred to follow-on plans because their detailed design depends on the **Task 1 spike** outcome. The roadmap for them is at the end.

## Conventions

- **Working directory:** all commands run from the repo root `/Users/james/My_Programs/cross_platform_osrs_launcher` unless noted.
- **License header:** every `.cs` file starts with the line `// SPDX-License-Identifier: GPL-3.0-or-later` (the repo is GPLv3).
- **Commits:** messages use Conventional Commits and end with the trailer `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`. The per-step commit commands below show the title only; append the trailer.
- **Namespaces:** root namespace is `OsrsLauncher.Core` (and `OsrsLauncher.Harness`).
- **Unverified constants:** the exact Jagex OAuth endpoints, `client_id`, scopes, redirect URI, JSON shapes, and `JX_*` variable names are captured in **Task 2** from the reference repos and confirmed live in **Task 8**. Tasks 3–7 are written against those expected shapes and use fixture configs, so they do not block on the live values.

---

## Task 0: Solution and project scaffolding

**Files:**
- Create: `OsrsLauncher.sln`
- Create: `src/OsrsLauncher.Core/OsrsLauncher.Core.csproj`
- Create: `tests/OsrsLauncher.Core.Tests/OsrsLauncher.Core.Tests.csproj`

- [ ] **Step 1: Create the solution and projects**

```bash
dotnet new sln -n OsrsLauncher
dotnet new classlib -n OsrsLauncher.Core -o src/OsrsLauncher.Core
dotnet new xunit -n OsrsLauncher.Core.Tests -o tests/OsrsLauncher.Core.Tests
rm src/OsrsLauncher.Core/Class1.cs tests/OsrsLauncher.Core.Tests/UnitTest1.cs
dotnet sln add src/OsrsLauncher.Core tests/OsrsLauncher.Core.Tests
dotnet add tests/OsrsLauncher.Core.Tests reference src/OsrsLauncher.Core
```

- [ ] **Step 2: Confirm the projects target net8.0 with nullable enabled**

Open `src/OsrsLauncher.Core/OsrsLauncher.Core.csproj` and verify it contains (the `classlib` template produces this on SDK 8.x):

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

If `TargetFramework` is not `net8.0`, change it to `net8.0`.

- [ ] **Step 3: Build to verify the skeleton compiles**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore: scaffold solution with Core and Core.Tests projects"
```

---

## Task 1: Spike — Avalonia WebView redirect capture (de-risk)

**Purpose:** prove the Avalonia 12 WebView on macOS arm64 can observe a navigation to a custom scheme (`testscheme:`) so we can read the OAuth `code` and cancel the navigation. This resolves spec §8. The spike is **throwaway** and not added to `OsrsLauncher.sln`.

**Files:**
- Create: `spikes/webview-redirect-spike/` (Avalonia app)
- Create: `docs/reference/webview-spike-findings.md`

- [ ] **Step 1: Create a minimal Avalonia app for the spike**

```bash
dotnet new install Avalonia.Templates
dotnet new avalonia.app -n WebViewSpike -o spikes/webview-redirect-spike
```

- [ ] **Step 2: Add the Avalonia 12 WebView package**

Reference docs: https://docs.avaloniaui.net/docs/app-development/embedding-web-content

```bash
dotnet add spikes/webview-redirect-spike package Avalonia.WebView
```

If the package id differs in Avalonia 12, find the correct one from the docs above and record it in the findings file. Pin Avalonia packages to the latest 12.x.

- [ ] **Step 3: Wire a WebView that loads an inline page which redirects to a custom scheme**

In `spikes/webview-redirect-spike/MainWindow.axaml.cs`, after `InitializeComponent()`, create a WebView, subscribe to its navigation-starting event, and load an inline HTML page that triggers a custom-scheme navigation:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// Spike: confirm we can observe + cancel a custom-scheme navigation.
// NOTE: exact WebView class + navigation event names are the unknown this
// spike resolves — consult the Avalonia 12 docs linked in the plan and adjust.
const string html =
    "<html><body><a id='go' href='testscheme:callback?code=SPIKE123&state=abc'>go</a>" +
    "<script>document.getElementById('go').click();</script></body></html>";

// Pseudocode shape to realize against the real API:
// var web = new WebView();
// web.NavigationStarting += (s, e) => {
//     System.Diagnostics.Debug.WriteLine($"NAV: {e.Url}");
//     if (e.Url.StartsWith("testscheme:")) { e.Cancel = true; /* parse code */ }
// };
// web.HtmlContent = html;  // or web.NavigateToString(html)
// Content = web;
```

- [ ] **Step 4: Run the spike on macOS and observe**

Run: `dotnet run --project spikes/webview-redirect-spike`
Expected: the window opens, the WebView loads, and the navigation handler logs `NAV: testscheme:callback?code=SPIKE123&state=abc` (visible in the run console / debugger).

- [ ] **Step 5: Record the outcome**

Write `docs/reference/webview-spike-findings.md` documenting: the exact Avalonia WebView package id + version, the exact control class and navigation event name, whether the custom-scheme URL was observable and cancelable, the installed `dotnet --version`, and whether Avalonia 12 required .NET 9 (spec §14). State the chosen Phase-3 capture strategy: **(1)** in-WebView navigation interception if it worked, else **(2)** OS `jagex:` protocol handler, else **(3)** localhost loopback.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "spike: validate Avalonia WebView custom-scheme redirect capture"
```

---

## Task 2: Reference reconnaissance — capture the Jagex flow constants and shapes

**Purpose:** transcribe the exact OAuth constants, JSON shapes, and `JX_*` variable names from the public reference implementations so Tasks 3–7 use real values. No live login yet (that is Task 8).

**Files:**
- Create: `docs/reference/jagex-flow.md`

- [ ] **Step 1: Clone the reference repos into a scratch location (outside the repo tree)**

```bash
git clone --depth 1 https://github.com/melxin/native-linux-jagex-launcher /tmp/ref-melxin
git clone --depth 1 https://github.com/aitoiaita/linux-jagex-launcher /tmp/ref-aitoiaita
```

- [ ] **Step 2: Extract the OAuth + session details**

Read the Rust source in `/tmp/ref-melxin/src` and `/tmp/ref-aitoiaita/src`. Find and record, with the source file + line for each:
- Authorize endpoint URL and token endpoint URL
- `client_id` value
- `scope` string
- `redirect_uri`
- Whether PKCE (`code_challenge`/`code_verifier`) is used and the method
- Game-session creation endpoint + request body shape + response shape (the `sessionId` field)
- Character/accounts list endpoint + auth header + response shape (`accountId`, `displayName` fields)

- [ ] **Step 3: Confirm the `JX_*` variables RuneLite consumes**

Cross-check the env vars the reference launchers set against RuneLite's source/wiki (https://github.com/runelite/runelite/wiki/Using-Jagex-Accounts). Record the exact variable names and what each holds.

- [ ] **Step 4: Write `docs/reference/jagex-flow.md`**

Document every value from Steps 2–3 with its source citation. This file is the single source of truth for Tasks 5, 6, 7, and 8. Mark anything still uncertain as "confirm in Task 8."

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "docs: capture Jagex OAuth flow constants and JX_ contract from references"
```

---

## Task 3: PKCE generator

**Files:**
- Create: `src/OsrsLauncher.Core/Auth/Pkce.cs`
- Test: `tests/OsrsLauncher.Core.Tests/Auth/PkceTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/OsrsLauncher.Core.Tests/Auth/PkceTests.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
using OsrsLauncher.Core.Auth;
using Xunit;

namespace OsrsLauncher.Core.Tests.Auth;

public class PkceTests
{
    [Fact]
    public void CreateChallenge_MatchesRfc7636Vector()
    {
        // RFC 7636 Appendix B test vector.
        var verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        var challenge = Pkce.CreateChallenge(verifier);
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", challenge);
    }

    [Fact]
    public void GenerateVerifier_IsUrlSafeAndCorrectLength()
    {
        var v = Pkce.GenerateVerifier();
        Assert.InRange(v.Length, 43, 128);
        Assert.DoesNotContain('+', v);
        Assert.DoesNotContain('/', v);
        Assert.DoesNotContain('=', v);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~PkceTests`
Expected: FAIL — `Pkce` does not exist (compile error).

- [ ] **Step 3: Implement `Pkce`**

`src/OsrsLauncher.Core/Auth/Pkce.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.Cryptography;
using System.Text;

namespace OsrsLauncher.Core.Auth;

public static class Pkce
{
    public static string GenerateVerifier(int byteLength = 32)
        => Base64Url(RandomNumberGenerator.GetBytes(byteLength));

    public static string CreateChallenge(string verifier)
        => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~PkceTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add PKCE verifier/challenge generation"
```

---

## Task 4: OAuth config and authorize-URL builder

**Files:**
- Create: `src/OsrsLauncher.Core/Auth/JagexOAuthConfig.cs`
- Create: `src/OsrsLauncher.Core/Auth/AuthorizeUrlBuilder.cs`
- Test: `tests/OsrsLauncher.Core.Tests/Auth/AuthorizeUrlBuilderTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/OsrsLauncher.Core.Tests/Auth/AuthorizeUrlBuilderTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~AuthorizeUrlBuilderTests`
Expected: FAIL — `JagexOAuthConfig` / `AuthorizeUrlBuilder` do not exist.

- [ ] **Step 3: Implement the config and builder**

`src/OsrsLauncher.Core/Auth/JagexOAuthConfig.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
namespace OsrsLauncher.Core.Auth;

public sealed record JagexOAuthConfig(
    string AuthorizeEndpoint,
    string TokenEndpoint,
    string ClientId,
    string RedirectUri,
    string Scope);
```

`src/OsrsLauncher.Core/Auth/AuthorizeUrlBuilder.cs`:

```csharp
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
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~AuthorizeUrlBuilderTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add Jagex OAuth config and authorize-URL builder"
```

---

## Task 5: OAuthClient — token exchange and refresh

**Files:**
- Create: `src/OsrsLauncher.Core/Auth/OAuthTokens.cs`
- Create: `src/OsrsLauncher.Core/Auth/OAuthException.cs`
- Create: `src/OsrsLauncher.Core/Auth/OAuthClient.cs`
- Create: `tests/OsrsLauncher.Core.Tests/StubHttpMessageHandler.cs`
- Test: `tests/OsrsLauncher.Core.Tests/Auth/OAuthClientTests.cs`

- [ ] **Step 1: Add the reusable test HTTP stub**

`tests/OsrsLauncher.Core.Tests/StubHttpMessageHandler.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;

namespace OsrsLauncher.Core.Tests;

public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => _responder = responder;

    public static StubHttpMessageHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(_ => new HttpResponseMessage(status) { Content = new StringContent(json) });

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (request.Content is not null)
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        return _responder(request);
    }
}
```

- [ ] **Step 2: Write the failing tests**

`tests/OsrsLauncher.Core.Tests/Auth/OAuthClientTests.cs`:

```csharp
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
    }

    [Fact]
    public async Task ExchangeCodeAsync_ThrowsOnErrorStatus()
    {
        var handler = StubHttpMessageHandler.Json("""{"error":"invalid_grant"}""", HttpStatusCode.BadRequest);
        var client = new OAuthClient(new HttpClient(handler), Config);

        await Assert.ThrowsAsync<OAuthException>(() => client.ExchangeCodeAsync("bad", "v"));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~OAuthClientTests`
Expected: FAIL — `OAuthClient` / `OAuthTokens` / `OAuthException` do not exist.

- [ ] **Step 4: Implement the tokens record, exception, and client**

`src/OsrsLauncher.Core/Auth/OAuthTokens.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
namespace OsrsLauncher.Core.Auth;

public sealed record OAuthTokens(
    string AccessToken,
    string? RefreshToken,
    string? IdToken,
    int ExpiresIn);
```

`src/OsrsLauncher.Core/Auth/OAuthException.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
namespace OsrsLauncher.Core.Auth;

public sealed class OAuthException : Exception
{
    public OAuthException(string message) : base(message) { }
}
```

`src/OsrsLauncher.Core/Auth/OAuthClient.cs`:

```csharp
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
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~OAuthClientTests`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add OAuthClient with token exchange and refresh"
```

---

## Task 6: GameSessionClient — create session and list characters

**Files:**
- Create: `src/OsrsLauncher.Core/Session/GameSessionModels.cs`
- Create: `src/OsrsLauncher.Core/Session/GameSessionException.cs`
- Create: `src/OsrsLauncher.Core/Session/GameSessionClient.cs`
- Test: `tests/OsrsLauncher.Core.Tests/Session/GameSessionClientTests.cs`

> The endpoints and JSON shapes below are the expected shapes from Task 2's `docs/reference/jagex-flow.md`. If Task 2 found different field names, update the `JsonPropertyName` attributes and tests to match before implementing.

- [ ] **Step 1: Write the failing tests**

`tests/OsrsLauncher.Core.Tests/Session/GameSessionClientTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~GameSessionClientTests`
Expected: FAIL — session types do not exist.

- [ ] **Step 3: Implement the models, exception, and client**

`src/OsrsLauncher.Core/Session/GameSessionModels.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
namespace OsrsLauncher.Core.Session;

public sealed record GameSession(string SessionId);

public sealed record JagexCharacter(string AccountId, string DisplayName);

public sealed record GameSessionConfig(string SessionsEndpoint, string AccountsEndpoint);
```

`src/OsrsLauncher.Core/Session/GameSessionException.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
namespace OsrsLauncher.Core.Session;

public sealed class GameSessionException : Exception
{
    public GameSessionException(string message) : base(message) { }
}
```

`src/OsrsLauncher.Core/Session/GameSessionClient.cs`:

```csharp
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
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~GameSessionClientTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add GameSessionClient for session + character list"
```

---

## Task 7: RuneLiteLauncher — build JX_ env and spawn RuneLite

**Files:**
- Create: `src/OsrsLauncher.Core/Launch/IProcessRunner.cs`
- Create: `src/OsrsLauncher.Core/Launch/ProcessRunner.cs`
- Create: `src/OsrsLauncher.Core/Launch/RuneLiteLauncher.cs`
- Test: `tests/OsrsLauncher.Core.Tests/Launch/RuneLiteLauncherTests.cs`

> The `JX_*` key names below are the expected set from Task 2. If Task 2 found a different set, update `BuildEnvironment` and the test together before implementing.

- [ ] **Step 1: Write the failing tests**

`tests/OsrsLauncher.Core.Tests/Launch/RuneLiteLauncherTests.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
using OsrsLauncher.Core.Auth;
using OsrsLauncher.Core.Launch;
using OsrsLauncher.Core.Session;
using Xunit;

namespace OsrsLauncher.Core.Tests.Launch;

public class RuneLiteLauncherTests
{
    private static RuneLiteLaunchInputs SampleInputs() => new(
        new GameSession("SESS-1"),
        new JagexCharacter("ACC-1", "Zezima"),
        new OAuthTokens("AT", "RT", "IT", 3600));

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public string? StartedPath;
        public IReadOnlyDictionary<string, string>? Env;
        public void Start(string executablePath, IReadOnlyDictionary<string, string> environment)
        {
            StartedPath = executablePath;
            Env = environment;
        }
    }

    [Fact]
    public void BuildEnvironment_MapsJxVariables()
    {
        var env = RuneLiteLauncher.BuildEnvironment(SampleInputs());

        Assert.Equal("SESS-1", env["JX_SESSION_ID"]);
        Assert.Equal("ACC-1", env["JX_CHARACTER_ID"]);
        Assert.Equal("Zezima", env["JX_DISPLAY_NAME"]);
        Assert.Equal("AT", env["JX_ACCESS_TOKEN"]);
        Assert.Equal("RT", env["JX_REFRESH_TOKEN"]);
    }

    [Fact]
    public void ResolveExecutablePath_PrefersOverrideWhenItExists()
    {
        var launcher = new RuneLiteLauncher(new FakeProcessRunner(), fileExists: p => p == "/custom/RuneLite");
        Assert.Equal("/custom/RuneLite", launcher.ResolveExecutablePath("/custom/RuneLite"));
    }

    [Fact]
    public void ResolveExecutablePath_ThrowsWhenMissing()
    {
        var launcher = new RuneLiteLauncher(new FakeProcessRunner(), fileExists: _ => false);
        Assert.Throws<RuneLiteNotFoundException>(() => launcher.ResolveExecutablePath(null));
    }

    [Fact]
    public void Launch_StartsResolvedPathWithEnv()
    {
        var runner = new FakeProcessRunner();
        var launcher = new RuneLiteLauncher(runner, fileExists: _ => true);

        launcher.Launch(SampleInputs(), overridePath: "/custom/RuneLite");

        Assert.Equal("/custom/RuneLite", runner.StartedPath);
        Assert.Equal("SESS-1", runner.Env!["JX_SESSION_ID"]);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~RuneLiteLauncherTests`
Expected: FAIL — launch types do not exist.

- [ ] **Step 3: Implement the runner abstraction and launcher**

`src/OsrsLauncher.Core/Launch/IProcessRunner.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
namespace OsrsLauncher.Core.Launch;

public interface IProcessRunner
{
    void Start(string executablePath, IReadOnlyDictionary<string, string> environment);
}
```

`src/OsrsLauncher.Core/Launch/ProcessRunner.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;

namespace OsrsLauncher.Core.Launch;

public sealed class ProcessRunner : IProcessRunner
{
    public void Start(string executablePath, IReadOnlyDictionary<string, string> environment)
    {
        var psi = new ProcessStartInfo(executablePath) { UseShellExecute = false };
        foreach (var (key, value) in environment)
            psi.Environment[key] = value;
        Process.Start(psi);
    }
}
```

`src/OsrsLauncher.Core/Launch/RuneLiteLauncher.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
using OsrsLauncher.Core.Auth;
using OsrsLauncher.Core.Session;

namespace OsrsLauncher.Core.Launch;

public sealed record RuneLiteLaunchInputs(GameSession Session, JagexCharacter Character, OAuthTokens Tokens);

public sealed class RuneLiteNotFoundException : Exception
{
    public RuneLiteNotFoundException(string message) : base(message) { }
}

public sealed class RuneLiteLauncher
{
    public const string DefaultMacPath = "/Applications/RuneLite.app/Contents/MacOS/RuneLite";

    private readonly IProcessRunner _runner;
    private readonly Func<string, bool> _fileExists;

    public RuneLiteLauncher(IProcessRunner runner, Func<string, bool>? fileExists = null)
    {
        _runner = runner;
        _fileExists = fileExists ?? File.Exists;
    }

    public static IReadOnlyDictionary<string, string> BuildEnvironment(RuneLiteLaunchInputs input) => new Dictionary<string, string>
    {
        ["JX_SESSION_ID"] = input.Session.SessionId,
        ["JX_CHARACTER_ID"] = input.Character.AccountId,
        ["JX_DISPLAY_NAME"] = input.Character.DisplayName,
        ["JX_ACCESS_TOKEN"] = input.Tokens.AccessToken,
        ["JX_REFRESH_TOKEN"] = input.Tokens.RefreshToken ?? "",
    };

    public string ResolveExecutablePath(string? overridePath)
    {
        var path = overridePath ?? DefaultMacPath;
        if (!_fileExists(path))
            throw new RuneLiteNotFoundException($"RuneLite executable not found at: {path}");
        return path;
    }

    public void Launch(RuneLiteLaunchInputs input, string? overridePath = null)
        => _runner.Start(ResolveExecutablePath(overridePath), BuildEnvironment(input));
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~RuneLiteLauncherTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Run the full suite**

Run: `dotnet test`
Expected: PASS (all tasks' tests green).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add RuneLiteLauncher with JX_ env construction and path resolution"
```

---

## Task 8: Console harness — first authenticated launch (milestone)

> **STATUS — 2026-05-24: ✅ DONE, implemented differently than written below.** The
> console + system-browser approach below does NOT work for the real flow: the Jagex login
> is **two OAuth legs**, and leg 2 is implicit (`id_token` returned in the URL `#fragment`,
> which a localhost/console catcher cannot read) and depends on leg-1 session cookies. It was
> implemented instead as a **WebView-driven two-leg harness** (`src/OsrsLauncher.Harness`,
> Avalonia + `NativeWebView`) reusing the Core library. Verified end-to-end on Apple Silicon:
> real Jagex login → game session → character selection → RuneLite logs into the game world.
> Key fix: emit only the three Jagex `JX_*` vars (see `docs/reference/jagex-flow.md` §5). The
> steps below are retained for historical context.

**Purpose:** wire the core into a real login using the **system browser** (avoids the WebView dependency for this milestone), confirm the Task 2 constants live, and achieve the first authenticated RuneLite launch. This is the Phase 1 milestone.

**Files:**
- Create: `src/OsrsLauncher.Harness/OsrsLauncher.Harness.csproj`
- Create: `src/OsrsLauncher.Harness/Program.cs`
- Create: `src/OsrsLauncher.Harness/JagexEndpoints.cs`
- Create: `docs/manual-tests/first-authenticated-launch.md`

- [ ] **Step 1: Create the harness project and reference Core**

```bash
dotnet new console -n OsrsLauncher.Harness -o src/OsrsLauncher.Harness
dotnet sln add src/OsrsLauncher.Harness
dotnet add src/OsrsLauncher.Harness reference src/OsrsLauncher.Core
```

- [ ] **Step 2: Add the verified endpoint constants from Task 2**

`src/OsrsLauncher.Harness/JagexEndpoints.cs` — fill every value from `docs/reference/jagex-flow.md`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
using OsrsLauncher.Core.Auth;
using OsrsLauncher.Core.Session;

namespace OsrsLauncher.Harness;

public static class JagexEndpoints
{
    // Values come from docs/reference/jagex-flow.md (Task 2), confirmed live here.
    public static JagexOAuthConfig OAuth { get; } = new(
        AuthorizeEndpoint: "FILL_FROM_TASK_2",
        TokenEndpoint: "FILL_FROM_TASK_2",
        ClientId: "FILL_FROM_TASK_2",
        RedirectUri: "FILL_FROM_TASK_2",
        Scope: "FILL_FROM_TASK_2");

    public static GameSessionConfig Session { get; } = new(
        SessionsEndpoint: "FILL_FROM_TASK_2",
        AccountsEndpoint: "FILL_FROM_TASK_2");
}
```

(These are the only intentionally-deferred values in the plan; they are populated from Task 2's documented findings, not invented.)

- [ ] **Step 3: Write the harness flow**

`src/OsrsLauncher.Harness/Program.cs`:

```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using OsrsLauncher.Core.Auth;
using OsrsLauncher.Core.Launch;
using OsrsLauncher.Core.Session;
using OsrsLauncher.Harness;

var http = new HttpClient();
var verifier = Pkce.GenerateVerifier();
var challenge = Pkce.CreateChallenge(verifier);
var state = Guid.NewGuid().ToString("N");
var nonce = Guid.NewGuid().ToString("N");

var authUrl = AuthorizeUrlBuilder.Build(JagexEndpoints.OAuth, challenge, state, nonce);
Console.WriteLine("Opening the Jagex login page in your browser...");
Process.Start(new ProcessStartInfo("open", $"\"{authUrl}\"") { UseShellExecute = false });

Console.WriteLine("After logging in, paste the full redirected URL (or just the ?code= value):");
var pasted = Console.ReadLine() ?? "";
var code = ExtractCode(pasted);

var oauth = new OAuthClient(http, JagexEndpoints.OAuth);
var tokens = await oauth.ExchangeCodeAsync(code, verifier);
Console.WriteLine("Got tokens.");

var sessions = new GameSessionClient(http, JagexEndpoints.Session);
var session = await sessions.CreateSessionAsync(tokens.IdToken
    ?? throw new InvalidOperationException("No id_token returned."));
var characters = await sessions.ListCharactersAsync(session);

for (var i = 0; i < characters.Count; i++)
    Console.WriteLine($"[{i}] {characters[i].DisplayName}");
Console.Write("Pick a character index: ");
var pick = int.Parse(Console.ReadLine() ?? "0");

var launcher = new RuneLiteLauncher(new ProcessRunner());
launcher.Launch(new RuneLiteLaunchInputs(session, characters[pick], tokens));
Console.WriteLine("Launched RuneLite. Confirm it is logged into the chosen character.");

static string ExtractCode(string pasted)
{
    pasted = pasted.Trim();
    if (Uri.TryCreate(pasted, UriKind.Absolute, out var uri) && uri.Query.Length > 1)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && kv[0] == "code")
                return Uri.UnescapeDataString(kv[1]);
        }
    }
    return pasted; // user pasted the bare code
}
```

`ExtractCode` is dependency-free (no `System.Web` reference needed): it reads the `code` query parameter from a pasted redirect URL, or returns the pasted text if the user pasted the bare code. If the Task 1 spike succeeded, you may instead reuse its WebView here to capture the `code` automatically rather than pasting it.

- [ ] **Step 4: Build the harness**

Run: `dotnet build src/OsrsLauncher.Harness`
Expected: build succeeds once Task 2 values are filled in.

- [ ] **Step 5: Write the manual test checklist**

`docs/manual-tests/first-authenticated-launch.md`: document the exact run steps, what a correct redirect URL looks like, the expected console output at each stage, and the pass criterion ("RuneLite opens already logged into the selected character"). Note any deviation from the expected JSON shapes so Tasks 5/6 can be corrected.

- [ ] **Step 6: Run the live integration test**

Run: `dotnet run --project src/OsrsLauncher.Harness`
Follow `docs/manual-tests/first-authenticated-launch.md`. Requires RuneLite installed at the default path and a real Jagex account.
Expected: RuneLite launches authenticated. **This is the Phase 1 milestone.**

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: console harness achieving first authenticated RuneLite launch"
```

---

## Self-review (completed during planning)

- **Spec coverage:** OAuth login (Tasks 3–5), game session + characters (Task 6), `JX_*` launch (Task 7), end-to-end authenticated launch (Task 8), WebView risk (Task 1 spike), constants/`JX_*` verification (Tasks 2 + 8), security (no token logging; harness keeps tokens in memory only). Deferred to roadmap: keychain persistence + fast path (spec §9.1, §6.2), character-picker UI (§9.2), RuneLite-path settings UI (§9.4), packaging (§12) — these are GUI/persistence phases gated on the spike.
- **Placeholder scan:** the only intentional deferrals are the `JagexEndpoints` constants, which are populated from Task 2's documented, sourced findings (not invented), and the spike's exact WebView API (the unknown the spike exists to resolve). All TDD tasks contain complete code.
- **Type consistency:** `JagexOAuthConfig`, `OAuthTokens`, `OAuthClient`, `GameSession`, `JagexCharacter`, `GameSessionConfig`, `GameSessionClient`, `RuneLiteLaunchInputs`, `IProcessRunner`, `RuneLiteLauncher`, `RuneLiteNotFoundException` are used identically across tasks. `StubHttpMessageHandler` is shared by Tasks 5 and 6.

---

## Roadmap — follow-on plans (to be detailed after Phase 1)

Each becomes its own plan via `superpowers:writing-plans` once the Task 1 spike result is known.

- **Phase 2 — Persistence + fast path:** `ICredentialStore` interface + in-memory impl (TDD), then a macOS Keychain implementation via `Security.framework` P/Invoke (`SecItemAdd`/`SecItemCopyMatching`). Orchestrator that, on startup, loads the stored refresh token, refreshes, and launches without UI; falls back to login on failure. (Spec §6.2, §9.1.)
- **Phase 3 — Avalonia GUI:** `OsrsLauncher.App` Avalonia project; `LoginWindow` hosting the WebView using the capture strategy chosen by the spike; `CharacterPicker` window (remember last choice); settings for the RuneLite path override. Replaces the console harness. (Spec §9.2, §9.4.)
- **Phase 4 — Packaging:** `dotnet publish -r osx-arm64`, bundle into `RuneLite-Jagex-Launcher.app` with `Info.plist` (and the `jagex:` URL scheme registration if Phase 3 uses it), ad-hoc code-sign, and a `scripts/package-macos.sh`. (Spec §12.)
