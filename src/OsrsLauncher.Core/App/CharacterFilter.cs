// SPDX-License-Identifier: GPL-3.0-or-later
using OsrsLauncher.Core.Session;

namespace OsrsLauncher.Core.App;

public static class CharacterFilter
{
    public static IReadOnlyList<JagexCharacter> Selectable(IReadOnlyList<JagexCharacter> all)
        => all.Where(c => !string.IsNullOrWhiteSpace(c.DisplayName)).ToList();
}
