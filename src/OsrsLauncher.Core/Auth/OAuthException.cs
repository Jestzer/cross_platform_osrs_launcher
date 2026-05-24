// SPDX-License-Identifier: GPL-3.0-or-later
namespace OsrsLauncher.Core.Auth;

public sealed class OAuthException : Exception
{
    public OAuthException(string message) : base(message) { }
}
