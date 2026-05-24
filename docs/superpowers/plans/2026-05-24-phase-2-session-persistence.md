# Phase 2: Session Persistence & Fast Relaunch — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist the Jagex game session so subsequent launches go straight into RuneLite with no WebView login, until the session expires server-side (at which point the user logs in again).

**Architecture:** A new `OsrsLauncher.Core.Persistence` namespace adds an `ICredentialStore` abstraction with an in-memory implementation (unit-tested) and a macOS Keychain implementation. The harness gains a startup fast-path: if a stored session exists (and `--relogin` was not passed), it launches RuneLite directly from the stored `sessionId` + character and exits without showing the WebView; otherwise it runs the existing two-leg login and persists the result. `RuneLiteLauncher` gains a tokens-free launch path (the Jagex env only needs session + character).

**Tech Stack:** C# / .NET 8, xUnit, System.Text.Json, macOS `security` CLI (Keychain), Avalonia (existing harness).

**Branch:** `phase-2-persistence`. **Background:** verified flow + the 3-var `JX_` contract are in `docs/reference/jagex-flow.md`; the relaunch model (persist the session; headless consent is impossible) is documented there and in the original spec §6.2/§9.1.

---

## Conventions

- Working dir: repo root `/Users/james/My_Programs/cross_platform_osrs_launcher`. Branch `phase-2-persistence`.
- Every `.cs` file starts with `// SPDX-License-Identifier: GPL-3.0-or-later`.
- Commits: Conventional Commits, ending (after a blank line) with `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`.
- TDD where the logic is pure. The Keychain implementation and the relaunch fast-path are verified by a live run (Task 5), not unit tests.

## Decisions

- **Store the session, not the refresh token.** Re-minting a session requires the interactive leg-2 consent (browser cookies); there is no headless path. So persistence stores `sessionId` + selected character and reuses it until rejected.
- **Keychain via the `security` CLI** (`add/find/delete-generic-password`), invoked with `ProcessStartInfo.ArgumentList` (no shell). This reliably stores the secret in the login Keychain (not plaintext on disk). **Caveat:** the secret is passed as a process argument on `Save`, briefly visible to `ps` on a multi-user machine — acceptable for a single-user personal tool; a Security-framework P/Invoke implementation (no argv exposure) is a documented future hardening. The stored value is a short-lived game session token, not a password or refresh token.

---

## Task 1: StoredSession model + JSON serializer

**Files:**
- Create: `src/OsrsLauncher.Core/Persistence/StoredSession.cs`
- Create: `src/OsrsLauncher.Core/Persistence/StoredSessionSerializer.cs`
- Test: `tests/OsrsLauncher.Core.Tests/Persistence/StoredSessionSerializerTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/OsrsLauncher.Core.Tests/Persistence/StoredSessionSerializerTests.cs`:
```csharp
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
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~StoredSessionSerializerTests`
Expected: FAIL — types not defined.

- [ ] **Step 3: Implement**

`src/OsrsLauncher.Core/Persistence/StoredSession.cs`:
```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
namespace OsrsLauncher.Core.Persistence;

public sealed record StoredSession(string SessionId, string AccountId, string? DisplayName);
```

`src/OsrsLauncher.Core/Persistence/StoredSessionSerializer.cs`:
```csharp
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
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~StoredSessionSerializerTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: add StoredSession model and JSON serializer"
```

---

## Task 2: ICredentialStore + in-memory implementation

**Files:**
- Create: `src/OsrsLauncher.Core/Persistence/ICredentialStore.cs`
- Create: `src/OsrsLauncher.Core/Persistence/InMemoryCredentialStore.cs`
- Test: `tests/OsrsLauncher.Core.Tests/Persistence/InMemoryCredentialStoreTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/OsrsLauncher.Core.Tests/Persistence/InMemoryCredentialStoreTests.cs`:
```csharp
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
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~InMemoryCredentialStoreTests`
Expected: FAIL — types not defined.

- [ ] **Step 3: Implement**

