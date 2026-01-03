#!/bin/bash

# ClassicUO Development Runner
# Fast development workflow without NativeAOT compilation

set -e

SCRIPT_DIR="$(dirname "$0")"
PROJECT_ROOT="."
SETTINGS_PATH="/Users/forrrest/projects/UO-BritainKnights/settings.json"
# SETTINGS_PATH="/Users/forrrest/projects/OpenUO/settings/settings_osi.json"
# SETTINGS_PATH="/Users/forrrest/projects/UO-Adventures-Dev/ClassicUO/settings.json"

cd "./src/ClassicUO.Client"

echo "=========================================="
echo "ClassicUO - Development Mode"
echo "=========================================="
echo ""
echo "Running with .NET runtime (fast iteration)"
echo "- Architecture: $(uname -m) native"
echo "- Fast startup, full debugging support"
echo "- Settings: $SETTINGS_PATH"
echo ""
echo "Note: Plugin system (cuoapi.dll) may not work on arm64"
echo "      Game will run without plugins"
echo ""
echo "Press Ctrl+C to stop"
echo ""

# Run with .NET runtime directly
dotnet run -c Debug -- -settings "$SETTINGS_PATH" "$@"

