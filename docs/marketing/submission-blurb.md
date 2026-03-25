# Universal Submission Blurb

Use this for Smithery, mcp.so, PulseMCP, mcpservers.org, LobeHub, etc.

---

## Short description (one line)

Remote-control Unity Editor from any AI IDE — 65 tools covering scene, asset, prefab, profiler, light probe, screenshot, runtime, and more. File-based IPC, zero dependencies, Unity 2022.3 LTS+.

## Full description

Unity AI Bridge is an open-source MCP server that gives AI agents full access to the Unity Editor. It provides 65 tools across 15 categories:

- **Scene** — open, save, create, inspect scenes
- **GameObject** — find, create, destroy, modify objects and components
- **Assets** — search, modify, move, copy, create materials
- **Prefab** — create, open, save, instantiate prefabs
- **Script** — read, write, delete, execute C# code
- **Screenshot** — capture Game view or Scene view as PNG
- **Runtime** — query MonoBehaviour fields, invoke static methods in Play Mode
- **Profiler** — snapshot, stream, frame hierarchy, hotpath, GC allocation analysis
- **Light Probe** — generate grids, analyze, bake, configure
- **Reflection** — find and call any static method
- **Tests** — run EditMode/PlayMode tests
- **Editor** — get/set play state, selection
- **Package Manager** — list, search, add, remove packages

**Technical highlights:**
- File-based IPC (not WebSocket) — no open ports, survives recompilation
- Zero external dependencies — pure Python stdlib + self-contained C# package
- Works with Claude Code, Cursor, GitHub Copilot, Windsurf, Claude Desktop
- Unity 2022.3 LTS+ and Unity 6 compatible
- Apache 2.0 licensed

## Categories / Tags

game-engine, unity, unity3d, game-development, mcp-server, ai-tools, profiler, editor, development-tools

## GitHub URL

https://github.com/butterlatte-zhang/unity-ai-bridge