`src/OsrsLauncher.Core/Persistence/ICredentialStore.cs`:
```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
namespace OsrsLauncher.Core.Persistence;

public interface ICredentialStore
{
    void Save(StoredSession session);
    StoredSession? Load();
    void Clear();
}
```

`src/OsrsLauncher.Core/Persistence/InMemoryCredentialStore.cs`:
```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
namespace OsrsLauncher.Core.Persistence;

public sealed class InMemoryCredentialStore : ICredentialStore
{
    private StoredSession? _session;

    public void Save(StoredSession session) => _session = session;
    public StoredSession? Load() => _session;
    public void Clear() => _session = null;
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~InMemoryCredentialStoreTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: add ICredentialStore with in-memory implementation"
```

---

## Task 3: Tokens-free Jagex launch path on RuneLiteLauncher

The relaunch path has only a `sessionId` + character (no OAuth tokens), and the Jagex env needs only those three vars. Add a tokens-free launch path and route the existing one through it (DRY).

**Files:**
- Modify: `src/OsrsLauncher.Core/Launch/RuneLiteLauncher.cs`
- Test: `tests/OsrsLauncher.Core.Tests/Launch/RuneLiteLauncherTests.cs`

- [ ] **Step 1: Write the failing tests** (add to the existing `RuneLiteLauncherTests` class)

Add these two tests:
```csharp
    [Fact]
    public void BuildJagexEnvironment_MapsThreeVars()
    {
        var env = RuneLiteLauncher.BuildJagexEnvironment(
            new GameSession("SESS-1"), new JagexCharacter("ACC-1", "Zezima"));

        Assert.Equal("SESS-1", env["JX_SESSION_ID"]);
        Assert.Equal("ACC-1", env["JX_CHARACTER_ID"]);
        Assert.Equal("Zezima", env["JX_DISPLAY_NAME"]);
        Assert.False(env.ContainsKey("JX_ACCESS_TOKEN"));
    }

    [Fact]
    public void LaunchJagexSession_StartsResolvedPathWithEnv()
    {
        var runner = new FakeProcessRunner();
        var launcher = new RuneLiteLauncher(runner, fileExists: _ => true);

        launcher.LaunchJagexSession(
            new GameSession("SESS-9"), new JagexCharacter("ACC-9", "Woox"), overridePath: "/custom/RuneLite");

        Assert.Equal("/custom/RuneLite", runner.StartedPath);
        Assert.Equal("SESS-9", runner.Env!["JX_SESSION_ID"]);
        Assert.Equal("ACC-9", runner.Env!["JX_CHARACTER_ID"]);
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~RuneLiteLauncherTests`
Expected: FAIL — `BuildJagexEnvironment` / `LaunchJagexSession` not defined.

- [ ] **Step 3: Implement** in `RuneLiteLauncher.cs`

