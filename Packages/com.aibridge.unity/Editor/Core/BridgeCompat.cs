#nullable enable

using System.Threading.Tasks;
using UnityAiBridge.Logger;
using UnityAiBridge.Utils;
using UnityEngine;

namespace UnityAiBridge.Editor.Tools
{
    /// <summary>
    /// Compatibility layer providing logging, log collection, and deferred result notification.
    /// Delegates to BridgePlugin for core functionality.
    /// </summary>
    public class BridgeCompat
    {
        private static BridgeCompat? _instance;
        private UnityLogCollector? _logCollector;

        public static bool HasInstance => true;

        public static BridgeCompat Instance
        {
            get
            {
                _instance ??= new BridgeCompat();
                return _instance;
            }
        }

        public UnityLogCollector? LogCollector
        {
            get
            {
                if (_logCollector == null)
                {
                    try
                    {
                        var storage = new BufferedFileLogStorage(
                            logger: BridgeLoggerFactory.CreateLogger("LogCollector"));
                        _logCollector = new UnityLogCollector(storage);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[BridgeCompat] Failed to initialize LogCollector: {e.Message}");
                    }
                }
                return _logCollector;
            }
        }

        public static bool IsLogEnabled(LogLevel level)
        {
            return true;
        }

        public void LogTrace(string message, System.Type? type = null)
        {
            Debug.Log($"[Bridge:Trace] {message}");
        }

        public void LogInfo(string message, System.Type? type = null)
        {
            Debug.Log($"[Bridge:Info] {message}");
        }

        public void LogInfo(string format, params object[] args)
        {
            try
            {
                Debug.Log($"[Bridge:Info] {string.Format(format, args)}");
            }
            catch
            {
                Debug.Log($"[Bridge:Info] {format}");
            }
        }

        public void EnsureBridgeInitialized()
        {
            BridgePlugin.EnsureInitialized();
        }

        public static void ConnectIfNeeded()
        {
            // No-op: file-based IPC doesn't need connection
        }

        /// <summary>
        /// Handles deferred tool completion. In the file-based system,
        /// we write the result to a pending results file for the next poll.
        /// </summary>
        public static Task NotifyToolRequestCompleted(RequestToolCompletedData data)
        {
            if (data?.Result == null)
                return Task.CompletedTask;

            var requestId = data.Result.RequestID;
            if (string.IsNullOrEmpty(requestId))
                return Task.CompletedTask;

            // Write deferred result to the bridge results directory
            try
            {
                var projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath)!;
                var resultsDir = System.IO.Path.Combine(projectRoot, "Temp", "UnityBridge", "results");
                if (!System.IO.Directory.Exists(resultsDir))
                    System.IO.Directory.CreateDirectory(resultsDir);

                var status = data.Result.ResponseStatus == ResponseCallTool.Status.Error ? "error" : "success";
                var json = $"{{\"status\":\"{status}\",\"message\":{System.Text.Json.JsonSerializer.Serialize(data.Result.Message)}}}";

                // Write using the requestId as the filename so bridge.py can pick it up
                var resultFile = System.IO.Path.Combine(resultsDir, $"{requestId}.json");
                BridgeFileUtils.WriteAtomically(resultFile, json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BridgeCompat] Failed to write deferred result: {e.Message}");
            }

            return Task.CompletedTask;
        }
    }
}
