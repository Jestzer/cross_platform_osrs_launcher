// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using OsrsLauncher.Core.Auth;
using OsrsLauncher.Core.Launch;
using OsrsLauncher.Core.Session;

namespace OsrsLauncher.Harness;

/// <summary>
/// Task 8 — WebView-driven two-leg Jagex OAuth harness.
///
/// Flow overview:
///   Leg 1 (Launcher client):
///     Navigate WebView → https://account.jagex.com/oauth2/auth (PKCE S256)
///     Intercept redirect to https://secure.runescape.com/m=weblogin/launcher-redirect
///     Exchange code → OAuthTokens (access + refresh + id_token)
///     Decode id_token to read login_provider claim
///
///   Leg 2 (Consent client, Jagex-account path only):
///     Navigate WebView → https://account.jagex.com/oauth2/auth with consent client_id
///     Intercept redirect to http://localhost  — result arrives in the URL fragment (#)
///     Extract id_token (and code) from fragment key=value pairs
///
///   Game session:
///     POST /game-session/v1/sessions with leg-2 id_token
///     GET  /game-session/v1/accounts  with session Bearer token
///
///   Launch RuneLite via RuneLiteLauncher with JX_ env vars.
///
/// SAFE LOGGING CONTRACT (see [n] markers):
///   Never print tokens, codes, verifiers, sessionId values, or fragment values.
///   Log only: lengths, presence booleans, the login_provider string, display names (not secret),
///   the resolved RuneLite path, and exception messages.
/// </summary>
public partial class MainWindow : Window
{
    // ── Jagex OAuth constants ───────────────────────────────────────────────
    // Leg 1 — launcher client
    private const string AuthorizeEndpoint     = "https://account.jagex.com/oauth2/auth";
    private const string TokenEndpoint         = "https://account.jagex.com/oauth2/token";
    private const string LauncherClientId      = "com_jagex_auth_desktop_launcher";
    private const string LauncherRedirectUri   = "https://secure.runescape.com/m=weblogin/launcher-redirect";
    private const string LauncherScope         = "openid offline gamesso.token.create user.profile.read";

    // Leg 2 — consent client
    private const string ConsentClientId       = "1fddee4e-b100-4f4e-b2b0-097f9088f9d2";
    private const string ConsentRedirectUri    = "http://localhost";
    // response_type for implicit-hybrid consent step: "id_token code"
    // (no PKCE on leg 2 — the reference implementations confirm this)

    // Game session
    private const string SessionsEndpoint      = "https://auth.jagex.com/game-session/v1/sessions";
    private const string AccountsEndpoint      = "https://auth.jagex.com/game-session/v1/accounts";

    // ── Per-login ephemeral state ───────────────────────────────────────────
    private enum LoginStage { Idle, Leg1, Leg2 }
    private LoginStage _stage = LoginStage.Idle;

    private string? _leg1Verifier;
    private string? _leg1State;

    private string? _leg2State;
    private string? _leg2Nonce;

    private OAuthTokens? _leg1Tokens;

    // ── Services ───────────────────────────────────────────────────────────
    private readonly HttpClient _http = new();
    private readonly OAuthClient _oauthClient;
    private readonly GameSessionClient _sessionClient;
    private readonly RuneLiteLauncher _launcher;

    public MainWindow()
    {
        InitializeComponent();

        // Wire services
        var launcherConfig = new JagexOAuthConfig(
            AuthorizeEndpoint,
            TokenEndpoint,
            LauncherClientId,
            LauncherRedirectUri,
            LauncherScope);

        _oauthClient    = new OAuthClient(_http, launcherConfig);
        _sessionClient  = new GameSessionClient(_http, new GameSessionConfig(SessionsEndpoint, AccountsEndpoint));
        _launcher       = new RuneLiteLauncher(new ProcessRunner());

        // Wire WebView navigation events BEFORE setting Source
        WebView.NavigationStarted   += OnNavigationStarted;
        WebView.NavigationCompleted += OnNavigationCompleted;
        WebView.AdapterCreated      += (_, _) => Console.WriteLine("[HARNESS] WebView adapter ready.");

        // Kick off leg 1
        _ = StartLeg1Async();
    }

