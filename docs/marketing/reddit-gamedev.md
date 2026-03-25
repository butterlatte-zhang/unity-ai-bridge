# r/gamedev Post

**Subreddit:** r/gamedev
**Flair:** (none or "Tools")

---

**Title:** Open-sourced 65 tools that let AI agents remote-control Unity Editor — profiling, scene editing, light probes, and more

**Body:**

I've been building a large Unity game with 50+ developers, and we kept running into the same problem: AI coding assistants are great at writing C# files, but they can't see or touch anything inside the Unity Editor.

So I built **Unity AI Bridge** — it connects AI IDEs (Claude Code, Cursor, Copilot, Windsurf) to Unity Editor through MCP (Model Context Protocol). The AI gets 65 tools to work with:

**What AI can actually do now:**
- Open/create/modify scenes
- Find, create, destroy GameObjects and components
- Create materials, prefabs, manage assets
- Take profiler snapshots, find hotpaths, track GC allocations
- Generate light probe grids and bake lighting
- Execute arbitrary C# in the editor
- Run unit tests
- Call any static method via reflection

**The boring-but-important technical bits:**
- Works on **Unity 2022.3 LTS+** (not just Unity 6)
- File-based IPC — no open ports, no WebSocket, survives recompilation and play-mode switches
- Zero external dependencies — pure Python stdlib + self-contained C# package
- Add custom tools with a single `[BridgeTool]` attribute

Unity recently announced a similar feature (AI Gateway) in Unity 6.2, which is great validation. But most production projects won't be on Unity 6 for a while, and the official version doesn't cover deep tools like Profiler integration or LightProbe operations.

GitHub: https://github.com/butterlatte-zhang/unity-ai-bridge
Apache 2.0 licensed.

If you're using AI assistants with Unity, I'd love to hear what tools/workflows you wish existed.
