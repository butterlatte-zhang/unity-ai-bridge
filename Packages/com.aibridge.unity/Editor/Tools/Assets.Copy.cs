
#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityAiBridge.Editor.Utils;
using UnityAiBridge.Data;
using UnityEditor;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Assets
    {
        public const string AssetsCopyToolId = "assets-copy";
        [BridgeTool
        (
            AssetsCopyToolId,
            Title = "Assets / Copy"
        )]
        [Description("Copy the asset at path and stores it at newPath. " +
            "Does AssetDatabase.Refresh() at the end. " +
            "Use '" + AssetsFindToolId + "' tool to find assets before copying.")]
        public CopyAssetsResponse Copy
        (
            [Description("The paths of the asset to copy.")]
            string[] sourcePaths,
            [Description("The paths to store the copied asset.")]
            string[] destinationPaths
        )
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                if (sourcePaths.Length == 0)
                    throw new System.Exception(Error.SourcePathsArrayIsEmpty());

                if (sourcePaths.Length != destinationPaths.Length)
                    throw new System.Exception(Error.SourceAndDestinationPathsArrayMustBeOfTheSameLength());

                var response = new CopyAssetsResponse();

                for (var i = 0; i < sourcePaths.Length; i++)
                {
                    var sourcePath = sourcePaths[i];
                    var destinationPath = destinationPaths[i];

                    if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
                    {
                        response.Errors ??= new();
                        response.Errors.Add(Error.SourceOrDestinationPathIsEmpty());
                        continue;
                    }
                    if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                    {
                        response.Errors ??= new();
                        response.Errors.Add($"[Error] Failed to copy asset from {sourcePath} to {destinationPath}.");
                        continue;
                    }
                    var newAssetType = AssetDatabase.GetMainAssetTypeAtPath(destinationPath);
                    var newAsset = AssetDatabase.LoadAssetAtPath(destinationPath, newAssetType);

                    response.CopiedAssets ??= new();
                    response.CopiedAssets.Add(new AssetObjectRef(newAsset));
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorUtils.RepaintAllEditorWindows();

                return response;
            });
        }

        public class CopyAssetsResponse
        {
            [Description("List of copied assets.")]
            public List<AssetObjectRef>? CopiedAssets { get; set; }
            [Description("List of errors encountered during copy operations.")]
            public List<string>? Errors { get; set; }
        }
    }
}