# r/ClaudeAI Post

**Subreddit:** r/ClaudeAI
**Flair:** MCP / Tools (if available)

---

**Title:** MCP server with 65 tools for controlling Unity Editor — open source, zero dependencies

**Body:**

Built an MCP server that gives Claude (and other AI IDEs) full access to the Unity game engine editor. 65 tools covering scene management, profiling, asset operations, light probes, screenshot, runtime query/invoke, and more.

**Why this is useful:** Claude can already read/write .cs files, but it's blind to what's happening inside Unity. With this bridge, Claude can inspect your scene hierarchy, modify components, take profiler snapshots to find performance issues, bake lighting, run tests — basically anything you'd do manually in the editor.

**Example workflow:** "Profile my game, find the top 3 GC allocation sources, and fix them" — Claude takes a profiler snapshot, identifies hotpaths, reads the relevant scripts, and makes targeted fixes. All in one conversation.

**Technical approach:**
- File-based IPC (not WebSocket) — no ports, no firewall issues
- Pure Python stdlib MCP server — no npm, no pip install
- Works with Claude Code (Skill mode), Claude Desktop (MCP mode), Cursor, Copilot, Windsurf
- Unity 2022.3 LTS+ compatible

**Setup in Claude Desktop:**
```json
{
  "mcpServers": {
    "unity-bridge": {
      "command": "python3",
      "args": [".claude/skills/unity-bridge/mcp_server.py"],
      "cwd": "/path/to/unity-project"
    }
  }
}
```

GitHub: https://github.com/butterlatte-zhang/unity-ai-bridge

Unity just shipped a similar feature in Unity 6.2 (AI Gateway), but this works on 2022.3+ and has deeper tooling (Profiler, LightProbe, Reflection).
