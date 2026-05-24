// SPDX-License-Identifier: GPL-3.0-or-later
namespace OsrsLauncher.Core.Persistence;

public sealed record StoredSession(string SessionId, string AccountId, string? DisplayName);
