// SPDX-License-Identifier: GPL-3.0-or-later
namespace OsrsLauncher.Core.Launch;

public interface IProcessRunner
{
    void Start(string executablePath, IReadOnlyDictionary<string, string> environment);
}
