#!/usr/bin/env bash
set -euo pipefail

APP="/Applications/ObsController.app"
BIN_NAME="ObsControllerAgent"
SRC_DIR="$(cd "$(dirname "$0")" && pwd)"
SRC_BIN="$SRC_DIR/$BIN_NAME"

echo "📦 Instaluję ObsController (macOS)..."

sudo rm -rf "$APP"
sudo mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

sudo tee "$APP/Contents/Info.plist" >/dev/null <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleName</key><string>ObsController</string>
  <key>CFBundleIdentifier</key><string>com.example.obscontroller</string>
  <key>CFBundleVersion</key><string>1.0</string>
  <key>CFBundleShortVersionString</key><string>1.0</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleURLTypes</key>
  <array><dict>
    <key>CFBundleURLName</key><string>ObsController</string>
    <key>CFBundleURLSchemes</key><array><string>obscontroller</string></array>
  </dict></array>
</dict></plist>
PLIST

sudo cp "$SRC_BIN" "$APP/Contents/MacOS/$BIN_NAME"
sudo chmod +x "$APP/Contents/MacOS/$BIN_NAME"

LSREG="/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister"
[ -x "$LSREG" ] && "$LSREG" -f "$APP" || true
open -a "$APP" || true

echo "✅ Zainstalowano. Jeśli macOS zablokuje: System Settings → Privacy & Security → Allow Anyway."
echo "Otwórz w przeglądarce: obscontroller://start?stream=1"