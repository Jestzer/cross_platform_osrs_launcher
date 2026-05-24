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
using OsrsLauncher.Core.Session;

namespace OsrsLauncher.Harness;

/// <summary>
/// Drives the two-leg Jagex OAuth login against a caller-supplied NativeWebView.
/// After accounts are fetched, raises <see cref="Succeeded"/> instead of persisting
/// or launching — the caller handles session storage and navigation.
/// On any failure, raises <see cref="Failed"/> with a safe message (no secrets).
/// </summary>
public sealed class JagexLoginFlow
{
    // ── Jagex OAuth constants ───────────────────────────────────────────────
    // Leg 1 — launcher client
    private const string AuthorizeEndpoint   = "https://account.jagex.com/oauth2/auth";
    private const string TokenEndpoint       = "https://account.jagex.com/oauth2/token";
    private const string LauncherClientId    = "com_jagex_auth_desktop_launcher";
    private const string LauncherRedirectUri = "https://secure.runescape.com/m=weblogin/launcher-redirect";
    private const string LauncherScope       = "openid offline gamesso.token.create user.profile.read";

    // Leg 2 — consent client
    private const string ConsentClientId    = "1fddee4e-b100-4f4e-b2b0-097f9088f9d2";
    private const string ConsentRedirectUri = "http://localhost";

    // Game session
    private const string SessionsEndpoint = "https://auth.jagex.com/game-session/v1/sessions";
    private const string AccountsEndpoint = "https://auth.jagex.com/game-session/v1/accounts";

    // ── Events ──────────────────────────────────────────────────────────────
    public event Action<GameSession, IReadOnlyList<JagexCharacter>>? Succeeded;
    public event Action<string>? Failed;

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

    private NativeWebView? _webView;

    public JagexLoginFlow()
    {
        var launcherConfig = new JagexOAuthConfig(
            AuthorizeEndpoint,
            TokenEndpoint,
            LauncherClientId,
            LauncherRedirectUri,
            LauncherScope);

        _oauthClient   = new OAuthClient(_http, launcherConfig);
        _sessionClient = new GameSessionClient(_http, new GameSessionConfig(SessionsEndpoint, AccountsEndpoint));
    }

    /// <summary>
    /// Begins leg 1; auto-advances through leg 2, session creation, and account listing.
    /// The webView must already be attached to a visible window.
    /// </summary>
    public void Start(NativeWebView webView)
    {
        _webView = webView;

        webView.NavigationStarted   += OnNavigationStarted;
        webView.NavigationCompleted += OnNavigationCompleted;
        webView.AdapterCreated      += (_, _) => Console.WriteLine("[HARNESS] WebView adapter ready.");

        _ = StartLeg1Async();
    }

    // ── Leg 1: navigate to launcher authorize ──────────────────────────────
    private Task StartLeg1Async()
    {
        _leg1Verifier = Pkce.GenerateVerifier(byteLength: 64);
        var challenge = Pkce.CreateChallenge(_leg1Verifier);

        _leg1State = GenerateRandomBase64Url(16);
        var nonce  = GenerateRandomBase64Url(16);

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

        Dispatcher.UIThread.Post(() => _webView!.Source = new Uri(url));
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
        bool isLauncherRedirect = url.StartsWith(LauncherRedirectUri, StringComparison.OrdinalIgnoreCase);
        bool isJagexScheme      = url.StartsWith("jagex:", StringComparison.OrdinalIgnoreCase);

        if (!isLauncherRedirect && !isJagexScheme)
            return;

        if (isJagexScheme && !isLauncherRedirect)
        {
            Console.WriteLine("[HARNESS] WARNING: saw jagex: scheme redirect on leg 1 — capturing from it instead of launcher-redirect");
        }

        e.Cancel = true;

        var uri   = new Uri(url);
        var query = ParseQuery(uri.Query.TrimStart('?'));

        if (!query.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
        {
            Console.WriteLine("[HARNESS][ERROR] leg-1 redirect arrived but no 'code' query param found. URL keys: " +
                              string.Join(", ", query.Keys));
            Failed?.Invoke("Login failed: no authorization code in redirect.");
            return;
        }

        Console.WriteLine($"[2] captured leg-1 code (len={code.Length}) from {(isJagexScheme ? "jagex: scheme" : "launcher-redirect")}");

        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessLeg1CodeAsync(code);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HARNESS][ERROR] leg-1 processing failed at stage: {ex.Message}");
                Failed?.Invoke($"Login failed: {ex.Message}");
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
            Failed?.Invoke($"Token exchange failed: {ex.Message}");
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
            Failed?.Invoke("Legacy RuneScape accounts are not supported. Please use a Jagex account.");
            return;
        }

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

            var payload = parts[1];
            var padded  = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=')
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
        _leg2State = GenerateRandomBase64Url(16);
        _leg2Nonce = GenerateRandomBase64Url(16);

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

        Dispatcher.UIThread.Post(() => _webView!.Source = new Uri(url));
        return Task.CompletedTask;
    }

    // ── Leg 2 redirect handler ─────────────────────────────────────────────
    private void HandleLeg2Redirect(Avalonia.Controls.WebViewNavigationStartingEventArgs e, string url)
    {
        if (!url.StartsWith(ConsentRedirectUri, StringComparison.OrdinalIgnoreCase))
            return;

        e.Cancel = true;
        _stage = LoginStage.Idle;

        Uri uri;
        try { uri = new Uri(url); }
        catch
        {
            Console.WriteLine($"[HARNESS][ERROR] leg-2 redirect URL could not be parsed: len={url.Length}");
            Failed?.Invoke("Login failed: could not parse consent redirect URL.");
            return;
        }

        var fragment    = uri.Fragment;
        bool hasFragment = !string.IsNullOrEmpty(fragment) && fragment.Length > 1;
        var fragContent = hasFragment ? fragment.TrimStart('#') : string.Empty;
        var fragParams  = hasFragment ? ParseQuery(fragContent) : new Dictionary<string, string>();

        var queryParams = ParseQuery(uri.Query.TrimStart('?'));

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
            Failed?.Invoke("Login failed: consent step did not return an id_token.");
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
                Failed?.Invoke($"Session creation failed: {ex.Message}");
            }
        });
    }

    // ── Game session + character listing ──────────────────────────────────
    private async Task ProcessLeg2Async(string leg2IdToken)
    {
        GameSession session;
        try
        {
            session = await _sessionClient.CreateSessionAsync(leg2IdToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HARNESS][ERROR] CreateSessionAsync failed: {ex.Message}");
            Failed?.Invoke($"Session creation failed: {ex.Message}");
            return;
        }

        Console.WriteLine($"[7] session created (sessionId len={session.SessionId.Length})");

        IReadOnlyList<JagexCharacter> characters;
        try
        {
            characters = await _sessionClient.ListCharactersAsync(session);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HARNESS][ERROR] ListCharactersAsync failed: {ex.Message}");
            Failed?.Invoke($"Account listing failed: {ex.Message}");
            return;
        }

        var names = string.Join(", ", characters.Select(c => c.DisplayName ?? $"(no displayName, accountId len={c.AccountId.Length})"));
        Console.WriteLine($"[8] accounts: {characters.Count} -> [{names}]");

        // Raise success — caller handles character selection, persistence, and launch.
        Succeeded?.Invoke(session, characters);
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
