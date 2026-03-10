
#nullable enable
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityAiBridge;

using UnityAiBridge.Utils;
using UnityAiBridge.Editor.Utils;
using UnityEditor;

namespace UnityAiBridge.Editor.Tools
{
    public static partial class Tool_Script
    {
        public const string ScriptDeleteToolId = "script-delete";
        [BridgeTool
        (
            ScriptDeleteToolId,
            Title = "Script / Delete"
        )]
        [Description("Delete the script file(s). " +
            "Does AssetDatabase.Refresh() and waits for Unity compilation to complete before reporting results. " +
            "Use '" + ScriptReadToolId + "' tool to read existing script files first.")]
        public static ResponseCallTool Delete
        (
            [Description("File paths to the files. Sample: \"Assets/Scripts/MyScript.cs\".")]
            string[] files,
            [RequestID]
            string? requestId = null
        )
        {
            if (files == null || files.Length == 0)
                return ResponseCallTool.Error(Error.ScriptPathIsEmpty()).SetRequestID(requestId);

            if (files.Any(f => string.IsNullOrEmpty(f)))
                return ResponseCallTool.Error(Error.ScriptPathIsEmpty()).SetRequestID(requestId);

            if (!files.All(f => f.EndsWith(".cs")))
                return ResponseCallTool.Error(Error.FilePathMustEndsWithCs()).SetRequestID(requestId);

            var invalidFiles = files.Where(f => !File.Exists(f)).ToArray();
            if (invalidFiles.Length > 0)
                return ResponseCallTool.Error(Error.ScriptFileNotFound(invalidFiles)).SetRequestID(requestId);

            foreach (var f in files)
            {
                File.Delete(f);
                if (File.Exists(f + ".meta"))
                    File.Delete(f + ".meta");
            }

            UnityAiBridge.Utils.MainThread.Instance.RunAsync(async () =>
            {
                await Task.Yield();
                // Schedule notification to be sent after compilation completes (survives domain reload)
                ScriptUtils.SchedulePostCompilationNotification(requestId, string.Join(",", files), "Script deletion");

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            });

            var scriptWord = files.Length > 1 ? "Scripts" : "Script";
            return ResponseCallTool.Processing($"{scriptWord} deleted. Refreshing AssetDatabase and waiting for compilation to complete...").SetRequestID(requestId);
        }
    }
}
