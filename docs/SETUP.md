# Unity AI Bridge — Setup & Reference

This document is written for AI coding assistants performing installation and configuration on behalf of users.

## Prerequisites

- Unity 2022.3+ (including Unity 6000.x)
- Python 3.8+
- The user's AI IDE of choice

## Step 1: Install

Clone the repository to a temporary location and copy the required files into the user's Unity project:

```bash
# Clone the repo (shallow clone is fine)
git clone --depth 1 https://github.com/butterlatte-zhang/unity-ai-bridge.git /tmp/unity-ai-bridge

# Copy the Unity package into the project's Packages/ directory
cp -r /tmp/unity-ai-bridge/Packages/com.aibridge.unity /path/to/unity-project/Packages/com.aibridge.unity

# Copy the skill/MCP scripts
cp -r /tmp/unity-ai-bridge/.claude/ /path/to/unity-project/.claude/

# Clean up
rm -rf /tmp/unity-ai-bridge
```

Replace `/path/to/unity-project` with the actual Unity project root (the directory containing `Assets/` and `Packages/`).

The bridge activates automatically via `[InitializeOnLoad]` when Unity reloads the domain. Ask the user to reopen or refocus the Unity Editor, then verify:

```bash
cat /path/to/unity-project/Temp/UnityBridge/heartbeat   # should show a recent timestamp
```

## Step 2: Connect AI IDE

Configure per IDE:

### Claude Code (Skill mode)

No extra config. Claude Code auto-discovers `.claude/skills/unity-bridge/SKILL.md`.

### Cursor

Create `.cursor/mcp.json` in the Unity project root:

```json
{
  "mcpServers": {
    "unity-bridge": {
      "command": "python3",
      "args": [".claude/skills/unity-bridge/mcp_server.py"]
    }
  }
}
```

### GitHub Copilot (VS Code)

Create `.github/copilot-mcp.json` in the Unity project root:

```json
{
  "mcpServers": {
    "unity-bridge": {
      "command": "python3",
      "args": [".claude/skills/unity-bridge/mcp_server.py"]
    }
  }
}
```

Requires Copilot extension v1.200+.

### Windsurf

Edit `~/.codeium/windsurf/mcp_config.json` (macOS/Linux) or `%USERPROFILE%\.codeium\windsurf\mcp_config.json` (Windows):

```json
{
  "mcpServers": {
    "unity-bridge": {
      "command": "python3",
      "args": [".claude/skills/unity-bridge/mcp_server.py"],
      "cwd": "/absolute/path/to/unity-project"
    }
  }
}
```

Windsurf uses a global config, so `cwd` must be an absolute path.

### Claude Desktop

Edit `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS) or `%APPDATA%\Claude\claude_desktop_config.json` (Windows):

```json
{
  "mcpServers": {
    "unity-bridge": {
      "command": "python3",
      "args": [".claude/skills/unity-bridge/mcp_server.py"],
      "cwd": "/absolute/path/to/unity-project"
    }
  }
}
```

## Architecture

Dual-channel design — same Unity plugin, same file IPC:

- **Skill channel**: `bridge.py` writes command files directly. Used by Claude Code.
- **MCP channel**: `mcp_server.py` translates MCP JSON-RPC to command files. Used by all other IDEs.

File IPC protocol:

```
{ProjectRoot}/Temp/UnityBridge/
  commands/     # External -> Unity (JSON, deleted after read)
  results/      # Unity -> External (JSON, expires after 5 min)
  heartbeat     # UTC timestamp, updated every 1s
```

Core components:
- `BridgePlugin` — `[InitializeOnLoad]`, polls commands/, writes heartbeat
- `BridgeToolRegistry` — discovers `[BridgeToolType]` classes and `[BridgeTool]` methods via reflection
- `BridgeToolRunner` — deserializes params, dispatches to main thread, handles timeouts/errors

Serialization: `System.Text.Json` with custom converters for `Vector2/3/4`, `Quaternion`, `Color`, `Bounds`.

## Security

- Runs entirely locally. No network ports opened.
- `script-execute` and `reflection-method-call` can execute arbitrary code. To disable, delete their source files from `Packages/com.aibridge.unity/Editor/Tools/`.
- No authentication on file IPC (same threat model as LSP/DAP).
- `Temp/` is destroyed when Unity closes the project.

## Adding Custom Tools

See [CONTRIBUTING.md](../CONTRIBUTING.md) for the tool creation pattern. Full example:

```csharp
using UnityEngine;
using UnityEditor;
using UnityAiBridge;

[BridgeToolType]
public static partial class MyTools
{
    [BridgeTool("my-tool-name")]
    [System.ComponentModel.Description("What this tool does")]
    public static object MyTool(
        [System.ComponentModel.Description("Param description")]
        string required,
        int optional = 10)
    {
        return new { success = true };
    }
}
```

Key rules:
- `static partial class` with `[BridgeToolType]`
- `public static` method with `[BridgeTool("category-action")]` + `[Description]`
- Parameters with defaults are optional
- Return `string`, primitives, or objects (auto-serialized)
- Supported param types: `string`, `int`, `float`, `bool`, `string[]`, `int[]`, `Vector3`, `Color`, enums
- Throw exceptions for errors (caught and returned as structured error results)
- All Unity API calls run on main thread (handled by tool runner)

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Heartbeat missing/stale | Unity not running or plugin not loaded | Open Unity with a scene loaded; check Console for errors |
| `python3` not found | Python not on PATH | Use absolute path in MCP config, e.g. `/usr/local/bin/python3` |
| Tools not appearing | MCP config path wrong or IDE not restarted | Verify config file location; restart IDE |
| Tool returns error | Unity in wrong mode (Play/Edit) | Check tool requirements; reload scene if needed |
