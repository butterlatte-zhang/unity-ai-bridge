# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-03-10

### Added

- **65 tools** across 15 categories: Scene, GameObject, Assets, Prefab, Script, Object, Editor, Reflection, Screenshot, Runtime, Console, Profiler, Package, LightProbe, Tests
- **File-based IPC** — reliable local communication via `Temp/UnityBridge/`, no network ports
- **MCP Server** — stdio-based JSON-RPC server for Cursor, GitHub Copilot, Windsurf, Claude Desktop
- **Claude Code Skill** — direct integration via SKILL.md and parameter files
- **Attribute-based tool registry** — add custom tools with `[BridgeTool]` attribute, auto-discovered at startup
- **Unity type serialization** — full JSON support for Vector2/3/4, Color, Quaternion, Bounds, Matrix4x4, Rect, and more
- **Heartbeat system** — real-time Unity Editor availability detection
- **Roslyn integration** — execute arbitrary C# code via `script-execute` tool
- **Profiler tools** — snapshot, GC allocation tracking, hot path analysis, multi-frame streaming
- **Light probe tools** — generate, bake, analyze, and configure light probes
- **Zero external dependencies** — pure stdlib Python CLI/MCP, self-contained Unity package
