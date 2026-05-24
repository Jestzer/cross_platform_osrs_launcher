# WebView Redirect-Capture Spike — Findings (Task 1)

- **Date:** 2026-05-24
- **Branch:** `phase-1-core`
- **Spike code:** `spikes/webview-redirect-spike/` (throwaway; not in `OsrsLauncher.sln`)
- **Run:** `dotnet run --project spikes/webview-redirect-spike`

## Question

Can Avalonia's native WebView, on macOS / Apple Silicon, **observe and cancel** a
navigation/redirect to an (unregistered) custom URL scheme — the mechanism a future
OAuth login UI needs to capture the auth `code` and stop the navigation?

## Answer: YES — confirmed by a real run on macOS

The spike loaded a local HTML page that JS-redirects to
`testscheme:callback?code=SPIKE123&state=abc`. Observed console output:

```
[SPIKE] window shown; source set to file:///.../webview-spike.html
[SPIKE] AdapterCreated — platform WebView adapter is ready.
[NAV:WebResourceRequested] url=file:///.../webview-spike.html
[NAV:NavigationStarted]     url=file:///.../webview-spike.html
[NAV:NavigationCompleted]   url=file:///.../webview-spike.html isSuccess=True
[NAV:WebResourceRequested] url=testscheme:callback?code=SPIKE123&state=abc
[NAV:NavigationStarted]     url=testscheme:callback?code=SPIKE123&state=abc
[CAPTURED] testscheme:callback?code=SPIKE123&state=abc
[SPIKE] Cancel=true set — navigation blocked.
```

So WKWebView fires `NavigationStarted` for an unregistered custom scheme, the full URL
(including the `code`) is readable, and `e.Cancel = true` blocks the navigation.

## Environment confirmed

- macOS / Apple Silicon, **.NET SDK 8.0.125** (no .NET 9 required).
- `Avalonia.Controls.WebView` **12.0.1** ships a `lib/net8.0/` target and builds cleanly
  against Avalonia 12.x on net8.0. *(Resolves the open §14 question: net8.0 is sufficient.)*

## Resolved WebView API (`NativeWebView`, namespace `Avalonia.Controls`)

Confirmed by reflecting `Avalonia.Controls.WebView.dll` (the published docs were partly wrong):

| Member | Type / properties |
|---|---|
| `Source` | `Uri` — set to navigate (use a `file://` URI to load local HTML) |
| `NavigationStarted` | args `WebViewNavigationStartingEventArgs`: `.Request` (Uri), **`.Cancel` (writable bool)** ← capture + block here |
| `NavigationCompleted` | args `WebViewNavigationCompletedEventArgs`: `.Request` (Uri), `.IsSuccess` (bool) |
| `NewWindowRequested` | args: `.Request` (Uri), `.Handled` (writable bool) |
| `WebResourceRequested` | args: `.Request.Uri` (Uri) — also fires for custom schemes |
| `WebMessageReceived` | args: `.Body` (string) |

- The event is `NavigationStarted` (NOT `NavigationStarting` as docs claim); the URL property
  is `.Request` (NOT `.Url`/`.Uri`).
- **No `AppBuilder` setup call needed** on the net8.0 target — `UsePlatformDetect()`
  auto-detects the WKWebView adapter (`UseNativeWebView()` does not exist in this target).

## Decision: Phase 3 capture strategy

**Primary (confirmed viable): in-WebView interception via `NavigationStarted` + `e.Cancel`.**
Host Jagex's login in a `NativeWebView`, watch `NavigationStarted`, and capture the `code`
from the redirect, canceling the navigation.

Two refinements to validate live in Task 8:

1. **Prefer intercepting the HTTPS `launcher-redirect` over the `jagex:` scheme.** Per
   `docs/reference/jagex-flow.md`, Jagex redirects to
   `https://secure.runescape.com/m=weblogin/launcher-redirect?...` *before* bouncing to
   `jagex:`. If the `code` is present on that HTTPS URL, we intercept there and never touch a
   custom scheme — simpler and avoids any OS handoff. Confirm the exact redirect chain live.
2. **Custom-scheme caveat:** we tested with `testscheme:` deliberately. If the user has the
   (Rosetta) Jagex Launcher installed, macOS may have `jagex:` registered to it, so an actual
   `jagex:` redirect could be handed to that app by the OS. `NavigationStarted` fires before
   that and we cancel, but this is a reason to prefer (1) or a scheme/redirect we control.

**Alternative to evaluate: `WebAuthenticationBroker.AuthenticateAsync(window, new WebAuthenticatorOptions { RequestUri, RedirectUri })` → `result.CallbackUri`.** This higher-level
helper (likely backed by Apple's `ASWebAuthenticationSession`) is purpose-built for OAuth and
may be the cleanest production path. It is hard to test self-contained; validate it against the
real Jagex flow in Task 8 and compare with the `NavigationStarted` approach.

## Bottom line

No blocker. The Avalonia 12 native WebView on net8.0/Apple Silicon can render the login page
and capture the OAuth redirect. Proceed to Task 8 (wire the live two-leg flow) using
`NavigationStarted` interception as the baseline, evaluating `WebAuthenticationBroker` as the
preferred alternative.
