# r/unity3d Post

**Subreddit:** r/unity3d
**Flair:** Resources/Tools (if available)

---

**Title:** I built an open-source MCP bridge that lets AI agents control the Unity Editor — before Unity 6.2 announced theirs

**Body:**

Hey r/unity3d,

For the past year I've been working on a large open-world Unity game (50+ devs, 2M+ lines of C#), and one of the biggest pain points was the gap between AI coding assistants and the Unity Editor. AI can read and write .cs files, but it's completely blind to scenes, components, materials, profiler data, etc.

So I built **Unity AI Bridge** — an open-source tool that gives AI full editor access via 65 MCP tools. Then Unity 6.2 dropped their official AI Gateway doing the same thing. Felt good to be validated, but also made me realize how many people need this.

**The key difference: Unity AI Bridge works on 2022.3 LTS+**, not just Unity 6.2. If your project isn't on Unity 6 yet (most aren't), this is your only option.

### What it does

AI can now do things like:
- Inspect and modify scenes, GameObjects, components
- Create/edit prefabs and materials
- Run profiler snapshots, analyze hotpaths and GC allocations
- Generate and bake light probe grids
- Execute C# code directly in the editor
- Run tests, manage packages
- Call any static method via reflection

### Quick comparison with Unity 6 AI Gateway

| | Unity AI Bridge | Unity 6 AI Gateway |
|---|---|---|
| Unity version | **2022.3 LTS+** | 6.2+ only |
| Tools | 65 across 15 categories | General-purpose |
| Profiler/LightProbe/Reflection | Yes | Not yet |
| IPC | File-based (~100ms, debug-friendly) | Unix Socket |
| Dependencies | Zero (Python stdlib + C#) | TBD |

### How it works

File-based IPC between a Python MCP server and a C# Unity Editor plugin. No ports, no WebSocket, no npm/Node.js. Survives recompilation, play-mode transitions, and editor restarts. The ~100ms polling latency is invisible because AI think-time dominates every round trip.

Works with: Claude Code, Cursor, GitHub Copilot, Windsurf, Claude Desktop.

### Extensibility

Add your own tools in 5 lines:

```csharp
[BridgeToolType]
public static partial class MyTools
{
    [BridgeTool("my-tool")]
    [Description("Do something cool")]
    public static string DoSomething(string input = "default")
    {
        return $"Result: {input}";
    }
}
```

GitHub: https://github.com/butterlatte-zhang/unity-ai-bridge

Happy to answer any questions. This was born from real production needs, not a weekend hack.
