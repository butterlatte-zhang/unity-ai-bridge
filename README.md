**[English](README.md)** | **[中文](README.zh-CN.md)**

# Unity AI Bridge

**Remote-control the Unity Editor from any AI IDE — no ports, no dependencies, just works.**

[![Unity 2022.3+](https://img.shields.io/badge/Unity-2022.3%2B-black?logo=unity)](#)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/butterlatte-zhang/unity-ai-bridge?style=social)](https://github.com/butterlatte-zhang/unity-ai-bridge/stargazers)
[![Release](https://img.shields.io/github/v/release/butterlatte-zhang/unity-ai-bridge)](https://github.com/butterlatte-zhang/unity-ai-bridge/releases)

---

## Why Unity AI Bridge?

Most AI coding assistants can read and write files, but they are **blind to the Unity Editor** — they can't inspect your scene, tweak materials, run tests, or profile performance. Unity AI Bridge gives AI full editor access.

### Key Advantages

- **62 tools, 13 categories** — Scene, GameObject, Assets, Prefab, Script, Profiler, LightProbe, Tests, and more. Covers the full editor workflow, not just file I/O.
- **File-based IPC, not WebSocket** — No open ports, no firewall issues, no connection drops. Survives recompilation, play-mode transitions, and editor restarts gracefully.
- **Zero external dependencies** — Pure Python stdlib CLI/MCP server, self-contained C# Unity package. No pip install, no npm, no Node.js runtime.
- **Every major AI IDE** — Claude Code (Skill mode), Cursor, GitHub Copilot, Windsurf, Claude Desktop (MCP mode). One Unity plugin, all IDEs.
- **5-line extensibility** — Add custom tools with `[BridgeTool]` attribute. Auto-discovered, auto-serialized, auto-documented. No registration code needed.
- **Production-tested** — Built for and battle-tested in a large-scale open-world Unity game (50+ developers, 2M+ lines of C#).

---

## Quick Start

> **AI-native project** — Copy the prompt below and send it to your AI coding assistant. The [setup guide](docs/SETUP.md) is written for AI to follow — you don't need to run any commands yourself.
>
> ```
> Help me install Unity AI Bridge by following this guide:
> https://github.com/butterlatte-zhang/unity-ai-bridge/blob/main/docs/SETUP.md
> ```

If you prefer manual setup:

1. **Unity Package** — In Unity: *Window > Package Manager > + > Add package from git URL*:
   ```
   https://github.com/butterlatte-zhang/unity-ai-bridge.git?path=Packages/com.aibridge.unity
   ```
   Or manually copy `Packages/com.aibridge.unity` from this repo into your project's `Packages/` directory.

2. **IDE Integration** — Copy `.claude/` to your project root, then configure your IDE per [docs/SETUP.md](docs/SETUP.md).

Supports: Claude Code (Skill mode), Cursor, GitHub Copilot, Windsurf, Claude Desktop (MCP mode).

---

## Tool Categories

62 tools organized into 13 categories:

| Category | Count | Tools |
|----------|:-----:|-------|
| **Scene** | 7 | `scene-open`, `scene-save`, `scene-create`, `scene-list-opened`, `scene-get-data`, `scene-set-active`, `scene-unload` |
| **GameObject** | 11 | `gameobject-find`, `gameobject-create`, `gameobject-destroy`, `gameobject-modify`, `gameobject-duplicate`, `gameobject-set-parent`, `gameobject-component-add`, `gameobject-component-destroy`, `gameobject-component-get`, `gameobject-component-list-all`, `gameobject-component-modify` |
| **Assets** | 11 | `assets-find`, `assets-find-built-in`, `assets-get-data`, `assets-modify`, `assets-move`, `assets-copy`, `assets-delete`, `assets-create-folder`, `assets-refresh`, `assets-material-create`, `assets-shader-list-all` |
| **Prefab** | 5 | `assets-prefab-create`, `assets-prefab-open`, `assets-prefab-save`, `assets-prefab-close`, `assets-prefab-instantiate` |
| **Script** | 4 | `script-read`, `script-update-or-create`, `script-delete`, `script-execute` |
| **Object** | 2 | `object-get-data`, `object-modify` |
| **Editor** | 4 | `editor-application-get-state`, `editor-application-set-state`, `editor-selection-get`, `editor-selection-set` |
| **Reflection** | 2 | `reflection-method-find`, `reflection-method-call` |
| **Console** | 1 | `console-get-logs` |
| **Profiler** | 5 | `profiler-snapshot`, `profiler-stream`, `profiler-frame-hierarchy`, `profiler-hotpath`, `profiler-gc-alloc` |
| **Package** | 4 | `package-list`, `package-search`, `package-add`, `package-remove` |
| **Light Probe** | 5 | `lightprobe-generate-grid`, `lightprobe-analyze`, `lightprobe-bake`, `lightprobe-clear`, `lightprobe-configure-lights` |
| **Tests** | 1 | `tests-run` |

---

## Architecture

```
┌──────────────────────────────────────────────────┐
│                   AI IDE                         │
│  (Claude Code / Cursor / Copilot / Windsurf)     │
└──────────┬────────────────────┬──────────────────┘
           │                    │
     Skill mode            MCP mode
           │                    │
           ▼                    ▼
    ┌─────────────┐    ┌──────────────┐
    │  bridge.py  │    │ mcp_server.py│
    │  (Python)   │    │  (Python)    │
    └──────┬──────┘    └──────┬───────┘
           │                  │
           └────────┬─────────┘
                    │
              File-based IPC
            (request / response)
                    │
                    ▼
    ┌───────────────────────────────┐
    │     Unity Editor Plugin      │
    │   (com.aibridge.unity)       │
    │                              │
    │  BridgePlugin ← polls files  │
    │  BridgeToolRegistry          │
    │  BridgeToolRunner            │
    │  [BridgeTool] methods        │
    └───────────────────────────────┘
```

**Dual-channel design**: The same Unity plugin serves both Skill mode (direct CLI) and MCP mode (protocol server). Both channels communicate through the same file-based IPC — a pair of request/response files on disk. No network sockets, no port conflicts, no firewall rules.

**Why file IPC?** Unity's main thread is single-threaded and blocks during domain reload. File polling is the most reliable way to survive recompilation, play-mode transitions, and Editor restarts without losing messages.

---

## Add Your Own Tools

Expose any static method to AI with a single attribute:

```csharp
using UnityAiBridge;

[BridgeToolType]
public static partial class CustomTools
{
    [BridgeTool("custom-greet")]
    [System.ComponentModel.Description("Say hello")]
    public static string Greet(string name = "World")
    {
        return $"Hello, {name}!";
    }
}
```

The bridge discovers tools at Editor startup via reflection. No registration code, no config files. Parameters are automatically mapped to JSON Schema for the AI to call.

---

## Security

Unity AI Bridge runs **entirely on your local machine**. The file IPC channel is scoped to your user's temp directory, and no network listeners are opened.

See [SECURITY.md](SECURITY.md) for details.

---

## Compatibility

| Unity Version | Render Pipeline | Status |
|--------------|-----------------|--------|
| 2022.3 LTS+  | Built-in        | Supported |
| 2022.3 LTS+  | URP             | Supported |
| 2022.3 LTS+  | HDRP            | Supported |
| 6000.x (Unity 6) | All         | Supported |

**Platforms**: Windows, macOS

---

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

- Report bugs and request features via [GitHub Issues](https://github.com/butterlatte-zhang/unity-ai-bridge/issues)
- Submit pull requests against the `main` branch
- Add new tools by following the `[BridgeTool]` pattern above

---

## Acknowledgments

Unity AI Bridge is derived from [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) by Ivan Murzak (Apache License 2.0). See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for details.

## License

[Apache License 2.0](LICENSE)
