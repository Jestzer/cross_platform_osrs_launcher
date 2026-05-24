// SPDX-License-Identifier: GPL-3.0-or-later
using OsrsLauncher.Core.Persistence;
using OsrsLauncher.Core.Session;
using Xunit;

namespace OsrsLauncher.Core.Tests.Persistence;

public class InMemoryCredentialStoreTests
{
    private static StoredSession MakeSession(string sessionId = "SESS-1", string accountId = "ACC-1") =>
        new StoredSession(sessionId, new List<JagexCharacter> { new(accountId, "Zezima") }, accountId);

    [Fact]
    public void Load_WhenEmpty_ReturnsNull()
    {
        var store = new InMemoryCredentialStore();
        Assert.Null(store.Load());
    }

    [Fact]
    public void Save_ThenLoad_ReturnsSameSession()
    {
        var store = new InMemoryCredentialStore();
        var s = MakeSession();
        store.Save(s);
        Assert.Equal(s, store.Load());
    }

    [Fact]
    public void Clear_RemovesSession()
    {
        var store = new InMemoryCredentialStore();
        store.Save(MakeSession());
        store.Clear();
        Assert.Null(store.Load());
    }
}
