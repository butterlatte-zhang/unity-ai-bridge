**[English](README.md)** | **[中文](README.zh-CN.md)**

# Unity AI Bridge

**从任何 AI IDE 远程控制 Unity 编辑器 — 无端口、无依赖、开箱即用。**

[![Unity 2022.3+](https://img.shields.io/badge/Unity-2022.3%2B-black?logo=unity)](#)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/butterlatte-zhang/unity-ai-bridge?style=social)](https://github.com/butterlatte-zhang/unity-ai-bridge/stargazers)
[![Release](https://img.shields.io/github/v/release/butterlatte-zhang/unity-ai-bridge)](https://github.com/butterlatte-zhang/unity-ai-bridge/releases)

---

## 为什么选择 Unity AI Bridge？

大多数 AI 编程助手只能读写文件，但它们对 Unity 编辑器**完全无感知** — 无法查看场景、调整材质、运行测试或分析性能。Unity AI Bridge 让 AI 获得完整的编辑器访问能力。

### 核心优势

- **62 个工具，13 个分类** — 场景、GameObject、资源、Prefab、脚本、Profiler、光照探针、测试等，覆盖完整的编辑器工作流，不仅仅是文件读写。
- **基于文件的 IPC，非 WebSocket** — 无开放端口、无防火墙问题、无连接中断。能安全度过重编译、PlayMode 切换和编辑器重启。
- **零外部依赖** — CLI/MCP 服务器用纯 Python 标准库，Unity 包完全自包含。不需要 pip install、npm、Node.js。
- **全主流 AI IDE** — Claude Code（Skill 模式）、Cursor、GitHub Copilot、Windsurf、Claude Desktop（MCP 模式）。一个 Unity 插件，所有 IDE 通用。
- **5 行代码可扩展** — 用 `[BridgeTool]` 属性添加自定义工具。自动发现、自动序列化、自动文档化，无需注册代码。
- **生产验证** — 在大型开放世界 Unity 游戏中实战验证（50+ 开发者，200 万+ 行 C#）。

---

## 快速开始

> **AI 原生项目** — 复制下方提示词发送给你的 AI 编程助手即可。[安装指南](docs/SETUP.md)是写给 AI 看的，你不需要自己阅读或执行任何命令。
>
> ```
> 帮我按照这个指南安装 Unity AI Bridge：
> https://github.com/butterlatte-zhang/unity-ai-bridge/blob/main/docs/SETUP.md
> ```

如果你更喜欢手动安装：

1. **Unity 包** — 在 Unity 中：*Window > Package Manager > + > Add package from git URL*：
   ```
   https://github.com/butterlatte-zhang/unity-ai-bridge.git?path=Packages/com.aibridge.unity
   ```
   或者手动将本仓库的 `Packages/com.aibridge.unity` 复制到你项目的 `Packages/` 目录下。

2. **IDE 集成** — 将 `.claude/` 复制到项目根目录，然后按 [docs/SETUP.md](docs/SETUP.md) 配置你的 IDE。

支持：Claude Code（Skill 模式）、Cursor、GitHub Copilot、Windsurf、Claude Desktop（MCP 模式）。

---

## 工具分类

62 个工具，分为 13 个类别：

| 分类 | 数量 | 工具 |
|------|:----:|------|
| **场景** | 7 | `scene-open` 打开 · `scene-save` 保存 · `scene-create` 创建 · `scene-list-opened` 列出 · `scene-get-data` 获取数据 · `scene-set-active` 设为活动 · `scene-unload` 卸载 |
| **游戏对象** | 11 | `gameobject-find` 查找 · `gameobject-create` 创建 · `gameobject-destroy` 删除 · `gameobject-modify` 修改 · `gameobject-duplicate` 复制 · `gameobject-set-parent` 设父 · `gameobject-component-add` 加组件 · `gameobject-component-destroy` 删组件 · `gameobject-component-get` 获取组件 · `gameobject-component-list-all` 列出组件 · `gameobject-component-modify` 改组件 |
| **资源** | 11 | `assets-find` 搜索 · `assets-find-built-in` 内置资源 · `assets-get-data` 获取数据 · `assets-modify` 修改 · `assets-move` 移动 · `assets-copy` 复制 · `assets-delete` 删除 · `assets-create-folder` 创建文件夹 · `assets-refresh` 刷新 · `assets-material-create` 创建材质 · `assets-shader-list-all` Shader 列表 |
| **预制体** | 5 | `assets-prefab-create` 创建 · `assets-prefab-open` 打开编辑 · `assets-prefab-save` 保存 · `assets-prefab-close` 关闭 · `assets-prefab-instantiate` 实例化 |
| **脚本** | 4 | `script-read` 读取 · `script-update-or-create` 创建/更新 · `script-delete` 删除 · `script-execute` 执行 C# 代码 |
| **对象** | 2 | `object-get-data` 获取数据 · `object-modify` 修改 |
| **编辑器** | 4 | `editor-application-get-state` 获取状态 · `editor-application-set-state` 设置状态 · `editor-selection-get` 获取选中 · `editor-selection-set` 设置选中 |
| **反射** | 2 | `reflection-method-find` 查找方法 · `reflection-method-call` 调用方法 |
| **控制台** | 1 | `console-get-logs` 获取日志 |
| **性能分析** | 5 | `profiler-snapshot` 快照 · `profiler-stream` 多帧流 · `profiler-frame-hierarchy` 帧层级 · `profiler-hotpath` 热点路径 · `profiler-gc-alloc` GC 分配 |
| **包管理** | 4 | `package-list` 已安装 · `package-search` 搜索 · `package-add` 安装 · `package-remove` 卸载 |
| **光照探针** | 5 | `lightprobe-generate-grid` 生成网格 · `lightprobe-analyze` 分析 · `lightprobe-bake` 烘焙 · `lightprobe-clear` 清除 · `lightprobe-configure-lights` 配置灯光 |
| **测试** | 1 | `tests-run` 运行测试 |

---

## 架构

```
┌──────────────────────────────────────────────────┐
│                   AI IDE                         │
│  (Claude Code / Cursor / Copilot / Windsurf)     │
└──────────┬────────────────────┬──────────────────┘
           │                    │
      Skill 模式            MCP 模式
           │                    │
           ▼                    ▼
    ┌─────────────┐    ┌──────────────┐
    │  bridge.py  │    │ mcp_server.py│
    │  (Python)   │    │  (Python)    │
    └──────┬──────┘    └──────┬───────┘
           │                  │
           └────────┬─────────┘
                    │
             基于文件的 IPC
           (请求 / 响应)
                    │
                    ▼
    ┌───────────────────────────────┐
    │     Unity Editor 插件        │
    │   (com.aibridge.unity)       │
    │                              │
    │  BridgePlugin ← 轮询文件     │
    │  BridgeToolRegistry          │
    │  BridgeToolRunner            │
    │  [BridgeTool] 标记的方法     │
    └───────────────────────────────┘
```

**双通道设计**：同一个 Unity 插件同时服务 Skill 模式（直接 CLI 调用）和 MCP 模式（协议服务器）。两种通道通过相同的文件 IPC 通信 — 磁盘上的请求/响应文件对。无网络 Socket、无端口冲突、无防火墙规则。

**为什么用文件 IPC？** Unity 主线程是单线程的，域重载时会阻塞。文件轮询是最可靠的方式，能安全度过重编译、PlayMode 切换和编辑器重启，不会丢失消息。

---

## 添加自定义工具

用一个属性将任意静态方法暴露给 AI：

```csharp
using UnityAiBridge;

[BridgeToolType]
public static partial class CustomTools
{
    [BridgeTool("custom-greet")]
    [System.ComponentModel.Description("打个招呼")]
    public static string Greet(string name = "World")
    {
        return $"Hello, {name}!";
    }
}
```

Bridge 在编辑器启动时通过反射自动发现工具。无需注册代码、无需配置文件。参数自动映射为 JSON Schema 供 AI 调用。

---

## 安全性

Unity AI Bridge **完全运行在本地机器上**。文件 IPC 通道限定在用户临时目录内，不开启任何网络监听。

详见 [SECURITY.md](SECURITY.md)。

---

## 兼容性

| Unity 版本 | 渲染管线 | 状态 |
|-----------|---------|------|
| 2022.3 LTS+ | Built-in | 支持 |
| 2022.3 LTS+ | URP | 支持 |
| 2022.3 LTS+ | HDRP | 支持 |
| 6000.x (Unity 6) | 全部 | 支持 |

**平台**：Windows、macOS

---

## 参与贡献

欢迎贡献！请参阅 [CONTRIBUTING.md](CONTRIBUTING.md) 了解指南。

- 通过 [GitHub Issues](https://github.com/butterlatte-zhang/unity-ai-bridge/issues) 报告 Bug 和提出功能请求
- 向 `main` 分支提交 Pull Request
- 按照上面的 `[BridgeTool]` 模式添加新工具

---

## 致谢

Unity AI Bridge 基于 [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP)（Ivan Murzak，Apache License 2.0）开发。详见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

## 许可证

[Apache License 2.0](LICENSE)
