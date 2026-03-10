#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace UnityAiBridge.Editor
{
    /// <summary>
    /// 文件 IPC 桥接器：轮询 Temp/UnityBridge/commands/ 目录，调用 Bridge 工具，写结果到 Temp/UnityBridge/results/。
    /// Domain Reload 后自动恢复。
    /// </summary>
    [InitializeOnLoad]
    public static class FileBridgePoller
    {
        private static readonly string BridgeRoot = Path.Combine(
            Directory.GetParent(Application.dataPath)!.FullName, "Temp", "UnityBridge");

        private static readonly string CommandsDir = Path.Combine(BridgeRoot, "commands");
        private static readonly string ResultsDir = Path.Combine(BridgeRoot, "results");
        private static readonly string HeartbeatFile = Path.Combine(BridgeRoot, "heartbeat");

        private const double PollIntervalSeconds = 0.1;
        private const double ResultExpireSeconds = 300.0; // 5 分钟

        private static readonly int ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
        private static readonly JsonSerializerOptions CompactJsonOptions = new() { WriteIndented = false };

        private static double _lastPollTime;
        private static double _lastCleanupTime;
        private static double _lastHeartbeatTime;
        private static bool _pendingExecution;
        private static string? _pendingCommandId;
        private static string? _pendingToolName;
        private static Dictionary<string, JsonElement>? _pendingParams;

        // 异步工具执行状态
        private static Task? _asyncTask;
        private static string? _asyncCommandId;

        static FileBridgePoller()
        {
            EnsureDirectories();
            EditorApplication.update += OnEditorUpdate;
            _lastPollTime = EditorApplication.timeSinceStartup;
            _lastCleanupTime = EditorApplication.timeSinceStartup;
            Debug.Log("[UnityAiBridge] FileBridgePoller registered (Temp/UnityBridge).");
        }

        private static void EnsureDirectories()
        {
            try
            {
                Directory.CreateDirectory(CommandsDir);
                Directory.CreateDirectory(ResultsDir);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityAiBridge] Failed to create bridge directories: {e.Message}");
            }
        }

        private static void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;

            // 更新 heartbeat
            UpdateHeartbeat(now);

            // 轮询间隔
            if (now - _lastPollTime < PollIntervalSeconds)
                return;
            _lastPollTime = now;

            // 检查异步任务是否完成
            if (_asyncTask != null)
            {
                CheckAsyncTaskCompletion();
                return; // 异步任务进行中，不接受新命令
            }

            // 如果有正在执行的任务，处理它
            if (_pendingExecution)
            {
                ExecutePendingTool();
                return;
            }

            // 扫描新命令
            ProcessNextCommand();

            // 定期清理过期结果
            if (now - _lastCleanupTime > 60.0)
            {
                _lastCleanupTime = now;
                CleanupExpiredResults();
            }
        }

        #region Heartbeat

        private static void UpdateHeartbeat(double now)
        {
            if (now - _lastHeartbeatTime < 1.0)
                return;
            _lastHeartbeatTime = now;

            try
            {
                var content = new Dictionary<string, object>
                {
                    ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ["pid"] = ProcessId,
                    ["unityVersion"] = Application.unityVersion
                };
                BridgeFileUtils.WriteAtomically(HeartbeatFile, JsonSerializer.Serialize(content));
            }
            catch
            {
                // 忽略 heartbeat 写入失败
            }
        }

        #endregion

        #region 命令处理

        private static void ProcessNextCommand()
        {
            string[] files;
            try
            {
                if (!Directory.Exists(CommandsDir))
                    return;
                files = Directory.GetFiles(CommandsDir, "*.json");
            }
            catch
            {
                return;
            }

            if (files.Length == 0)
                return;

            // 按文件名排序，保证先进先处理
            Array.Sort(files);
            string commandFile = files[0];

            string json;
            try
            {
                json = File.ReadAllText(commandFile);
                File.Delete(commandFile);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityAiBridge] Failed to read command file: {e.Message}");
                return;
            }

            // 用文件名作为 fallback ID
            var fallbackId = Path.GetFileNameWithoutExtension(commandFile);

            CommandPayload? command;
            try
            {
                command = JsonSerializer.Deserialize<CommandPayload>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityAiBridge] Failed to parse command JSON: {e.Message}");
                WriteErrorResult(fallbackId, $"Failed to parse command JSON: {e.Message}");
                return;
            }

            if (command == null || string.IsNullOrEmpty(command.id) || string.IsNullOrEmpty(command.tool))
            {
                Debug.LogError("[UnityAiBridge] Invalid command: missing id or tool.");
                WriteErrorResult(command?.id ?? fallbackId, "Invalid command: missing id or tool.");
                return;
            }

            Debug.Log($"[UnityAiBridge] Executing tool: {command.tool} (id: {command.id})");

            // 构建请求参数
            Dictionary<string, JsonElement>? parameters = null;
            if (command.@params != null)
            {
                try
                {
                    var paramsJson = command.@params.Value.GetRawText();
                    parameters = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(paramsJson);
                }
                catch (Exception e)
                {
                    WriteErrorResult(command.id!, $"Failed to parse parameters: {e.Message}");
                    return;
                }
            }

            // 标记待执行，下一帧在主线程执行
            _pendingCommandId = command.id;
            _pendingToolName = command.tool;
            _pendingParams = parameters;
            _pendingExecution = true;
        }

        private static void ExecutePendingTool()
        {
            if (!_pendingExecution || _pendingCommandId == null || _pendingToolName == null)
                return;

            var commandId = _pendingCommandId;
            var toolName = _pendingToolName;
            var parameters = _pendingParams;

            _pendingExecution = false;
            _pendingCommandId = null;
            _pendingToolName = null;
            _pendingParams = null;

            try
            {
                // 确保 BridgePlugin 已初始化
                BridgePlugin.EnsureInitialized();

                var result = BridgePlugin.Runner.Execute(toolName, parameters);

                // 异步工具：存储 Task，下一帧轮询完成状态
                if (result.Status == BridgeToolStatus.Pending && result.AsyncTask != null)
                {
                    _asyncTask = result.AsyncTask;
                    _asyncCommandId = commandId;
                    Debug.Log($"[UnityAiBridge] Tool '{toolName}' is async, polling for completion...");
                    return;
                }

                WriteResult(commandId, result);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityAiBridge] Exception executing tool {toolName}: {e.Message}");
                WriteErrorResult(commandId, $"Exception: {e.Message}");
            }
        }

        private static void CheckAsyncTaskCompletion()
        {
            if (_asyncTask == null || _asyncCommandId == null)
                return;

            if (!_asyncTask.IsCompleted)
                return;

            var commandId = _asyncCommandId;
            var task = _asyncTask;

            _asyncTask = null;
            _asyncCommandId = null;

            try
            {
                var result = BridgePlugin.Runner.SerializeTaskResult(task);
                WriteResult(commandId, result);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityAiBridge] Failed to process async result: {e.Message}");
                WriteErrorResult(commandId, $"Failed to process async result: {e.Message}");
            }
        }

        #endregion

        #region 结果写入

        private static void WriteResult(string commandId, BridgeToolResult result)
        {
            var status = result.Status == BridgeToolStatus.Success ? "success" : "error";

            var resultPayload = new ResultPayload
            {
                id = commandId,
                status = status,
                message = result.Message,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var json = JsonSerializer.Serialize(resultPayload, CompactJsonOptions);
            var resultPath = Path.Combine(ResultsDir, $"{commandId}.json");
            BridgeFileUtils.WriteAtomically(resultPath, json);

            Debug.Log($"[UnityAiBridge] Result written for {commandId}: {status}");
        }

        private static void WriteErrorResult(string commandId, string errorMessage)
        {
            var resultPayload = new ResultPayload
            {
                id = commandId,
                status = "error",
                message = errorMessage,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var json = JsonSerializer.Serialize(resultPayload, CompactJsonOptions);
            var resultPath = Path.Combine(ResultsDir, $"{commandId}.json");
            BridgeFileUtils.WriteAtomically(resultPath, json);
        }

        #endregion

        #region 清理

        private static void CleanupExpiredResults()
        {
            try
            {
                if (!Directory.Exists(ResultsDir))
                    return;

                foreach (var file in Directory.GetFiles(ResultsDir, "*.json"))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        if ((DateTime.UtcNow - info.LastWriteTimeUtc).TotalSeconds > ResultExpireSeconds)
                        {
                            File.Delete(file);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        #endregion

        #region 数据模型

        private class CommandPayload
        {
            public string? id { get; set; }
            public string? tool { get; set; }
            public JsonElement? @params { get; set; }
        }

        private class ResultPayload
        {
            public string? id { get; set; }
            public string? status { get; set; }
            public string? message { get; set; }
            public long timestamp { get; set; }
        }

        #endregion
    }
}
