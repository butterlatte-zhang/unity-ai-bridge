# AI Playtest Guide

A practical guide to using Unity AI Bridge tools for automated playtesting.

## Prerequisites

- Unity project with `com.aibridge.unity` package installed
- Unity Editor running with the project open
- Python 3.8+ (for wait scripts)

## New Tools Overview

| Tool | Mode | Description |
|------|------|-------------|
| `screenshot-capture` | Play + Edit | Capture Game view or Scene view as PNG |
| `runtime-query` | Play only | Read any MonoBehaviour's public fields by type name |
| `runtime-invoke` | Play only | Call any public static method by type + method name |

## Helper Scripts

Located in `.claude/skills/unity-bridge/scripts/`:

| Script | Description |
|--------|-------------|
| `wait_playmode.py` | Block until Play Mode is fully ready (handles domain reload) |
| `wait_compile.py` | Trigger refresh and wait for compilation to complete |

## Quick Start

### 1. Enter Play Mode

```bash
# Start Play Mode
python3 bridge.py editor-application-set-state '{"isPlaying": true}'

# Wait for it to be ready (domain reload can take 10-30s)
python3 scripts/wait_playmode.py --timeout 60
```

### 2. Query Game State

```bash
# Find all instances of a type and read their fields
python3 bridge.py runtime-query '{"typeName": "PlayerController"}'

# Read specific fields only
python3 bridge.py runtime-query '{"typeName": "GameManager", "fields": "score,level,isGameOver"}'

# Find all enemies
python3 bridge.py runtime-query '{"typeName": "EnemyAI", "findAll": true, "maxResults": 5}'
```

### 3. Invoke Methods

```bash
# Call a static method with no arguments
python3 bridge.py runtime-invoke '{"typeName": "GameManager", "methodName": "RestartGame"}'

# Call with arguments
python3 bridge.py runtime-invoke '{"typeName": "DebugHelper", "methodName": "SetPlayerHealth", "arguments": "[100]"}'

# Call with string argument
python3 bridge.py runtime-invoke '{"typeName": "CheatConsole", "methodName": "Execute", "arguments": "[\"god_mode\"]"}'
```

### 4. Take Screenshots

```bash
# Basic screenshot (Play Mode → Game view, Edit Mode → Scene view)
python3 bridge.py screenshot-capture '{}'

# With tag and custom resolution
python3 bridge.py screenshot-capture '{"tag": "combat-test", "width": 1920, "height": 1080}'
```

### 5. Exit Play Mode

```bash
python3 bridge.py editor-application-set-state '{"isPlaying": false}'
```

## Automated Test Pattern

The recommended pattern for AI-driven testing:

```
1. Enter Play Mode + wait_playmode.py
2. Loop:
   a. Act    → runtime-invoke (trigger action)
   b. Wait   → sleep / frame step
   c. Query  → runtime-query (read state)
   d. Capture → screenshot-capture (visual check)
   e. Judge  → compare actual vs expected
   f. If FAIL → exit, fix code, wait_compile.py, restart
3. Exit Play Mode
```

## Tips

- **runtime-query** works with partial type names — `"Player"` will find `MyGame.PlayerController`
- **runtime-invoke** only works with `public static` methods. For instance methods, use `reflection-method-call`
- **screenshot-capture** works in both Play and Edit Mode — use Edit Mode screenshots to verify scene setup
- **wait_playmode.py** handles the tricky domain-reload timing automatically
- Use `console-get-logs` after each step to catch runtime errors

## Combining with Existing Tools

These new tools complement the existing tools:

- `script-update-or-create` → write code → `wait_compile.py` → `runtime-query` to verify
- `gameobject-create` → setup scene → `screenshot-capture` to verify layout
- `profiler-snapshot` → performance check → `runtime-invoke` to trigger heavy operation → `profiler-snapshot` again
