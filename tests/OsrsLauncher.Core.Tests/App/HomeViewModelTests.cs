// SPDX-License-Identifier: GPL-3.0-or-later
using OsrsLauncher.Core.App;
using OsrsLauncher.Core.Launch;
using OsrsLauncher.Core.Persistence;
using OsrsLauncher.Core.Session;
using Xunit;

namespace OsrsLauncher.Core.Tests.App;

public class HomeViewModelTests
{
    private sealed class FakeRunner : IProcessRunner
    {
        public string? StartedPath;
        public IReadOnlyDictionary<string, string>? Env;
        public void Start(string p, IReadOnlyDictionary<string, string> e) { StartedPath = p; Env = e; }
    }

    private static HomeViewModel Make(ICredentialStore store, out FakeRunner runner)
    {
        runner = new FakeRunner();
        return new HomeViewModel(store, new RuneLiteLauncher(runner, fileExists: _ => true));
    }

    private static StoredSession TwoCharSession(string selectedAccountId = "ACC-1") =>
        new StoredSession(
            "SESS-7",
            new List<JagexCharacter> { new("ACC-1", "Jestzer"), new("ACC-2", "Hoppity9") },
            selectedAccountId);

    [Fact]
    public void EmptyStore_IsNotLoggedIn_AndNoCharacters()
    {
        var vm = Make(new InMemoryCredentialStore(), out _);
        Assert.False(vm.IsLoggedIn);
        Assert.Null(vm.CharacterName);
        Assert.Empty(vm.Characters);
    }

    [Fact]
    public void WithSession_SelectingAcc1_IsLoggedIn_ExposesJestzer_CanSwitchIsTrue()
    {
        var store = new InMemoryCredentialStore();
        store.Save(TwoCharSession("ACC-1"));
        var vm = Make(store, out _);

        Assert.True(vm.IsLoggedIn);
        Assert.Equal("Jestzer", vm.CharacterName);
        Assert.True(vm.CanSwitchCharacter);
        Assert.Equal(2, vm.Characters.Count);
    }

    [Fact]
    public void Play_LaunchesSelectedCharacter_Acc1()
    {
        var store = new InMemoryCredentialStore();
        store.Save(TwoCharSession("ACC-1"));
        var vm = Make(store, out var runner);

        vm.Play();

        Assert.Equal("SESS-7", runner.Env!["JX_SESSION_ID"]);
        Assert.Equal("ACC-1", runner.Env!["JX_CHARACTER_ID"]);
    }

    [Fact]
    public void SelectCharacter_ThenPlay_UsesNewSelection()
    {
        var store = new InMemoryCredentialStore();
        store.Save(TwoCharSession("ACC-1"));
        var vm = Make(store, out var runner);

        vm.SelectCharacter("ACC-2");

        Assert.Equal("Hoppity9", vm.CharacterName);
        vm.Play();
        Assert.Equal("ACC-2", runner.Env!["JX_CHARACTER_ID"]);
    }

    [Fact]
    public void SelectCharacter_UnknownId_ThrowsArgumentException()
    {
        var store = new InMemoryCredentialStore();
        store.Save(TwoCharSession("ACC-1"));
        var vm = Make(store, out _);

        Assert.Throws<ArgumentException>(() => vm.SelectCharacter("nope"));
    }

    [Fact]
    public void Play_EmptyStore_ThrowsInvalidOperationException()
    {
        var vm = Make(new InMemoryCredentialStore(), out _);
        Assert.Throws<InvalidOperationException>(() => vm.Play());
    }
}
