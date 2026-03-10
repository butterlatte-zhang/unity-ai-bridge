
#nullable enable
using System.ComponentModel;
using UnityAiBridge;
using UnityAiBridge.Utils;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Editor
    {
        public const string EditorApplicationGetStateToolId = "editor-application-get-state";
        [BridgeTool
        (
            EditorApplicationGetStateToolId,
            Title = "Editor / Application / Get State"
        )]
        [Description("Returns available information about 'UnityEditor.EditorApplication'. " +
            "Use it to get information about the current state of the Unity Editor application. " +
            "Such as: playmode, paused state, compilation state, etc.")]
        public EditorStatsData? GetApplicationState()
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                return EditorStatsData.FromEditor();
            });
        }
    }
}
