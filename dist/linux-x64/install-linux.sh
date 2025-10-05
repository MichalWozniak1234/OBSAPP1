#!/bin/bash
echo "📦 Instalacja ObsController dla Linux..."
APP_DIR="/opt/ObsController"

# Utwórz folder i skopiuj pliki
sudo mkdir -p "$APP_DIR"
sudo cp -R "$(dirname "$0")"/* "$APP_DIR"
sudo chmod +x "$APP_DIR/ObsControllerAgent"

# Stwórz prosty skrót wykonywalny
sudo ln -sf "$APP_DIR/ObsControllerAgent" /usr/local/bin/obscontroller-agent

# Rejestracja protokołu obscontroller:// dla środowisk GNOME/KDE
xdg-mime default obscontroller-agent.desktop x-scheme-handler/obscontroller

echo "✅ Instalacja zakończona pomyślnie!"
echo "📂 Zainstalowano w: $APP_DIR"
echo "🎬 Teraz możesz wrócić na stronę i kliknąć „Rozpocznij transmisję”."
read -p "Naciśnij Enter, aby zamknąć..."
