// SPDX-License-Identifier: GPL-3.0-or-later
using OsrsLauncher.Core.Launch;
using OsrsLauncher.Core.Persistence;
using OsrsLauncher.Core.Session;

namespace OsrsLauncher.Core.App;

public sealed class HomeViewModel
{
    private readonly ICredentialStore _store;
    private readonly RuneLiteLauncher _launcher;

    public HomeViewModel(ICredentialStore store, RuneLiteLauncher launcher)
    {
        _store = store;
        _launcher = launcher;
    }

    public bool IsLoggedIn => _store.Load() is not null;
    public string? CharacterName => _store.Load()?.DisplayName;

    public void Play()
    {
        var s = _store.Load() ?? throw new InvalidOperationException("No stored session.");
        _launcher.LaunchJagexSession(new GameSession(s.SessionId), new JagexCharacter(s.AccountId, s.DisplayName));
    }
}
