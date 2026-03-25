# Third-Party Notices

Unity AI Bridge includes code derived from the following open-source projects.

---

## Unity-MCP

- **Repository**: https://github.com/IvanMurzak/Unity-MCP
- **License**: Apache License 2.0
- **Copyright**: Copyright (c) 2025 Ivan Murzak

Unity AI Bridge was originally derived from Unity-MCP's core architecture, including the tool registry pattern, file-based IPC mechanism, and serialization framework. Significant modifications have been made, including:

- Complete namespace rename (`Unity-MCP` → `UnityAiBridge`)
- Rewritten MCP server (pure Python stdlib, zero dependencies)
- Added CLI bridge for Claude Code Skill mode
- Extended tool set (65 tools across 15 categories)
- Added Unity type JSON converters (Vector, Color, Quaternion, Bounds, etc.)
- Added Profiler, LightProbe, and Tests tool categories
- Attribute-based tool auto-discovery system

---

## Microsoft.CodeAnalysis (Roslyn)

- **Repository**: https://github.com/dotnet/roslyn
- **License**: MIT License
- **Copyright**: Copyright (c) .NET Foundation and Contributors

The following Roslyn compiler DLLs are bundled in `Packages/com.aibridge.unity/Editor/Plugins/` to support the `script-execute` tool (runtime C# compilation):

- `Microsoft.CodeAnalysis.dll`
- `Microsoft.CodeAnalysis.CSharp.dll`
- `System.Collections.Immutable.dll`
- `System.Reflection.Metadata.dll`

Full license text: https://github.com/dotnet/roslyn/blob/main/License.txt

---

## System.Text.Json and Dependencies

- **Repository**: https://github.com/dotnet/runtime
- **License**: MIT License
- **Copyright**: Copyright (c) .NET Foundation and Contributors

The following .NET runtime DLLs are bundled in `Packages/com.aibridge.unity/Editor/Plugins/` to provide JSON serialization on Unity 2022.3+:

- `System.Text.Json.dll` (7.0.3)
- `System.Text.Encodings.Web.dll` (7.0.0)
- `System.Runtime.CompilerServices.Unsafe.dll` (6.0.0)
- `Microsoft.Bcl.AsyncInterfaces.dll` (7.0.0)

Full license text: https://github.com/dotnet/runtime/blob/main/LICENSE.TXT
