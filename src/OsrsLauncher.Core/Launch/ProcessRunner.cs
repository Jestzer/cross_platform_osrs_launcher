// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;

namespace OsrsLauncher.Core.Launch;

public sealed class ProcessRunner : IProcessRunner
{
    public void Start(string executablePath, IReadOnlyDictionary<string, string> environment)
    {
        var psi = new ProcessStartInfo(executablePath) { UseShellExecute = false };
        foreach (var (key, value) in environment)
            psi.Environment[key] = value;
        Process.Start(psi);
    }
}
