// SPDX-License-Identifier: GPL-3.0-or-later
namespace OsrsLauncher.Core.Persistence;

public sealed class InMemoryCredentialStore : ICredentialStore
{
    private StoredSession? _session;

    public void Save(StoredSession session) => _session = session;
    public StoredSession? Load() => _session;
    public void Clear() => _session = null;
}
