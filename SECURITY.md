# Security

Unity AI Bridge runs **entirely on your local machine**. All communication happens via local file I/O — no network ports are opened.

## Dangerous Tools

These tools can execute arbitrary code and are included by default:

| Tool | Risk | What it does |
|------|------|-------------|
| `script-execute` | **Critical** | Compiles and runs arbitrary C# via Roslyn |
| `reflection-method-call` | **Critical** | Invokes any .NET method by name, including private methods |

To disable them, delete the corresponding source files from the package:
- `Packages/com.aibridge.unity/Editor/Tools/Script.Execute.cs`
- `Packages/com.aibridge.unity/Editor/Tools/Reflection.MethodCall.cs`

## IPC Security Model

- Commands are exchanged via files in `{ProjectRoot}/Temp/UnityBridge/`.
- No authentication — any local process with filesystem access can send commands. This matches the threat model of LSP/DAP and similar local dev tooling.
- Command files are deleted immediately after reading. Result files expire after 5 minutes.
- The `Temp/` directory is destroyed when Unity closes the project.
