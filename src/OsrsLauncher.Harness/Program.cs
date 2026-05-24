// SPDX-License-Identifier: GPL-3.0-or-later
using Avalonia;

namespace OsrsLauncher.Harness;

class Program
{
    /// <summary>
    /// Raw command-line arguments stashed before Avalonia consumes them via
    /// StartWithClassicDesktopLifetime. Readable by MainWindow during the login flow.
    /// Index 0, if present, is the character selector: a display name (case-insensitive)
    /// or a 0-based numeric index into the accounts list.
    /// </summary>
    public static string[] Args { get; private set; } = Array.Empty<string>();

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        Args = args;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
