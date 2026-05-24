# cross_platform_osrs_launcher

A native desktop launcher that logs into a **Jagex account** and starts
**RuneLite** with the session credentials it needs — without the official Jagex
Launcher.

## Why

The Jagex Launcher is the supported way to obtain Jagex-account login
credentials for Old School RuneScape / RuneLite, but on macOS it ships only as
an **Intel binary (Rosetta 2)**. This project is a **native Apple Silicon**
alternative for that one job: do the Jagex OAuth login, get a game session, and
launch RuneLite with the `JX_*` environment variables it reads.

Built with **C# / .NET + Avalonia 12**, so it is cross-platform-capable. macOS /
Apple Silicon is the only build-and-test target for v1.

## Status

🚧 **Design phase.** No application code yet. The design is documented in
[`docs/superpowers/specs/2026-05-23-cross-platform-osrs-launcher-design.md`](docs/superpowers/specs/2026-05-23-cross-platform-osrs-launcher-design.md).

## Scope (v1)

- ✅ Jagex-account OAuth login (incl. 2FA) in a native system webview
- ✅ Game session + character selection
- ✅ Launch RuneLite with `JX_*` credentials
- ✅ Optional one-click relaunch (refresh token stored in the OS keychain)
- ❌ No RS3, official-client launching, game downloads/updates, or plugins
  (that is [Bolt](https://github.com/Adamcake/Bolt)'s territory)

## Security

Your Jagex **refresh token grants full account access and bypasses your
password.** This app stores it only in the operating system keychain — never in
plaintext, never logged. Do not share keychain exports or any captured tokens.

## Prior art / references

This builds on community work that already reverse-engineered the Jagex login
flow:

- [melxin/native-linux-jagex-launcher](https://github.com/melxin/native-linux-jagex-launcher)
- [aitoiaita/linux-jagex-launcher](https://github.com/aitoiaita/linux-jagex-launcher)
- [Kompreya/Runelite-Sans-Jagex-Launcher](https://github.com/Kompreya/Runelite-Sans-Jagex-Launcher)
- [RuneLite — Using Jagex Accounts](https://github.com/runelite/runelite/wiki/Using-Jagex-Accounts)

## Disclaimer

Unofficial. Not affiliated with or endorsed by Jagex or RuneLite. For personal
use. A `LICENSE` has not been chosen yet.
