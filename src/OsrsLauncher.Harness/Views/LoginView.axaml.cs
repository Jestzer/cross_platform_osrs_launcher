// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using OsrsLauncher.Core.Session;

namespace OsrsLauncher.Harness.Views;

public partial class LoginView : UserControl
{
    public event Action<GameSession, IReadOnlyList<JagexCharacter>>? Succeeded;
    public event Action<string>? Failed;

    public LoginView()
    {
        InitializeComponent();

        var flow = new JagexLoginFlow();

        flow.Succeeded += (session, characters) => Succeeded?.Invoke(session, characters);
        flow.Failed    += msg =>
        {
            Failed?.Invoke(msg);
            Dispatcher.UIThread.Post(() =>
            {
                var label = this.FindControl<TextBlock>("StatusLabel")!;
                label.IsVisible  = true;
                label.Foreground = Avalonia.Media.Brushes.Red;
                label.Text       = msg;
            });
        };

        // Start is called from OnAttachedToVisualTree to ensure the WebView is in the tree.
        _flow = flow;
    }

    private readonly JagexLoginFlow _flow;
    private bool _started;

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_started)
        {
            _started = true;
            var webView = this.FindControl<NativeWebView>("WebView")!;

            var statusLabel = this.FindControl<TextBlock>("StatusLabel")!;
            statusLabel.IsVisible  = true;
            statusLabel.Foreground = Avalonia.Media.Brushes.Gray;
            statusLabel.Text       = "Logging in…";

            _flow.Start(webView);
        }
    }
}
