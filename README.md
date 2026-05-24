# Cross Platform OSRS Launcher

A native desktop launcher that logs into a Jagex account and starts
RuneLite with the session credentials it needs, without the official Jagex
Launcher. Cross-platform compatibility and simplicity should allow for (hopefully) easier future-proofing and cross-platform support.

## Why

Jagex has failed to offer support for Apple Silicon (M-series CPUs) and Linux users. Bolt (I believe) meets the needs of Linux users, but it currently has no Mac support. With Rosetta 2 being removed soon by Apple, Apple Silicon users are potentionally left in the dust and have no easy way of playing OSRS with a Jagex account, until now!

This is built with Avalonia, so it is cross-platform-capable. Apple Silicon is the only build-and-test target for now, though.

## Status

🚧 **Design phase.** No application code yet. The design is documented in
[`docs/superpowers/specs/2026-05-23-cross-platform-osrs-launcher-design.md`](docs/superpowers/specs/2026-05-23-cross-platform-osrs-launcher-design.md).

## Scope (v1)

- ✅ Jagex-account OAuth login (incl. 2FA) in a native system webview
- ✅ Game session + character selection
- ✅ Launch RuneLite with `JX_*` credentials
- ✅ Optional one-click relaunch (refresh token stored in the OS keychain)
- ❌ No RS3, official-client launching, game downloads/updates, or plugins

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

This is completely unofficial and not affiliated with or endorsed by Jagex or RuneLite in any way shape or form.

## License

Licensed under the [GNU General Public License v3.0](LICENSE).
