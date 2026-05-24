// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Avalonia.Controls;

namespace WebViewSpike;

/// <summary>
/// Spike: Does Avalonia NativeWebView fire NavigationStarted for an unregistered custom URL
/// scheme redirect, and can it be cancelled?  Run this app and look for [NAV:*] / [CAPTURED]
/// lines in stdout.
///
/// Discovered NativeWebView navigation API (confirmed via reflection on net8.0 DLL):
///   NavigationStarted   += EventHandler&lt;WebViewNavigationStartingEventArgs&gt;
///     args.Request  : Uri   (the target URL)
///     args.Cancel   : bool  (settable — set true to block navigation)
///   NavigationCompleted += EventHandler&lt;WebViewNavigationCompletedEventArgs&gt;
///     args.Request  : Uri
///     args.IsSuccess: bool
///   NewWindowRequested  += EventHandler&lt;WebViewNewWindowRequestedEventArgs&gt;
///     args.Request  : Uri
///     args.Handled  : bool (settable)
///   WebMessageReceived  += EventHandler&lt;WebMessageReceivedEventArgs&gt;
///     args.Body     : string
///   WebResourceRequested+= EventHandler&lt;WebResourceRequestedEventArgs&gt;
///     args.Request  : WebViewWebResourceRequest
///       .Uri        : Uri
///   AdapterCreated      += EventHandler&lt;WebViewAdapterEventArgs&gt;
///   AdapterDestroyed    += EventHandler&lt;WebViewAdapterEventArgs&gt;
/// </summary>
public partial class MainWindow : Window
{
    private const string SpikeHtml =
        """
        <!doctype html><html><body><h2>WebView spike</h2>
        <script>setTimeout(function(){ window.location.href='testscheme:callback?code=SPIKE123&state=abc'; }, 500);</script>
        </body></html>
        """;

    public MainWindow()
    {
        InitializeComponent();

        // Wire up all navigation events BEFORE setting Source.
        WebView.NavigationStarted    += OnNavigationStarted;
        WebView.NavigationCompleted  += OnNavigationCompleted;
        WebView.NewWindowRequested   += OnNewWindowRequested;
        WebView.WebMessageReceived   += OnWebMessageReceived;
        WebView.WebResourceRequested += OnWebResourceRequested;
        WebView.AdapterCreated       += OnAdapterCreated;
        WebView.AdapterDestroyed     += OnAdapterDestroyed;

        // Write spike HTML to a temp file and point the WebView at it.
        var tmpFile = Path.Combine(Path.GetTempPath(), "webview-spike.html");
        File.WriteAllText(tmpFile, SpikeHtml);
        var fileUri = new Uri(tmpFile);

        WebView.Source = fileUri;

        Console.WriteLine($"[SPIKE] window shown; source set to {fileUri}");
    }

    // ── NavigationStarted ──────────────────────────────────────────────────
    // Type:    WebViewNavigationStartingEventArgs
    // .Request (Uri)  — the navigation target
    // .Cancel  (bool) — set true to block
    private void OnNavigationStarted(object? sender, Avalonia.Controls.WebViewNavigationStartingEventArgs e)
    {
        var url = e.Request?.ToString() ?? "(null)";
        Console.WriteLine($"[NAV:NavigationStarted] url={url}");

        if (url.StartsWith("testscheme:", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[CAPTURED] {url}");
            e.Cancel = true;
            Console.WriteLine("[SPIKE] Cancel=true set — navigation blocked.");
        }
    }

    // ── NavigationCompleted ────────────────────────────────────────────────
    // Type:       WebViewNavigationCompletedEventArgs
    // .Request    (Uri)
    // .IsSuccess  (bool)
    private void OnNavigationCompleted(object? sender, Avalonia.Controls.WebViewNavigationCompletedEventArgs e)
    {
        var url = e.Request?.ToString() ?? "(null)";
        Console.WriteLine($"[NAV:NavigationCompleted] url={url} isSuccess={e.IsSuccess}");
    }

    // ── NewWindowRequested ─────────────────────────────────────────────────
    // Type:     WebViewNewWindowRequestedEventArgs
    // .Request  (Uri)
    // .Handled  (bool, settable)
    private void OnNewWindowRequested(object? sender, Avalonia.Controls.WebViewNewWindowRequestedEventArgs e)
    {
        var url = e.Request?.ToString() ?? "(null)";
        Console.WriteLine($"[NAV:NewWindowRequested] url={url}");
    }

    // ── WebMessageReceived ─────────────────────────────────────────────────
    // Type:   WebMessageReceivedEventArgs
    // .Body   (string) — NOT .Message
    private void OnWebMessageReceived(object? sender, Avalonia.Controls.WebMessageReceivedEventArgs e)
    {
        Console.WriteLine($"[NAV:WebMessageReceived] body={e.Body}");
    }

    // ── WebResourceRequested ───────────────────────────────────────────────
    // Type:           WebResourceRequestedEventArgs
    // .Request        (WebViewWebResourceRequest)
    //   .Uri          (Uri)  — NOT .RequestUri
    private void OnWebResourceRequested(object? sender, Avalonia.Controls.WebResourceRequestedEventArgs e)
    {
        var url = e.Request?.Uri?.ToString() ?? "(null)";
        Console.WriteLine($"[NAV:WebResourceRequested] url={url}");
    }

    // ── AdapterCreated / AdapterDestroyed ──────────────────────────────────
    private void OnAdapterCreated(object? sender, Avalonia.Controls.WebViewAdapterEventArgs e)
    {
        Console.WriteLine("[SPIKE] AdapterCreated — platform WebView adapter is ready.");
    }

    private void OnAdapterDestroyed(object? sender, Avalonia.Controls.WebViewAdapterEventArgs e)
    {
        Console.WriteLine("[SPIKE] AdapterDestroyed.");
    }
}
