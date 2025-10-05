#!/bin/bash
echo "📦 Instalacja ObsController dla macOS Apple Silicon..."
APP_DIR="/Applications/ObsController"

# Utwórz folder i skopiuj pliki
sudo mkdir -p "$APP_DIR"
sudo cp -R "$(dirname "$0")"/* "$APP_DIR"
sudo chmod +x "$APP_DIR/ObsControllerAgent"

# Zarejestruj protokół (obscontroller://)
echo "🔗 Rejestrowanie protokołu obscontroller://..."
PROTO_FILE="$HOME/Library/LaunchAgents/com.obscontroller.agent.plist"
cat <<EOF > "$PROTO_FILE"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key><string>com.obscontroller.agent</key>
    <key>ProgramArguments</key>
    <array>
        <string>$APP_DIR/ObsControllerAgent</string>
    </array>
    <key>RunAtLoad</key><true/>
</dict>
</plist>
EOF

launchctl load "$PROTO_FILE" 2>/dev/null

echo "✅ Instalacja zakończona pomyślnie!"
echo "📂 Zainstalowano w: $APP_DIR"
echo "🎬 Teraz możesz wrócić na stronę i kliknąć „Rozpocznij transmisję”."
read -p "Naciśnij Enter, aby zamknąć..."
