
#nullable enable
using System.ComponentModel;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityEditor;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Assets
    {
        public const string AssetsRefreshToolId = "assets-refresh";
        [BridgeTool
        (
            AssetsRefreshToolId,
            Title = "Assets / Refresh"
        )]
        [Description("Refreshes the AssetDatabase. " +
            "Use it if any file was added or updated in the project outside of Unity API. " +
            "Use it if need to force scripts recompilation when '.cs' file changed.")]
        public void Refresh(ImportAssetOptions options = ImportAssetOptions.ForceSynchronousImport)
        {
            UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                AssetDatabase.Refresh(options);
            });
        }
    }
}