Add the imports if missing (`using OsrsLauncher.Core.Session;` is already present). Add these members, and refactor the existing `BuildEnvironment(RuneLiteLaunchInputs)` to delegate (keeps existing tests + the harness's current call site working):
```csharp
    public static IReadOnlyDictionary<string, string> BuildJagexEnvironment(GameSession session, JagexCharacter character) => new Dictionary<string, string>
    {
        // Jagex-account login path: exactly these three vars (see docs/reference/jagex-flow.md §5).
        ["JX_SESSION_ID"] = session.SessionId,
        ["JX_CHARACTER_ID"] = character.AccountId,
        ["JX_DISPLAY_NAME"] = character.DisplayName ?? "",
    };

    public static IReadOnlyDictionary<string, string> BuildEnvironment(RuneLiteLaunchInputs input)
        => BuildJagexEnvironment(input.Session, input.Character);

    public void LaunchJagexSession(GameSession session, JagexCharacter character, string? overridePath = null)
        => _runner.Start(ResolveExecutablePath(overridePath), BuildJagexEnvironment(session, character));
```
(Delete the old body of `BuildEnvironment(RuneLiteLaunchInputs)` — it is now a one-line delegation. Leave `Launch(RuneLiteLaunchInputs, ...)`, `ResolveExecutablePath`, `DefaultMacPath`, and `RuneLiteLaunchInputs` unchanged.)

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~RuneLiteLauncherTests`
Expected: PASS (existing 5 + 2 new = 7 in this class). Then run full `dotnet test` — all pass.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: add tokens-free LaunchJagexSession path (session + character only)"
```

---

## Task 4: macOS Keychain credential store

**Files:**
- Create: `src/OsrsLauncher.Core/Persistence/KeychainCredentialStore.cs`

> No unit test: this touches the real login Keychain. It is verified end-to-end by the Task 5 live run (save on login, load on relaunch). The first access may trigger a one-time macOS "allow access to Keychain" prompt — that is expected; the user clicks Allow.

- [ ] **Step 1: Implement `KeychainCredentialStore`**

`src/OsrsLauncher.Core/Persistence/KeychainCredentialStore.cs`:
```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;

namespace OsrsLauncher.Core.Persistence;

/// <summary>
/// Stores the session blob in the macOS login Keychain via the `security` CLI.
/// NOTE: on Save the secret is passed as a process argument (briefly visible to `ps`
/// on a multi-user machine). Acceptable for a single-user personal tool; a
/// Security.framework P/Invoke version (no argv exposure) is a future hardening.
/// </summary>
public sealed class KeychainCredentialStore : ICredentialStore
{
    private const string Service = "cross_platform_osrs_launcher";
    private const string Account = "jagex-session";

    public void Save(StoredSession session)
    {
        var json = StoredSessionSerializer.Serialize(session);
        // -U updates the item if it already exists.
        Run(new[] { "add-generic-password", "-s", Service, "-a", Account, "-w", json, "-U" }, out _);
    }

    public StoredSession? Load()
    {
        if (!Run(new[] { "find-generic-password", "-s", Service, "-a", Account, "-w" }, out var stdout))
            return null;
        return StoredSessionSerializer.Deserialize(stdout.Trim());
    }

    public void Clear()
        => Run(new[] { "delete-generic-password", "-s", Service, "-a", Account }, out _);

    private static bool Run(string[] args, out string stdout)
    {
        var psi = new ProcessStartInfo("/usr/bin/security")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)!;
        stdout = p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
        return p.ExitCode == 0;
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build src/OsrsLauncher.Core`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. Then `dotnet test` — all existing tests still pass (no new tests here).

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat: add macOS Keychain credential store via security CLI"
```

---

## Task 5: Harness fast-path integration (live-verified)

Wire persistence into `src/OsrsLauncher.Harness`: load on startup → fast-path launch; persist after login; `--relogin` to force a fresh login. **Read the existing harness first** (`Program.cs`, `MainWindow.axaml.cs`) to match its structure.

**Files:**
- Modify: `src/OsrsLauncher.Harness/Program.cs` (startup fast-path + arg handling)
- Modify: `src/OsrsLauncher.Harness/MainWindow.axaml.cs` (persist after character selection; use `LaunchJagexSession`)

- [ ] **Step 1: Add the startup fast-path in `Program.cs`**

Before building/starting the Avalonia app, check for a stored session. The harness already stashes args (e.g., `Program.Args`); reuse that. Logic to add at the start of `Main` (adapt to the actual code you read):
```csharp
// SPDX-License-Identifier: GPL-3.0-or-later
// (top of Main, before BuildAvaloniaApp().StartWithClassicDesktopLifetime(...))
var store = new OsrsLauncher.Core.Persistence.KeychainCredentialStore();
var relogin = args.Contains("--relogin");
if (!relogin)
{
    var saved = store.Load();
    if (saved is not null)
    {
        Console.WriteLine($"[fast-path] stored session found for {saved.DisplayName ?? "(no name)"}; launching RuneLite without login.");
        try
        {
            new OsrsLauncher.Core.Launch.RuneLiteLauncher(new OsrsLauncher.Core.Launch.ProcessRunner())
                .LaunchJagexSession(
                    new OsrsLauncher.Core.Session.GameSession(saved.SessionId),
                    new OsrsLauncher.Core.Session.JagexCharacter(saved.AccountId, saved.DisplayName));
            Console.WriteLine("[fast-path] RuneLite launched. If it says \"Failed to login\", the session expired — re-run with --relogin.");
            return; // do not show the WebView window
        }
        catch (OsrsLauncher.Core.Launch.RuneLiteNotFoundException ex)
        {
            Console.WriteLine($"[fast-path][ERROR] {ex.Message}");
            return;
        }
    }
}
Console.WriteLine(relogin ? "[login] --relogin: starting fresh login." : "[login] no stored session; starting login.");
// ...fall through to the existing Avalonia app startup (WebView two-leg login)...
```
Keep the character-selection arg (e.g. `-- "Jestzer"`) working: `--relogin` is a flag; the display-name/index selector remains a separate positional arg.

- [ ] **Step 2: Persist after character selection in `MainWindow.axaml.cs`**

Find where the harness has the `GameSession` and the selected `JagexCharacter` and currently calls the launcher (the `[8b] selected character` / `[9] launching` area). Immediately before launching, persist:
```csharp
        new OsrsLauncher.Core.Persistence.KeychainCredentialStore().Save(
            new OsrsLauncher.Core.Persistence.StoredSession(
                session.SessionId, selectedCharacter.AccountId, selectedCharacter.DisplayName));
        Console.WriteLine("[persist] session saved to Keychain for fast relaunch.");
```
And change the launch call to the tokens-free path:
```csharp
        launcher.LaunchJagexSession(session, selectedCharacter);
```
(Use the existing `launcher` instance and the actual local variable names you find for the session and selected character.)

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 4: Live verification (with the user)** — document and run this checklist:
  1. **Fresh login + persist:** `dotnet run --project src/OsrsLauncher.Harness -- "Jestzer"` → log in via WebView → confirm `[persist] session saved` and RuneLite logs into the world. (A one-time Keychain "allow access" prompt may appear — click Allow.)
  2. **Fast relaunch (the win):** `dotnet run --project src/OsrsLauncher.Harness` → confirm it prints `[fast-path] ...launching RuneLite without login`, shows **no WebView window**, and RuneLite logs straight in.
  3. **Force re-login:** `dotnet run --project src/OsrsLauncher.Harness -- --relogin` → confirm it shows the WebView login again and re-persists.
  4. **Expiry behavior (informational):** if a fast-path launch ever shows "Failed to login," re-run with `--relogin`.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(harness): fast-path relaunch from stored session + --relogin"
```

---

## Self-review (completed during planning)

- **Spec coverage:** persist credentials in OS keychain (spec §9.1) → Tasks 1,2,4. Fast-path auto-launch without UI (spec §6.2) → Task 5 Step 1. `ICredentialStore` interface + in-memory + macOS impl (spec §5 architecture) → Tasks 2,4. Re-login fallback → Task 5 (`--relogin` + the "session expired → re-run" behavior). The investigation's finding (store session, not refresh token; no headless consent) is encoded in the Decisions section and the stored model (`StoredSession` = sessionId + character, no tokens).
- **Placeholder scan:** none — Tasks 1–4 have complete code; Task 5 gives concrete code with explicit "adapt to the variable names you read in the existing harness" guidance (the harness already exists and must be read, not guessed).
- **Type consistency:** `StoredSession(SessionId, AccountId, DisplayName?)`, `ICredentialStore.Save/Load/Clear`, `InMemoryCredentialStore`, `KeychainCredentialStore`, `RuneLiteLauncher.BuildJagexEnvironment(GameSession, JagexCharacter)` / `LaunchJagexSession(GameSession, JagexCharacter, overridePath?)` are used consistently across tasks and match existing Core types (`GameSession.SessionId`, `JagexCharacter.AccountId`/`DisplayName`).

## Out of scope (future)

- Security-framework P/Invoke Keychain store (removes the `Save` argv exposure).
- Proactive session-expiry detection / graceful in-app "log in again" prompt (Phase 3 GUI).
- Storing the launcher refresh token for the legacy-RS account path.
