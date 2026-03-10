# Contributing

This document is written for AI coding assistants helping developers contribute to Unity AI Bridge.

## Repository Structure

```
.claude/skills/unity-bridge/   # bridge.py, mcp_server.py, SKILL.md, params/
Packages/com.aibridge.unity/   # Unity package (UPM)
  Editor/
    Core/                      # Bridge infrastructure (BridgePlugin, Registry, Runner)
    Tools/                     # Tool implementations — add new tools here
    Plugins/                   # Roslyn DLLs for script-execute
  Runtime/
    Attributes/                # [BridgeTool], [BridgeToolType]
    Data/                      # Data structures (SceneRef, GameObjectRef, etc.)
    Serialization/             # Reflection and JSON converters
    Utils/                     # Shared utilities
cli/                           # PyInstaller build scripts (build_mac.sh, build_win.bat)
tests/                         # Python tests (imports from .claude/skills/unity-bridge/)
docs/                          # Setup & reference (SETUP.md)
```

## Adding a New Tool

1. Find or create a `[BridgeToolType]` partial class in `Editor/Tools/`.
2. Add a `public static` method with `[BridgeTool("category-action")]` and `[Description]`.
3. Use `category-action` kebab-case naming (e.g., `terrain-set-height`).
4. Parameters with defaults are optional. Use `[Description]` on parameters too.
5. Return `string` (JSON), primitives, or objects (auto-serialized via System.Text.Json).
6. All Unity API calls must run on the main thread — the tool runner handles this.

```csharp
[BridgeTool("category-action")]
[System.ComponentModel.Description("What this tool does")]
public static string MyTool(
    [System.ComponentModel.Description("Parameter description")]
    string param,
    int optional = 10)
{
    return JsonSerializer.Serialize(new { success = true });
}
```

## Code Style

- **Nullable**: `#nullable enable`, annotate all types.
- **JSON**: `System.Text.Json` only (not Newtonsoft).
- **Naming**: `_camelCase` private fields, `PascalCase` methods/properties.
- **No `Debug.Log`**: Use the bridge's `Logs` utility.

## PR Process

1. Branch from `main` (e.g., `feat/terrain-tools`).
2. Conventional commits: `feat(tools):`, `fix(bridge):`, `docs:`.
3. All CI checks must pass.
4. No breaking changes to existing tool signatures without discussion.

## Running Tests

```bash
# Python (tests/ imports from .claude/skills/unity-bridge/)
pip install pytest && pytest tests/ -v

# Unity — edit-mode tests run via Unity Test Runner
```
