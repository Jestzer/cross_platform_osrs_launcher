// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;

namespace OsrsLauncher.Core.Persistence;

public static class StoredSessionSerializer
{
    private static readonly JsonSerializerOptions Opts = new(JsonSerializerDefaults.Web);

    public static string Serialize(StoredSession session) => JsonSerializer.Serialize(session, Opts);

    public static StoredSession? Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<StoredSession>(json, Opts); }
        catch (JsonException) { return null; }
    }
}
