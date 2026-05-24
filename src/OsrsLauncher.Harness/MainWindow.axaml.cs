// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Threading;
using OsrsLauncher.Core.App;
using OsrsLauncher.Core.Launch;
using OsrsLauncher.Core.Persistence;
using OsrsLauncher.Core.Session;
using OsrsLauncher.Harness.Views;

namespace OsrsLauncher.Harness;

/// <summary>
/// Navigation shell. Owns shared services and swaps Root.Content between
/// HomeView, LoginView, and CharacterPickerView.
/// </summary>
public partial class MainWindow : Window
{
    private readonly KeychainCredentialStore _store  = new();
    private readonly RuneLiteLauncher        _launcher = new(new ProcessRunner());

    public MainWindow()
    {
        InitializeComponent();
        ShowHome();
    }

    // ── Navigation API (called by views) ────────────────────────────────────

    /// <summary>Show the home view, optionally displaying a status message.</summary>
    public void ShowHome(string? statusMessage = null)
    {
        var view = new HomeView(this, _store, _launcher);
        if (!string.IsNullOrEmpty(statusMessage))
            view.SetStatus(statusMessage, isError: true);
        Root.Content = view;
    }

    /// <summary>Navigate to the login WebView (full re-login / switch account).</summary>
    public void ShowLogin()
    {
        var loginView = new LoginView();

        loginView.Succeeded += (session, accounts) =>
        {
            Dispatcher.UIThread.Post(() => ShowPicker(session, accounts));
        };

        loginView.Failed += msg =>
        {
            Dispatcher.UIThread.Post(() => ShowHome(msg));
        };

        Root.Content = loginView;
    }

    /// <summary>Navigate to the character picker, then persist the FULL list + return home on selection.</summary>
    public void ShowPicker(GameSession session, IReadOnlyList<JagexCharacter> accounts)
    {
        var selectable = CharacterFilter.Selectable(accounts);

        Root.Content = new CharacterPickerView(selectable, chosen =>
        {
            _store.Save(new StoredSession(session.SessionId, selectable, chosen.AccountId));
            Dispatcher.UIThread.Post(() => ShowHome());
        });
    }

    /// <summary>
    /// Show the character picker built from the stored session — no re-login required.
    /// Selecting a character updates SelectedAccountId in the stored session and returns home.
    /// </summary>
    public void ShowStoredCharacterPicker()
    {
        var vm = new HomeViewModel(_store, _launcher);
        Root.Content = new CharacterPickerView(vm.Characters, chosen =>
        {
            vm.SelectCharacter(chosen.AccountId);
            Dispatcher.UIThread.Post(() => ShowHome());
        });
    }
}
