// SPDX-License-Identifier: GPL-3.0-or-later
using OsrsLauncher.Core.Auth;
using OsrsLauncher.Core.Launch;
using OsrsLauncher.Core.Session;
using Xunit;

namespace OsrsLauncher.Core.Tests.Launch;

public class RuneLiteLauncherTests
{
    private static RuneLiteLaunchInputs SampleInputs() => new(
        new GameSession("SESS-1"),
        new JagexCharacter("ACC-1", "Zezima"),
        new OAuthTokens("AT", "RT", "IT", 3600));

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public string? StartedPath;
        public IReadOnlyDictionary<string, string>? Env;
        public void Start(string executablePath, IReadOnlyDictionary<string, string> environment)
        {
            StartedPath = executablePath;
            Env = environment;
        }
    }

    [Fact]
    public void BuildEnvironment_MapsJxVariables()
    {
        var env = RuneLiteLauncher.BuildEnvironment(SampleInputs());

        Assert.Equal("SESS-1", env["JX_SESSION_ID"]);
        Assert.Equal("ACC-1", env["JX_CHARACTER_ID"]);
        Assert.Equal("Zezima", env["JX_DISPLAY_NAME"]);
        Assert.False(env.ContainsKey("JX_ACCESS_TOKEN"));
        Assert.False(env.ContainsKey("JX_REFRESH_TOKEN"));
    }

    [Fact]
    public void ResolveExecutablePath_PrefersOverrideWhenItExists()
    {
        var launcher = new RuneLiteLauncher(new FakeProcessRunner(), fileExists: p => p == "/custom/RuneLite");
        Assert.Equal("/custom/RuneLite", launcher.ResolveExecutablePath("/custom/RuneLite"));
    }

    [Fact]
    public void ResolveExecutablePath_ThrowsWhenMissing()
    {
        var launcher = new RuneLiteLauncher(new FakeProcessRunner(), fileExists: _ => false);
        Assert.Throws<RuneLiteNotFoundException>(() => launcher.ResolveExecutablePath(null));
    }

    [Fact]
    public void Launch_StartsResolvedPathWithEnv()
    {
        var runner = new FakeProcessRunner();
        var launcher = new RuneLiteLauncher(runner, fileExists: _ => true);

        launcher.Launch(SampleInputs(), overridePath: "/custom/RuneLite");

        Assert.Equal("/custom/RuneLite", runner.StartedPath);
        Assert.Equal("SESS-1", runner.Env!["JX_SESSION_ID"]);
    }

    [Fact]
    public void BuildEnvironment_DefaultsMissingDisplayNameToEmpty()
    {
        var inputs = new RuneLiteLaunchInputs(
            new GameSession("SESS-1"),
            new JagexCharacter("ACC-1", null),
            new OAuthTokens("AT", "RT", "IT", 3600));

        var env = RuneLiteLauncher.BuildEnvironment(inputs);

        Assert.Equal("", env["JX_DISPLAY_NAME"]);
    }
}
