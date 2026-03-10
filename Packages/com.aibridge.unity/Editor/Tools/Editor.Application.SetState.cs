
#nullable enable
using System;
using System.ComponentModel;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityAiBridge.Editor.Utils;
using UnityEditor;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Editor
    {
        public const string EditorApplicationSetStateToolId = "editor-application-set-state";
        [BridgeTool
        (
            EditorApplicationSetStateToolId,
            Title = "Editor / Application / Set State"
        )]
        [Description("Control the Unity Editor application state. " +
            "You can start, stop, or pause the 'playmode'. " +
            "Use '" + EditorApplicationGetStateToolId + "' tool to get the current state first.")]
        public EditorStatsData? SetApplicationState
        (
            [Description("If true, the 'playmode' will be started. If false, the 'playmode' will be stopped.")]
            bool isPlaying = false,
            [Description("If true, the 'playmode' will be paused. If false, the 'playmode' will be resumed.")]
            bool isPaused = false
        )
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                if (UnityEditor.EditorUtility.scriptCompilationFailed)
                {
                    var compilationErrorDetails = ScriptUtils.GetCompilationErrorDetails();
                    throw new Exception($"Unity project has compilation error. Please fix all compilation errors before doing this operation.\n{compilationErrorDetails}");
                }
                EditorApplication.isPlaying = isPlaying;
                EditorApplication.isPaused = isPaused;

                return EditorStatsData.FromEditor();
            });
        }
    }
}
