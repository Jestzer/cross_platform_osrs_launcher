// SPDX-License-Identifier: GPL-3.0-or-later
namespace OsrsLauncher.Core.Session;

public sealed class GameSessionException : Exception
{
    public GameSessionException(string message) : base(message) { }
}
