
#nullable enable
using System;
using System.ComponentModel;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityAiBridge.Editor.Utils;
using UnityEditor;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Assets
    {
        public const string AssetsMoveToolId = "assets-move";
        [BridgeTool
        (
            AssetsMoveToolId,
            Title = "Assets / Move"
        )]
        [Description("Move the assets at paths in the project. " +
            "Should be used for asset rename. " +
            "Does AssetDatabase.Refresh() at the end. " +
            "Use '" + AssetsFindToolId + "' tool to find assets before moving.")]
        public string[] Move
        (
            [Description("The paths of the assets to move.")]
            string[] sourcePaths,
            [Description("The paths of moved assets.")]
            string[] destinationPaths
        )
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                if (sourcePaths.Length == 0)
                    throw new ArgumentException(Error.SourcePathsArrayIsEmpty(), nameof(sourcePaths));

                if (sourcePaths.Length != destinationPaths.Length)
                    throw new ArgumentException(Error.SourceAndDestinationPathsArrayMustBeOfTheSameLength());

                var logs = new string[sourcePaths.Length];

                for (int i = 0; i < sourcePaths.Length; i++)
                {
                    var error = AssetDatabase.MoveAsset(sourcePaths[i], destinationPaths[i]);
                    logs[i] = string.IsNullOrEmpty(error)
                        ? $"[Success] Moved asset from {sourcePaths[i]} to {destinationPaths[i]}."
                        : $"[Error] Failed to move asset from {sourcePaths[i]} to {destinationPaths[i]}: {error}.";
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorUtils.RepaintAllEditorWindows();
                return logs;
            });
        }
    }
}
