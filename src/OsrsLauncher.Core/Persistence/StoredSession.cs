// SPDX-License-Identifier: GPL-3.0-or-later
using OsrsLauncher.Core.Session;

namespace OsrsLauncher.Core.Persistence;

public sealed record StoredSession(
    string SessionId,
    IReadOnlyList<JagexCharacter> Characters,
    string SelectedAccountId);
