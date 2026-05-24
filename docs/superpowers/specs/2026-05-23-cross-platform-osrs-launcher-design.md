# Cross-Platform OSRS Launcher — Design Spec

- **Date:** 2026-05-23
- **Status:** Design approved; implementation plan pending
- **Repo:** `cross_platform_osrs_launcher`

## 1. Problem

The Jagex Launcher is the supported way to obtain Jagex-account login
credentials for Old School RuneScape / RuneLite. On macOS it ships **only as an
Intel binary (Rosetta 2)** — there is no native Apple Silicon option for this
login method.

## 2. Goal

A native Apple Silicon desktop application that:

1. Performs the Jagex-account OAuth login (including 2FA) in a system webview.
2. Obtains a game session and the account's character list.
3. Launches RuneLite with the `JX_*` environment variables it reads, fully
   authenticated.
4. Optionally remembers the login for one-click relaunch.

Built in **C# / .NET with Avalonia 12** so it is cross-platform-capable.
**macOS / Apple Silicon is the only v1 build, test, and release target.**

This is **not** a port of [Bolt](https://github.com/Adamcake/Bolt). Bolt is a
full CEF + X11 launcher; porting its GUI shell to macOS is large and unnecessary
for this goal. This project is a fresh, minimal implementation of the
login→launch path only.

## 3. Non-Goals (v1)

- No RS3 and no official RuneScape client launching.
- No game-file download / update / extraction.
- No plugin system (Bolt's Lua plugin API is out of scope).
- No Windows / Linux **release**. Code stays portable via Avalonia + abstracted
  platform services, but only macOS is built/tested/released in v1. (Linux is
  already served by the community launchers below; Windows has the native Jagex
  Launcher.)

## 4. Authoritative References

The Jagex OAuth flow and the `JX_*` contract are already reverse-engineered by
the community. Exact endpoint URLs, `client_id`, scopes, redirect URI, PKCE
parameters, and variable names will be **transcribed from these sources and
verified by a live login test** during implementation — not guessed:

- [melxin/native-linux-jagex-launcher](https://github.com/melxin/native-linux-jagex-launcher) (Rust) — OAuth + session flow
- [aitoiaita/linux-jagex-launcher](https://github.com/aitoiaita/linux-jagex-launcher) (Rust)
- [Kompreya/Runelite-Sans-Jagex-Launcher](https://github.com/Kompreya/Runelite-Sans-Jagex-Launcher) — launch scripts
- [RuneLite source + wiki "Using Jagex Accounts"](https://github.com/runelite/runelite/wiki/Using-Jagex-Accounts) — the consuming side (`JX_*` vars, `credentials.properties`, `--insecure-write-credentials`)

## 5. Architecture

Each unit has one purpose, communicates through a defined interface, and is
testable in isolation.

| Component | Responsibility | Depends on | Tested by |
|---|---|---|---|
| `OAuthClient` (pure logic) | Build authorize URL w/ PKCE + state; exchange `code`→tokens; refresh tokens | `HttpClient`, PKCE helper | Unit (mock HTTP) |
| `GameSessionClient` (pure logic) | Create Jagex game session; list account characters | `HttpClient` | Unit (mock HTTP) |
| `CredentialStore` (interface + per-OS impl) | Securely persist refresh token + last character | macOS Keychain (Win DPAPI / Linux libsecret later) | Unit (in-memory impl) + manual |
| `RuneLiteLauncher` (pure logic) | Locate RuneLite; spawn it with `JX_*` env | injectable process runner | Unit (fake runner) |
| `LoginWindow` (Avalonia UI) | Host native `WebView`; capture OAuth redirect; hand `code` to `OAuthClient` | `OAuthClient` | Manual / integration |
| `CharacterPicker` (Avalonia UI) | Pick character when >1; remember last | `CredentialStore` | Manual |
| `App` orchestrator | Wire the flows below | all of the above | Manual |

### Proposed project layout

- `src/OsrsLauncher.Core/` — pure logic (`OAuthClient`, `GameSessionClient`, `RuneLiteLauncher`, models). No UI, no platform deps.
- `src/OsrsLauncher.Platform/` — `CredentialStore` interface + per-OS implementations (macOS Keychain first).
- `src/OsrsLauncher.App/` — Avalonia UI (`LoginWindow`, `CharacterPicker`, `App`).
- `tests/OsrsLauncher.Core.Tests/` — unit tests for the pure-logic units.

## 6. Flows

### 6.1 First run

`LoginWindow` (webview) → user authenticates (+2FA) → capture `code` →
`OAuthClient` exchanges for tokens → `GameSessionClient` creates session + lists
characters → `CharacterPicker` (if >1) → `CredentialStore.Save(refresh token +
chosen character)` → `RuneLiteLauncher.Launch(JX_*)` → **authenticated RuneLite
opens.** ← the "it works" milestone.

### 6.2 Subsequent runs (fast path)

App start → `CredentialStore.Load()` → `OAuthClient.Refresh()` →
`GameSessionClient` session → `RuneLiteLauncher.Launch()` → done, **no login
UI**.

### 6.3 Fallback

If the stored refresh token is expired/revoked, the fast path fails over to the
full `LoginWindow` flow (6.1).

## 7. The `JX_*` Contract

RuneLite reads Jagex-account credentials from environment variables. Target set
(**to verify against RuneLite source**): `JX_SESSION_ID`, `JX_CHARACTER_ID`,
`JX_DISPLAY_NAME`, and the launcher-set `JX_ACCESS_TOKEN`, `JX_REFRESH_TOKEN`.
`RuneLiteLauncher` sets these on the child process environment
(`ProcessStartInfo.Environment`) and execs the RuneLite binary directly
(`open -a` does not reliably pass custom env vars on macOS).

## 8. Key Technical Risk — Redirect Capture

Jagex's flow ends in a **custom-scheme (`jagex:`) redirect**, not a plain URL
(it bounces through `https://secure.runescape.com/...launcher-redirect` first).
Capture strategy, in order of preference:

1. **Intercept in the Avalonia `WebView`'s navigation event** — cancel the
   `jagex:` / intermediate navigation and parse `code` / `state`. Cleanest.
2. **Register the OS-level `jagex:` protocol handler** (macOS Info.plist
   `CFBundleURLTypes`) and receive the callback. Also works with the external
   system browser.
3. **Localhost loopback catcher** — only if a suitable `client_id` permits a
   `http://localhost` redirect URI.

⚠️ **De-risk this first** with a throwaway spike (Section 13, step 1): if the
Avalonia WebView on macOS cannot cleanly capture the `code`, the approach shifts
to (2)/(3). Note: WKWebView blocks auto-handling of non-http(s) schemes, so the
navigation-event interception must read the URL and cancel rather than letting
it load.

## 9. Decisions (confirmed)

1. **Persist login** — store the refresh token in the OS keychain for one-click
   relaunch. (Makes this a real Jagex-Launcher replacement.)
2. **Character picker** — shown when the account has >1 OSRS character; remember
   the last choice; allow switching.
3. **Fast-path auto-launch** — on startup, if stored creds refresh OK, launch
   RuneLite immediately; show the full login UI only when needed.
4. **RuneLite location** — default `/Applications/RuneLite.app/Contents/MacOS/RuneLite`,
   auto-detected, with a manual override.

## 10. Security

- The refresh token grants full account access (bypasses password) → keychain
  only, never logged, never written in plaintext.
- Login + 2FA happen inside the system WebView (WebKit); the app never handles
  the user's password.
- `.gitignore` blocks `credentials.properties` and `*.secret` from ever being
  committed.

## 11. Testing Strategy

- **TDD the pure-logic units**: PKCE generation + authorize-URL building, token
  exchange/refresh parsing, session/character parsing, `JX_*` env construction —
  all with mocked HTTP / fake process runner.
- **Manual integration test** for the WebView capture and the live OAuth flow
  (human login + 2FA cannot be fully automated). Maintain a written manual-test
  checklist.
- **Walking skeleton first** (Section 13).

## 12. Packaging & Distribution

- macOS: `dotnet publish -r osx-arm64` + bundle into a `.app`; **ad-hoc
  code-sign** for personal use (Developer ID + notarization only if
  distributing). A small bundling script lives in the repo.
- Cross-platform packaging (Windows/Linux) is a later, optional step.

## 13. Build Order (vertical slices)

1. **Spike** — prove the Avalonia WebView captures the redirect `code` on macOS
   (de-risks Section 8).
2. `OAuthClient` — real constants from references; verified end-to-end to obtain
   tokens.
3. `GameSessionClient` — session + character list.
4. `RuneLiteLauncher` — spawn with `JX_*` → **first authenticated RuneLite
   launch.**
5. `CredentialStore` (macOS Keychain) + fast-path relaunch.
6. `CharacterPicker` + settings / RuneLite-path UI.
7. macOS `.app` packaging + ad-hoc signing.

## 14. Verification Tasks for Implementation

These are concrete lookups with designated sources — resolved during
implementation, not open design questions:

- Exact OAuth constants: authorize/token endpoints, `client_id`, scopes,
  redirect URI, PKCE parameters (from references in Section 4; verify by live
  test).
- Exact `JX_*` set RuneLite consumes (from RuneLite source).
- Spike outcome for WebView redirect capture (Section 8).
- Confirm Avalonia 12 + its `WebView` package run on the installed **.NET 8**
  (8.0.125), or whether .NET 9 is required.
