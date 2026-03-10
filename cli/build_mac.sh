#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
SKILL_DIR="$ROOT_DIR/.claude/skills/unity-bridge"

echo "Building bridge-mac..."
pyinstaller --onefile --name bridge-mac "$SKILL_DIR/bridge.py" --distpath "$SKILL_DIR/bin/"

echo "Building mcp-server-mac..."
pyinstaller --onefile --name mcp-server-mac "$SKILL_DIR/mcp_server.py" --distpath "$SKILL_DIR/bin/"

echo "Done. Binaries in .claude/skills/unity-bridge/bin/"
