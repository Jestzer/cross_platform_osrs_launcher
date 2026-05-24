# Cross-Platform OSRS Launcher

A native desktop launcher that logs into a Jagex account and starts
RuneLite with the session credentials it needs, without the official Jagex
Launcher. Cross-platform compatibility and simplicity should allow for (hopefully) easier future-proofing and cross-platform support.

## Why

Jagex has failed to offer support for Apple Silicon (M-series CPUs) and Linux users. Bolt (I believe) meets the needs of Linux users, but it currently has no Mac support. With Rosetta 2 being removed soon by Apple, Apple Silicon users are potentionally left in the dust and have no easy way of playing OSRS with a Jagex account, until now!

This is built with Avalonia, so it is cross-platform-capable. Apple Silicon is the only build-and-test target for now, though.

## Features

- Logs into your **Jagex account** (including 2FA) in a native macOS WebView — no Jagex Launcher, no Rosetta.
- Launches **RuneLite** straight into the game with your session.
- **Remembers your login** in the macOS Keychain — after the first login it relaunches with no login window.
- **Switch character** instantly (reuses the session), or **Switch account** (full re-login).
- Native **Apple Silicon** app with an OSRS-themed UI.

## Install

**Option A — download the release (easiest):**

1. Download `OSRS-Launcher.app.zip` from the [latest release](https://github.com/Jestzer/cross_platform_osrs_launcher/releases), unzip it, and move **OSRS Launcher.app** to `/Applications`.
2. First launch: the app isn't notarized, so right-click it → **Open** (or **System Settings → Privacy & Security → Open Anyway**). You only need to do this once.

**Option B — build from source:**

```sh
git clone https://github.com/Jestzer/cross_platform_osrs_launcher
cd cross_platform_osrs_launcher
bash scripts/package-macos.sh        # builds dist/OSRS Launcher.app
# …or run it directly during development:
dotnet run --project src/OsrsLauncher.Harness
```

Requires the **.NET 8 SDK** and macOS on Apple Silicon.

## Usage

1. Open the app → **Log in** → sign into your Jagex account in the window → pick your character.
2. Click **Play** to launch RuneLite into the game.
3. Next time, it opens straight to your saved character — just click **Play**.
4. **Switch character** swaps characters with no re-login; **Switch account** does a fresh login.

You also need **RuneLite installed at `/Applications/RuneLite.app`** (from [runelite.net](https://runelite.net)).

## How it works

It performs the Jagex two-leg OAuth flow in a system WebView, creates a game session, and launches RuneLite with the `JX_SESSION_ID` / `JX_CHARACTER_ID` / `JX_DISPLAY_NAME` environment variables RuneLite reads. The reverse-engineered flow is documented in [`docs/reference/jagex-flow.md`](docs/reference/jagex-flow.md).

## Known limitations

- **Jagex accounts only** — legacy RuneScape-account login isn't implemented yet.
- **RuneLite must be at `/Applications/RuneLite.app`** (no custom-path setting yet).
- **Not notarized** — downloaders must bypass Gatekeeper once (see Install).
- The game session eventually expires server-side; when it does, use **Switch account** to log in again.
- No RS3, official-client launching, game downloads/updates, or plugins — that's [Bolt](https://github.com/Adamcake/Bolt)'s territory.

## Security

Your Jagex session is stored only in the macOS **Keychain** — never in plaintext, never logged. Login and 2FA happen inside the system WebView, so the app never handles your password.

## Prior art / references

This builds on community work that reverse-engineered the Jagex login flow:

- [melxin/native-linux-jagex-launcher](https://github.com/melxin/native-linux-jagex-launcher)
- [aitoiaita/linux-jagex-launcher](https://github.com/aitoiaita/linux-jagex-launcher)
- [Kompreya/Runelite-Sans-Jagex-Launcher](https://github.com/Kompreya/Runelite-Sans-Jagex-Launcher)
- [RuneLite — Using Jagex Accounts](https://github.com/runelite/runelite/wiki/Using-Jagex-Accounts)

## Disclaimer

This is completely unofficial and not affiliated with or endorsed by Jagex or RuneLite in any way shape or form.

## License

Licensed under the [GNU General Public License v3.0](LICENSE).
