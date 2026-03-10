---
name: unity-bridge
description: Remote-control Unity Editor via file-based IPC — 62 tools covering scene management, GameObject/component CRUD, asset operations, prefab editing, script execution, profiling, light probes, package management, and more.
user_invocable: true
---

# Unity Bridge

Interact with Unity Editor via file-based IPC. Always read `params/{tool}.json` before calling a tool to get accurate parameters.

## Invocation

`bridge.py` is located in the same directory as this SKILL.md. Run from the Unity project root:

```bash
python3 <skill-dir>/bridge.py <tool-name> '<json-params>'
```

where `<skill-dir>` is the directory containing this file. Omit JSON when no parameters are needed.

## Critical Rules

1. **Pre-check**: Unity Editor must be running. Verify via `Temp/UnityBridge/heartbeat`
2. **Read params first**: **Always** read `params/{tool-name}.json` before calling — do not guess parameters from memory
3. **camelCase**: JSON parameter keys must be camelCase. PascalCase keys are ignored
4. **Serial execution**: Only one tool call at a time. Concurrent calls are queued
5. **Param source**: JSON files under `params/` are auto-generated from C# tool method signatures

## Tool Index (62 tools)

**Scene (7)**: `scene-list-opened` list opened scenes · `scene-get-data` get scene data · `scene-open` open scene · `scene-create` create scene · `scene-save` save scene · `scene-set-active` set active scene · `scene-unload` unload scene

**GameObject (11)**: `gameobject-find` find · `gameobject-create` create · `gameobject-destroy` destroy · `gameobject-modify` modify properties · `gameobject-duplicate` duplicate · `gameobject-set-parent` set parent · `gameobject-component-add` add component · `gameobject-component-destroy` remove component · `gameobject-component-get` get component data · `gameobject-component-list-all` list components · `gameobject-component-modify` modify component

**Assets (11)**: `assets-find` search assets · `assets-find-built-in` search built-in assets · `assets-get-data` get asset data · `assets-modify` modify asset · `assets-copy` copy · `assets-move` move · `assets-delete` delete · `assets-refresh` refresh AssetDatabase · `assets-create-folder` create folder · `assets-material-create` create material · `assets-shader-list-all` list shaders

**Prefab (5)**: `assets-prefab-open` open for editing · `assets-prefab-close` close editing · `assets-prefab-save` save · `assets-prefab-create` create · `assets-prefab-instantiate` instantiate

**Script (4)**: `script-read` read · `script-update-or-create` create/update · `script-delete` delete · `script-execute` execute C# code

**Object (2)**: `object-get-data` get data · `object-modify` modify

**Editor (4)**: `editor-application-get-state` editor state · `editor-application-set-state` set state · `editor-selection-get` get selection · `editor-selection-set` set selection

**Reflection (2)**: `reflection-method-find` find methods · `reflection-method-call` call method

**Console (1)**: `console-get-logs` get logs

**Profiler (5)**: `profiler-snapshot` performance snapshot · `profiler-gc-alloc` GC allocations · `profiler-hotpath` hot paths · `profiler-frame-hierarchy` frame hierarchy · `profiler-stream` multi-frame sampling

**Package (4)**: `package-list` installed packages · `package-search` search packages · `package-add` install · `package-remove` uninstall

**LightProbe (5)**: `lightprobe-bake` bake · `lightprobe-clear` clear · `lightprobe-analyze` analyze lighting · `lightprobe-configure-lights` configure lights · `lightprobe-generate-grid` generate grid

**Tests (1)**: `tests-run` run tests

## Script Recompilation

**`script-update-or-create` and `script-delete` trigger Unity recompilation (30-60s)**. During recompilation the heartbeat expires and subsequent tool calls will be rejected.

Workflow:
1. Call `script-update-or-create` or `script-delete`
2. **Immediately pause all subsequent bridge calls**
3. Notify user: "Script modified, Unity is recompiling. Please let me know when compilation is complete."
4. Wait for user confirmation before resuming operations

Similarly, any operation that triggers `AssetDatabase.Refresh()` with pending script changes may cause recompilation.

## MCP Server

For Cursor/Copilot/Windsurf and other MCP clients, see `docs/SETUP.md`.

## Error Handling

- **heartbeat not found**: Unity Editor is not running or Bridge is not installed
- **heartbeat stale**: Editor may be compiling or frozen
- **timeout**: Operation timed out (default 30s)
- **tool not found**: Check tool name spelling against the index above
- **parameter error**: Read `params/{tool}.json` to verify correct parameter names and types
