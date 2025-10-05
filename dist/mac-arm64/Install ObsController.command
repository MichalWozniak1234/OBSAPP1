#!/bin/bash
# ------------------------------------------------------
# ObsController macOS Auto Installer
# ------------------------------------------------------
# Ten skrypt automatycznie instaluje ObsControllerAgent
# i rejestruje protokół URL obscontroller://
# ------------------------------------------------------

set -e

echo "🧩 Instalator ObsController – uruchamianie..."

# 1. Ustal ścieżkę, gdzie leży plik
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
APP_PATH="/Applications/ObsController"
AGENT_PATH="$APP_PATH/ObsControllerAgent"
PLIST_PATH="$HOME/Library/LaunchAgents/com.obscontroller.agent.plist"

# 2. Nadaj sobie uprawnienia (auto-fix)
echo "🔧 Sprawdzanie uprawnień wykonywania..."
chmod +x "$SCRIPT_DIR/ObsControllerAgent" || true
chmod +x "$0" || true

# 3. Utwórz folder docelowy
echo "📦 Instalowanie do $APP_PATH..."
sudo mkdir -p "$APP_PATH"
sudo cp "$SCRIPT_DIR/ObsControllerAgent" "$APP_PATH/"

# 4. Zarejestruj protokół obscontroller://
echo "🔗 Rejestrowanie protokołu obscontroller://..."
cat <<EOF | sudo tee /Library/LaunchAgents/com.obscontroller.protocol.plist >/dev/null
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
 "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key><string>com.obscontroller.protocol</string>
    <key>ProgramArguments</key>
    <array>
        <string>$APP_PATH/ObsControllerAgent</string>
    </array>
    <key>RunAtLoad</key><true/>
</dict>
</plist>
EOF

# 5. Odśwież LaunchAgents
launchctl unload "$PLIST_PATH" >/dev/null 2>&1 || true
launchctl load "$PLIST_PATH" >/dev/null 2>&1 || true

echo ""
echo "✅ Instalacja zakończona pomyślnie!"
echo "📍 Aplikacja: $APP_PATH"
echo "🌐 Protokół obscontroller:// gotowy do użycia."
echo ""
read -p "Naciśnij Enter, aby zakończyć..."
exit 0
