// SPDX-License-Identifier: GPL-3.0-or-later
namespace OsrsLauncher.Core.Auth;

public sealed record JagexOAuthConfig(
    string AuthorizeEndpoint,
    string TokenEndpoint,
    string ClientId,
    string RedirectUri,
    string Scope);
