#!/usr/bin/env bash
# package-macos.sh — Build a double-clickable macOS .app bundle for OSRS Launcher
# Usage: run from the repo root: bash scripts/package-macos.sh
# Requirements: macOS arm64, .NET 8, sips, iconutil, codesign (all ship with macOS/Xcode CLT)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# ── Paths ──────────────────────────────────────────────────────────────────────
PROJECT="src/OsrsLauncher.Harness/OsrsLauncher.Harness.csproj"
ICON_SRC="assets/icon-1024.png"
BUILD_DIR="build/macos"
PUBLISH_DIR="$BUILD_DIR/publish"
ICONSET_DIR="$BUILD_DIR/AppIcon.iconset"
ICNS_FILE="$BUILD_DIR/AppIcon.icns"
DIST_APP="dist/OSRS Launcher.app"
APP_EXECUTABLE="OsrsLauncher.Harness"

# ── Clean previous artifacts ───────────────────────────────────────────────────
echo "==> Cleaning build/ and dist/ ..."
rm -rf build/ dist/
mkdir -p "$BUILD_DIR" "$ICONSET_DIR" "$PUBLISH_DIR"
mkdir -p dist

# ── Step 1: Build .icns from icon-1024.png ─────────────────────────────────────
echo "==> Generating icon sizes for .icns ..."

# Each entry: "output_filename width height"
declare -a ICON_SPECS=(
    "icon_16x16.png       16   16"
    "icon_16x16@2x.png    32   32"
    "icon_32x32.png       32   32"
    "icon_32x32@2x.png    64   64"
    "icon_128x128.png    128  128"
    "icon_128x128@2x.png 256  256"
    "icon_256x256.png    256  256"
    "icon_256x256@2x.png 512  512"
    "icon_512x512.png    512  512"
    "icon_512x512@2x.png 1024 1024"
)

for spec in "${ICON_SPECS[@]}"; do
    read -r name w h <<< "$spec"
    sips -z "$h" "$w" "$ICON_SRC" --out "$ICONSET_DIR/$name" >/dev/null
done

echo "==> Converting iconset to .icns ..."
iconutil -c icns "$ICONSET_DIR" -o "$ICNS_FILE"
echo "    Created: $ICNS_FILE"

# ── Step 2: Publish self-contained for osx-arm64 ──────────────────────────────
echo "==> Publishing OsrsLauncher.Harness (osx-arm64, self-contained) ..."
dotnet publish "$PROJECT" \
    -c Release \
    -r osx-arm64 \
    --self-contained true \
    -p:UseAppHost=true \
    -o "$PUBLISH_DIR"

# Confirm the apphost binary exists
if [[ ! -f "$PUBLISH_DIR/$APP_EXECUTABLE" ]]; then
    echo "ERROR: Expected app-host '$APP_EXECUTABLE' not found in $PUBLISH_DIR"
    echo "       Contents: $(ls "$PUBLISH_DIR")"
    exit 1
fi
echo "    App-host binary: $APP_EXECUTABLE"

# ── Step 3: Assemble the .app bundle ──────────────────────────────────────────
echo "==> Assembling bundle: $DIST_APP ..."

APP_CONTENTS="$DIST_APP/Contents"
APP_MACOS="$APP_CONTENTS/MacOS"
APP_RESOURCES="$APP_CONTENTS/Resources"

mkdir -p "$APP_MACOS" "$APP_RESOURCES"

# Copy all published files (executable + dlls + native dylibs) into MacOS/
cp -R "$PUBLISH_DIR/." "$APP_MACOS/"

# Copy icon
cp "$ICNS_FILE" "$APP_RESOURCES/AppIcon.icns"

# Write Info.plist
cat > "$APP_CONTENTS/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleName</key><string>OSRS Launcher</string>
  <key>CFBundleDisplayName</key><string>OSRS Launcher</string>
  <key>CFBundleIdentifier</key><string>com.jestzer.osrslauncher</string>
  <key>CFBundleVersion</key><string>1.0.0</string>
  <key>CFBundleShortVersionString</key><string>1.0.0</string>
  <key>CFBundleExecutable</key><string>OsrsLauncher.Harness</string>
  <key>CFBundleIconFile</key><string>AppIcon</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict></plist>
PLIST

# Ensure the apphost is executable
chmod +x "$APP_MACOS/$APP_EXECUTABLE"

echo "    Bundle assembled."

# ── Step 4: Ad-hoc code-sign ──────────────────────────────────────────────────
echo "==> Ad-hoc code-signing bundle ..."
codesign --force --deep --sign - "$DIST_APP"

echo "==> Verifying code signature ..."
codesign --verify --verbose "$DIST_APP"

# ── Summary ───────────────────────────────────────────────────────────────────
BUNDLE_SIZE=$(du -sh "$DIST_APP" | awk '{print $1}')
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  Bundle:   $REPO_ROOT/$DIST_APP"
echo "  Size:     $BUNDLE_SIZE"
echo "  Signed:   ad-hoc (- identity)"
echo ""
echo "  To launch:"
echo "    open \"$REPO_ROOT/$DIST_APP\""
echo ""
echo "  NOTE: First launch requires right-click → Open (Gatekeeper"
echo "        will block a direct double-click for ad-hoc/unsigned apps)."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
