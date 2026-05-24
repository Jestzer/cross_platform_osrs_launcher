// SPDX-License-Identifier: GPL-3.0-or-later
using OsrsLauncher.Core.Persistence;
using Xunit;

namespace OsrsLauncher.Core.Tests.Persistence;

public class InMemoryCredentialStoreTests
{
    [Fact]
    public void Load_WhenEmpty_ReturnsNull()
    {
        var store = new InMemoryCredentialStore();
        Assert.Null(store.Load());
    }

    [Fact]
    public void Save_ThenLoad_ReturnsSession()
    {
        var store = new InMemoryCredentialStore();
        var s = new StoredSession("SESS-1", "ACC-1", "Zezima");
        store.Save(s);
        Assert.Equal(s, store.Load());
    }

    [Fact]
    public void Clear_RemovesSession()
    {
        var store = new InMemoryCredentialStore();
        store.Save(new StoredSession("SESS-1", "ACC-1", "Zezima"));
        store.Clear();
        Assert.Null(store.Load());
    }
}
