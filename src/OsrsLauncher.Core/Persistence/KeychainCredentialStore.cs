// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;

namespace OsrsLauncher.Core.Persistence;

/// <summary>
/// Stores the session blob in the macOS login Keychain via the `security` CLI.
/// NOTE: on Save the secret is passed as a process argument (briefly visible to `ps`
/// on a multi-user machine). Acceptable for a single-user personal tool; a
/// Security.framework P/Invoke version (no argv exposure) is a future hardening.
/// </summary>
public sealed class KeychainCredentialStore : ICredentialStore
{
    private const string Service = "cross_platform_osrs_launcher";
    private const string Account = "jagex-session";

    public void Save(StoredSession session)
    {
        var json = StoredSessionSerializer.Serialize(session);
        // -U updates the item if it already exists; -T trusts the `security` tool to read
        // it back without a GUI prompt (verified on macOS 26).
        Run(new[] { "add-generic-password", "-s", Service, "-a", Account, "-w", json, "-U", "-T", "/usr/bin/security" }, out _);
    }

    public StoredSession? Load()
    {
        if (!Run(new[] { "find-generic-password", "-s", Service, "-a", Account, "-w" }, out var stdout))
            return null;
        return StoredSessionSerializer.Deserialize(stdout.Trim());
    }

    public void Clear()
        => Run(new[] { "delete-generic-password", "-s", Service, "-a", Account }, out _);

    private static bool Run(string[] args, out string stdout)
    {
        var psi = new ProcessStartInfo("/usr/bin/security")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)!;
        stdout = p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
        return p.ExitCode == 0;
    }
}
