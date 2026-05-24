// SPDX-License-Identifier: GPL-3.0-or-later
using OsrsLauncher.Core.App;
using OsrsLauncher.Core.Launch;
using OsrsLauncher.Core.Persistence;
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

    [Fact]
    public void IsLoggedIn_FalseWhenEmpty()
    {
        var vm = Make(new InMemoryCredentialStore(), out _);
        Assert.False(vm.IsLoggedIn);
        Assert.Null(vm.CharacterName);
    }

    [Fact]
    public void IsLoggedIn_TrueWhenSaved_AndExposesName()
    {
        var store = new InMemoryCredentialStore();
        store.Save(new StoredSession("SESS-1", "ACC-1", "Jestzer"));
        var vm = Make(store, out _);
        Assert.True(vm.IsLoggedIn);
        Assert.Equal("Jestzer", vm.CharacterName);
    }

    [Fact]
    public void Play_LaunchesStoredSession()
    {
        var store = new InMemoryCredentialStore();
        store.Save(new StoredSession("SESS-7", "ACC-7", "Jestzer"));
        var vm = Make(store, out var runner);

        vm.Play();

        Assert.Equal("SESS-7", runner.Env!["JX_SESSION_ID"]);
        Assert.Equal("ACC-7", runner.Env!["JX_CHARACTER_ID"]);
    }

    [Fact]
    public void Play_ThrowsWhenNoSession()
    {
        var vm = Make(new InMemoryCredentialStore(), out _);
        Assert.Throws<InvalidOperationException>(() => vm.Play());
    }
}
