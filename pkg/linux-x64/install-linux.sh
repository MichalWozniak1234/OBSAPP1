#!/usr/bin/env bash
set -euo pipefail
echo "📦 Instaluję ObsController (Linux)..."

BIN_NAME="ObsControllerAgent"
SRC_DIR="$(cd "$(dirname "$0")" && pwd)"
SRC_BIN="$SRC_DIR/$BIN_NAME"

DEST_DIR="/opt/obscontroller"
DESKTOP_DIR="$HOME/.local/share/applications"
DESKTOP_FILE="$DESKTOP_DIR/obscontroller.desktop"

sudo mkdir -p "$DEST_DIR"
sudo cp "$SRC_BIN" "$DEST_DIR/$BIN_NAME"
sudo chmod +x "$DEST_DIR/$BIN_NAME"

mkdir -p "$DESKTOP_DIR"
cat > "$DESKTOP_FILE" <<EOF
[Desktop Entry]
Name=ObsController
Exec=$DEST_DIR/$BIN_NAME %u
Terminal=false
Type=Application
MimeType=x-scheme-handler/obscontroller;
NoDisplay=true
EOF

update-desktop-database "$HOME/.local/share/applications" >/dev/null 2>&1 || true
xdg-mime default obscontroller.desktop x-scheme-handler/obscontroller

echo "✅ Zainstalowano. Otwórz w przeglądarce: obscontroller://start?stream=1"