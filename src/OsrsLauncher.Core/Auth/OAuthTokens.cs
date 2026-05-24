// SPDX-License-Identifier: GPL-3.0-or-later
namespace OsrsLauncher.Core.Auth;

public sealed record OAuthTokens(
    string AccessToken,
    string? RefreshToken,
    string? IdToken,
    int ExpiresIn);