    // ── Leg 1: navigate to launcher authorize ──────────────────────────────
    private Task StartLeg1Async()
    {
        // Generate PKCE verifier (64-byte = 86-char base64url, matching the reference)
        _leg1Verifier = Pkce.GenerateVerifier(byteLength: 64);
        var challenge = Pkce.CreateChallenge(_leg1Verifier);

        // Random state (CSRF token) — 16 random bytes → 22-char base64url
        _leg1State = GenerateRandomBase64Url(16);

        // Random nonce — 16 random bytes
        var nonce = GenerateRandomBase64Url(16);

        var launcherConfig = new JagexOAuthConfig(
            AuthorizeEndpoint,
            TokenEndpoint,
            LauncherClientId,
            LauncherRedirectUri,
            LauncherScope);

        var url = AuthorizeUrlBuilder.Build(launcherConfig, challenge, _leg1State, nonce);

        _stage = LoginStage.Leg1;
        Console.WriteLine("[1] navigating to launcher authorize URL (leg 1)");
        Console.WriteLine($"    client_id={LauncherClientId}");
        Console.WriteLine($"    redirect_uri={LauncherRedirectUri}");
        Console.WriteLine($"    scope={LauncherScope}");
        Console.WriteLine($"    response_type=code  code_challenge_method=S256");

        // Must set Source on the UI thread
        Dispatcher.UIThread.Post(() => WebView.Source = new Uri(url));
        return Task.CompletedTask;
    }

    // ── NavigationStarted — shared interceptor ─────────────────────────────
    private void OnNavigationStarted(object? sender, Avalonia.Controls.WebViewNavigationStartingEventArgs e)
    {
        var url = e.Request?.ToString() ?? string.Empty;

        switch (_stage)
        {
            case LoginStage.Leg1:
                HandleLeg1Redirect(e, url);
                break;

            case LoginStage.Leg2:
                HandleLeg2Redirect(e, url);
                break;
        }
    }

    private void OnNavigationCompleted(object? sender, Avalonia.Controls.WebViewNavigationCompletedEventArgs e)
    {
        // Informational only — not logged in normal flow to keep output clean
    }

