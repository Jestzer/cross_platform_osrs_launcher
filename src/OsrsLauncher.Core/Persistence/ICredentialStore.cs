// SPDX-License-Identifier: GPL-3.0-or-later
namespace OsrsLauncher.Core.Persistence;

public interface ICredentialStore
{
    void Save(StoredSession session);
    StoredSession? Load();
    void Clear();
}
