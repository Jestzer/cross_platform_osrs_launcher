// SPDX-License-Identifier: GPL-3.0-or-later
using OsrsLauncher.Core.App;
using OsrsLauncher.Core.Session;
using Xunit;

namespace OsrsLauncher.Core.Tests.App;

public class CharacterFilterTests
{
    [Fact]
    public void Selectable_KeepsOnlyNamedCharacters()
    {
        var all = new List<JagexCharacter>
        {
            new("ACC-0", null),
            new("ACC-1", ""),
            new("ACC-2", "Jestzer"),
            new("ACC-3", "Hoppity9"),
        };

        var selectable = CharacterFilter.Selectable(all);

        Assert.Equal(2, selectable.Count);
        Assert.Equal("Jestzer", selectable[0].DisplayName);
        Assert.Equal("Hoppity9", selectable[1].DisplayName);
    }
}
