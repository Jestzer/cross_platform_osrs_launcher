// SPDX-License-Identifier: GPL-3.0-or-later
namespace OsrsLauncher.Core.Session;

public sealed record GameSession(string SessionId);

public sealed record JagexCharacter(string AccountId, string DisplayName);

public sealed record GameSessionConfig(string SessionsEndpoint, string AccountsEndpoint);
