#!/bin/bash
# HoloCade Unity Dedicated Server Launcher (Linux)
# Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

echo "========================================"
echo "HoloCade Unity Dedicated Server Launcher"
echo "========================================"
echo ""

# Default configuration
EXPERIENCE_TYPE="AIFacemask"
SCENE_NAME="HoloCadeScene"
PORT=7777
MAX_PLAYERS=4

# Parse command-line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -experience)
            EXPERIENCE_TYPE="$2"
            shift 2
            ;;
        -port)
            PORT="$2"
            shift 2
            ;;
        -maxplayers)
            MAX_PLAYERS="$2"
            shift 2
            ;;
        *)
            shift
            ;;
    esac
done

echo "Starting HoloCade Unity Dedicated Server..."
echo "Experience Type: $EXPERIENCE_TYPE"
echo "Scene: $SCENE_NAME"
echo "Port: $PORT"
echo "Max Players: $MAX_PLAYERS"
echo ""

# Build path to server executable (Builds/ is at Unity project root, two levels up from this script)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SERVER_PATH="$PROJECT_ROOT/Builds/Server/HoloCade_UnityServer.x86_64"

# Check if server executable exists
if [ ! -f "$SERVER_PATH" ]; then
    echo "ERROR: Server executable not found at $SERVER_PATH"
    echo ""
    echo "Please build the dedicated server target first:"
    echo "1. Open Unity"
    echo "2. File > Build Settings"
    echo "3. Select 'Dedicated Server' platform"
    echo "4. Click 'Build' and save to Builds/Server/"
    echo ""
    exit 1
fi

# Make executable if needed
chmod +x "$SERVER_PATH"

# Launch the dedicated server
echo "Launching server..."
"$SERVER_PATH" -batchmode -nographics -port "$PORT" -scene "$SCENE_NAME" -experienceType "$EXPERIENCE_TYPE" -maxPlayers "$MAX_PLAYERS" -logFile ServerLog.txt

echo ""
echo "Server stopped."















