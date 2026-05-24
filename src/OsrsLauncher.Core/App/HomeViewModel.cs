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

    private StoredSession? Session => _store.Load();

    public JagexCharacter? SelectedCharacter
    {
        get
        {
            var s = Session;
            return s?.Characters.FirstOrDefault(c => c.AccountId == s.SelectedAccountId);
        }
    }

    public bool IsLoggedIn => SelectedCharacter is not null;
    public string? CharacterName => SelectedCharacter?.DisplayName;
    public IReadOnlyList<JagexCharacter> Characters => Session?.Characters ?? Array.Empty<JagexCharacter>();
    public bool CanSwitchCharacter => Characters.Count > 1;

    public void SelectCharacter(string accountId)
    {
        var s = Session ?? throw new InvalidOperationException("No stored session.");
        if (s.Characters.All(c => c.AccountId != accountId))
            throw new ArgumentException($"Unknown character: {accountId}");
        _store.Save(s with { SelectedAccountId = accountId });
    }

    public void Play()
    {
        var s = Session ?? throw new InvalidOperationException("No stored session.");
        var c = SelectedCharacter ?? throw new InvalidOperationException("No character selected.");
        _launcher.LaunchJagexSession(new GameSession(s.SessionId), c);
    }
}
