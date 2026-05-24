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

    public static IReadOnlyDictionary<string, string> BuildJagexEnvironment(GameSession session, JagexCharacter character) => new Dictionary<string, string>
    {
        // Jagex-account login path: exactly these three vars (see docs/reference/jagex-flow.md §5).
        ["JX_SESSION_ID"] = session.SessionId,
        ["JX_CHARACTER_ID"] = character.AccountId,
        ["JX_DISPLAY_NAME"] = character.DisplayName ?? "",
    };

    public static IReadOnlyDictionary<string, string> BuildEnvironment(RuneLiteLaunchInputs input)
        => BuildJagexEnvironment(input.Session, input.Character);

    public void LaunchJagexSession(GameSession session, JagexCharacter character, string? overridePath = null)
        => _runner.Start(ResolveExecutablePath(overridePath), BuildJagexEnvironment(session, character));

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
