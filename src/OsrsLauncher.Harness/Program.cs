// SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using Avalonia;

namespace OsrsLauncher.Harness;

class Program
{
    /// <summary>
    /// Raw command-line arguments stashed before Avalonia consumes them via
    /// StartWithClassicDesktopLifetime. Readable by MainWindow during the login flow.
    /// The character selector, if present, is the first positional arg that is NOT
    /// a recognised flag (e.g. not "--relogin"): a display name (case-insensitive)
    /// or a 0-based numeric index into the accounts list.
    /// </summary>
    public static string[] Args { get; private set; } = Array.Empty<string>();

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // ── Fast-path: relaunch from stored Keychain session ─────────────────
        // Skip entirely when the user passes --relogin to force a fresh login.
        var store = new OsrsLauncher.Core.Persistence.KeychainCredentialStore();
        var relogin = args.Contains("--relogin");
        if (!relogin)
        {
            var saved = store.Load();
            if (saved is not null)
            {
                Console.WriteLine($"[fast-path] stored session found for {saved.DisplayName ?? "(no name)"}; launching RuneLite without login.");
                try
                {
                    new OsrsLauncher.Core.Launch.RuneLiteLauncher(new OsrsLauncher.Core.Launch.ProcessRunner())
                        .LaunchJagexSession(
                            new OsrsLauncher.Core.Session.GameSession(saved.SessionId),
                            new OsrsLauncher.Core.Session.JagexCharacter(saved.AccountId, saved.DisplayName));
                    Console.WriteLine("[fast-path] RuneLite launched. If it shows \"Failed to login\", the session expired — re-run with --relogin.");
                    return;
                }
                catch (OsrsLauncher.Core.Launch.RuneLiteNotFoundException ex)
                {
                    Console.WriteLine($"[fast-path][ERROR] {ex.Message}");
                    return;
                }
            }
        }
        Console.WriteLine(relogin ? "[login] --relogin: starting fresh login." : "[login] no stored session; starting login.");

        // Stash args for MainWindow; strip --relogin so the character selector
        // (positional index 0) is unaffected by the presence of the flag.
        Args = args.Where(a => a != "--relogin").ToArray();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
