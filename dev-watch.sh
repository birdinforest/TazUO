#!/bin/bash

# ClassicUO Development Watcher
# Auto-rebuild and restart on code changes

set -e

SCRIPT_DIR="$(dirname "$0")"
PROJECT_ROOT="$SCRIPT_DIR/.."
SETTINGS_PATH="/Users/forrrest/projects/UO-Adventures-Dev/ClassicUO/settings.json"

cd "$PROJECT_ROOT/src/ClassicUO.Client"

echo "=========================================="
echo "ClassicUO - Development Watch Mode"
echo "=========================================="
echo ""
echo "Watching for file changes..."
echo "- Auto-rebuild on save"
echo "- Auto-restart application"
echo "- Architecture: $(uname -m) native"
echo "- Settings: $SETTINGS_PATH"
echo ""
echo "Note: Plugin system (cuoapi.dll) may not work on arm64"
echo "      Game will run without plugins"
echo ""
echo "Press Ctrl+C to stop"
echo ""

# Run with watch mode
dotnet watch run -c Debug -- -settings "$SETTINGS_PATH" "$@"

