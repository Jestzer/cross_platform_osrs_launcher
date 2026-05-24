// SPDX-License-Identifier: GPL-3.0-or-later
using OsrsLauncher.Core.Auth;
using OsrsLauncher.Core.Session;

namespace OsrsLauncher.Core.Launch;

public sealed record RuneLiteLaunchInputs(GameSession Session, JagexCharacter Character, OAuthTokens Tokens);

public sealed class RuneLiteNotFoundException : Exception
{
    public RuneLiteNotFoundException(string message) : base(message) { }
}

public sealed class RuneLiteLauncher
{
    public const string DefaultMacPath = "/Applications/RuneLite.app/Contents/MacOS/RuneLite";

    private readonly IProcessRunner _runner;
    private readonly Func<string, bool> _fileExists;

    public RuneLiteLauncher(IProcessRunner runner, Func<string, bool>? fileExists = null)
    {
        _runner = runner;
        _fileExists = fileExists ?? File.Exists;
    }

    // Jagex-account login path: RuneLite expects exactly these three vars. JX_ACCESS_TOKEN/JX_REFRESH_TOKEN belong to the separate legacy-RS path (see docs/reference/jagex-flow.md §5).
    public static IReadOnlyDictionary<string, string> BuildEnvironment(RuneLiteLaunchInputs input) => new Dictionary<string, string>
    {
        ["JX_SESSION_ID"] = input.Session.SessionId,
        ["JX_CHARACTER_ID"] = input.Character.AccountId,
        ["JX_DISPLAY_NAME"] = input.Character.DisplayName ?? "",
    };

    public string ResolveExecutablePath(string? overridePath)
    {
        var path = overridePath ?? DefaultMacPath;
        if (!_fileExists(path))
            throw new RuneLiteNotFoundException($"RuneLite executable not found at: {path}");
        return path;
    }

    public void Launch(RuneLiteLaunchInputs input, string? overridePath = null)
        => _runner.Start(ResolveExecutablePath(overridePath), BuildEnvironment(input));
}