    // ── Leg 1 redirect handler ─────────────────────────────────────────────
    private void HandleLeg1Redirect(Avalonia.Controls.WebViewNavigationStartingEventArgs e, string url)
    {
        // Primary capture: the HTTPS launcher-redirect URI
        bool isLauncherRedirect = url.StartsWith(LauncherRedirectUri, StringComparison.OrdinalIgnoreCase);

        // Fallback: jagex: custom scheme (log and handle if seen)
        bool isJagexScheme = url.StartsWith("jagex:", StringComparison.OrdinalIgnoreCase);

        if (!isLauncherRedirect && !isJagexScheme)
            return;

        if (isJagexScheme && !isLauncherRedirect)
        {
            Console.WriteLine("[HARNESS] WARNING: saw jagex: scheme redirect on leg 1 — capturing from it instead of launcher-redirect");
        }

        e.Cancel = true;

        var uri = new Uri(url);
        var query = ParseQuery(uri.Query.TrimStart('?'));

        if (!query.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
        {
            Console.WriteLine("[HARNESS][ERROR] leg-1 redirect arrived but no 'code' query param found. URL keys: " +
                              string.Join(", ", query.Keys));
            return;
        }

        Console.WriteLine($"[2] captured leg-1 code (len={code.Length}) from {(isJagexScheme ? "jagex: scheme" : "launcher-redirect")}");

        // Run async work off the UI thread
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessLeg1CodeAsync(code);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HARNESS][ERROR] leg-1 processing failed at stage: {ex.Message}");
            }
        });
    }

    // ── Process leg-1 code: token exchange + decode id_token ──────────────
    private async Task ProcessLeg1CodeAsync(string code)
    {
        Console.WriteLine("[HARNESS] exchanging leg-1 code for tokens...");
        OAuthTokens tokens;
        try
        {
            tokens = await _oauthClient.ExchangeCodeAsync(code, _leg1Verifier!);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HARNESS][ERROR] token exchange failed: {ex.Message}");
            return;
        }

        _leg1Tokens = tokens;

        var idTokenPresent = !string.IsNullOrEmpty(tokens.IdToken);
        string loginProvider = "(unknown)";

        if (idTokenPresent && tokens.IdToken is not null)
        {
            loginProvider = DecodeLoginProviderClaim(tokens.IdToken);
        }

        Console.WriteLine($"[3] token exchange OK; id_token present={idTokenPresent}; login_provider={loginProvider}");

        if (loginProvider == "runescape")
        {
            Console.WriteLine("[HARNESS] login_provider=runescape — legacy RS account path is NOT implemented in this harness. Stopping gracefully.");
            Console.WriteLine("          To support legacy accounts, implement the JX_ACCESS_TOKEN / JX_REFRESH_TOKEN path.");
            return;
        }

        // Jagex account path — proceed to leg 2
        await StartLeg2Async();
    }

    // ── Decode JWT middle segment to extract login_provider ───────────────
    private static string DecodeLoginProviderClaim(string idToken)
    {
        try
        {
            var parts = idToken.Split('.');
            if (parts.Length < 2)
                return "(malformed JWT)";

            // Base64url decode — pad to multiple of 4
            var payload = parts[1];
            var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=')
                                .Replace('-', '+').Replace('_', '/');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("login_provider", out var prop))
                return prop.GetString() ?? "(null)";

            return "(claim absent)";
        }
        catch (Exception ex)
        {
            return $"(decode error: {ex.Message})";
        }
    }

    // ── Leg 2: navigate to consent authorize ──────────────────────────────
    private Task StartLeg2Async()
    {
        // leg-2 uses a fresh state and nonce; no PKCE (implicit flow per reference)
        _leg2State = GenerateRandomBase64Url(16);
        _leg2Nonce = GenerateRandomBase64Url(16);

        // Manually build the leg-2 URL — consent client uses response_type "id_token code"
        // with implicit flow (no PKCE). Parameters mirror consent_client.rs exactly.
        // The reference passes:
        //   response_type = "id_token code"
        //   client_id     = CONSENT_CLIENT_ID
        //   redirect_uri  = "http://localhost"
        //   scope         = "openid offline"  (consent step needs openid; "offline" aligns with reference)
        //   nonce         = <random>
        //   state         = <random CSRF token>
        //
        // NOTE: The reference does NOT pass id_token_hint or prompt on leg 2.
        // The leg-1 id_token is NOT forwarded here — the consent server identifies
        // the logged-in session via the browser's own cookie/session from leg 1.
        var queryParams = new Dictionary<string, string>
        {
            ["response_type"] = "id_token code",
            ["client_id"]     = ConsentClientId,
            ["redirect_uri"]  = ConsentRedirectUri,
            ["scope"]         = "openid offline",
            ["nonce"]         = _leg2Nonce,
            ["state"]         = _leg2State,
        };

        var encoded = string.Join("&", queryParams.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        var url = $"{AuthorizeEndpoint}?{encoded}";

        _stage = LoginStage.Leg2;
        Console.WriteLine("[4] navigating consent authorize URL (leg 2)");
        Console.WriteLine($"    client_id={ConsentClientId}");
        Console.WriteLine($"    response_type=id_token code   redirect_uri={ConsentRedirectUri}");
        Console.WriteLine($"    scope=openid offline   (no PKCE — implicit hybrid flow)");

        Dispatcher.UIThread.Post(() => WebView.Source = new Uri(url));
        return Task.CompletedTask;
    }

    // ── Leg 2 redirect handler ─────────────────────────────────────────────
    private void HandleLeg2Redirect(Avalonia.Controls.WebViewNavigationStartingEventArgs e, string url)
    {
        if (!url.StartsWith(ConsentRedirectUri, StringComparison.OrdinalIgnoreCase))
            return;

        e.Cancel = true;
        _stage = LoginStage.Idle;

        // The consent implicit flow returns params in the URL fragment (#key=value&...)
        // URI.Fragment includes the leading '#'. Trim it.
        Uri uri;
        try { uri = new Uri(url); }
        catch { Console.WriteLine($"[HARNESS][ERROR] leg-2 redirect URL could not be parsed: len={url.Length}"); return; }

        var fragment = uri.Fragment;
        bool hasFragment = !string.IsNullOrEmpty(fragment) && fragment.Length > 1;
        var fragContent = hasFragment ? fragment.TrimStart('#') : string.Empty;
        var fragParams  = hasFragment ? ParseQuery(fragContent) : new Dictionary<string, string>();

        // Also parse query params (some servers put params in query instead of fragment)
        var queryParams = ParseQuery(uri.Query.TrimStart('?'));

        // Merge: fragment takes priority
        var allParams = new Dictionary<string, string>(queryParams);
        foreach (var kv in fragParams) allParams[kv.Key] = kv.Value;

        var keysFound = string.Join(", ", allParams.Keys.OrderBy(k => k));
        Console.WriteLine($"[5] leg-2 redirect captured: host={uri.Host} hasFragment={hasFragment} keys=[{keysFound}]");

        var idTokenPresent = allParams.ContainsKey("id_token");
        var codePresent    = allParams.ContainsKey("code");
        Console.WriteLine($"[6] id_token in fragment present={idTokenPresent}; code present={codePresent}");

        if (!idTokenPresent)
        {
            Console.WriteLine("[HARNESS][ERROR] leg-2 id_token missing — cannot create game session. Keys found: " + keysFound);
            return;
        }

        var leg2IdToken = allParams["id_token"];

        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessLeg2Async(leg2IdToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HARNESS][ERROR] leg-2 processing failed: {ex.Message}");
            }
        });
    }

    // ── Game session + character + RuneLite launch ────────────────────────
    private async Task ProcessLeg2Async(string leg2IdToken)
    {
        // Create game session
        GameSession session;
        try
        {
            session = await _sessionClient.CreateSessionAsync(leg2IdToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HARNESS][ERROR] CreateSessionAsync failed: {ex.Message}");
            return;
        }

        Console.WriteLine($"[7] session created (sessionId len={session.SessionId.Length})");

        // List characters
        IReadOnlyList<OsrsLauncher.Core.Session.JagexCharacter> characters;
        try
        {
            characters = await _sessionClient.ListCharactersAsync(session);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HARNESS][ERROR] ListCharactersAsync failed: {ex.Message}");
            return;
        }

        var names = string.Join(", ", characters.Select(c => c.DisplayName ?? $"(no displayName, accountId len={c.AccountId.Length})"));
        Console.WriteLine($"[8] accounts: {characters.Count} -> [{names}]");

        if (characters.Count == 0)
        {
            Console.WriteLine("[HARNESS][ERROR] no characters returned — cannot launch.");
            return;
        }

        var character = characters[0];
        if (characters.Count > 1)
        {
            Console.WriteLine($"[HARNESS] multiple characters ({characters.Count}) — picking first for this harness; selection UI is future work.");
        }

        // Resolve RuneLite path and launch
        string runelitePath;
        try
        {
            runelitePath = _launcher.ResolveExecutablePath(overridePath: null);
        }
        catch (RuneLiteNotFoundException ex)
        {
            Console.WriteLine($"[HARNESS][ERROR] RuneLite not found: {ex.Message}");
            return;
        }

        Console.WriteLine($"[9] launching RuneLite at {runelitePath}");

        var inputs = new RuneLiteLaunchInputs(session, character, _leg1Tokens!);
        try
        {
            _launcher.Launch(inputs, overridePath: null);
            Console.WriteLine("[HARNESS] RuneLite launch invoked. Done.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HARNESS][ERROR] RuneLite launch failed: {ex.Message}");
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private static string GenerateRandomBase64Url(int byteLength)
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>Parse application/x-www-form-urlencoded or URL query string.</summary>
    private static Dictionary<string, string> ParseQuery(string raw)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
            return result;

        foreach (var pair in raw.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            if (idx < 0)
            {
                result[Uri.UnescapeDataString(pair)] = string.Empty;
            }
            else
            {
                var key   = Uri.UnescapeDataString(pair[..idx]);
                var value = Uri.UnescapeDataString(pair[(idx + 1)..]);
                result[key] = value;
            }
        }
        return result;
    }
}