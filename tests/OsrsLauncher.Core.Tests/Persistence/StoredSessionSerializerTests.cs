// SPDX-License-Identifier: GPL-3.0-or-later
using OsrsLauncher.Core.Persistence;
using OsrsLauncher.Core.Session;
using Xunit;

namespace OsrsLauncher.Core.Tests.Persistence;

public class StoredSessionSerializerTests
{
    [Fact]
    public void RoundTrips_PreservingCharactersAndSelection()
    {
        var s = new StoredSession(
            "SESS-1",
            new List<JagexCharacter> { new("ACC-1", "Jestzer"), new("ACC-2", "Hoppity9") },
            "ACC-2");

        var back = StoredSessionSerializer.Deserialize(StoredSessionSerializer.Serialize(s));

        Assert.NotNull(back);
        Assert.Equal("SESS-1", back!.SessionId);
        Assert.Equal("ACC-2", back.SelectedAccountId);
        Assert.Equal(2, back.Characters.Count);
        Assert.Equal(new JagexCharacter("ACC-1", "Jestzer"), back.Characters[0]);
        Assert.Equal(new JagexCharacter("ACC-2", "Hoppity9"), back.Characters[1]);
    }

    [Fact]
    public void Deserialize_Garbage_ReturnsNull()
    {
        Assert.Null(StoredSessionSerializer.Deserialize("not json"));
    }
}
