// SPDX-License-Identifier: GPL-3.0-or-later
using OsrsLauncher.Core.Persistence;
using Xunit;

namespace OsrsLauncher.Core.Tests.Persistence;

public class StoredSessionSerializerTests
{
    [Fact]
    public void RoundTrips()
    {
        var s = new StoredSession("SESS-1", "ACC-1", "Zezima");
        var json = StoredSessionSerializer.Serialize(s);
        Assert.Equal(s, StoredSessionSerializer.Deserialize(json));
    }

    [Fact]
    public void RoundTrips_WithNullDisplayName()
    {
        var s = new StoredSession("SESS-1", "ACC-1", null);
        var json = StoredSessionSerializer.Serialize(s);
        Assert.Equal(s, StoredSessionSerializer.Deserialize(json));
    }

    [Fact]
    public void Deserialize_Garbage_ReturnsNull()
    {
        Assert.Null(StoredSessionSerializer.Deserialize("not json"));
    }
}
