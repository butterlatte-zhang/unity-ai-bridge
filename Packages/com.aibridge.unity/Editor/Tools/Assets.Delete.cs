
#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityAiBridge.Editor.Utils;
using UnityAiBridge.Logger;
using UnityEditor;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Assets
    {
        public const string AssetsDeleteToolId = "assets-delete";
        [BridgeTool
        (
            AssetsDeleteToolId,
            Title = "Assets / Delete"
        )]
        [Description("Delete the assets at paths from the project. " +
            "Does AssetDatabase.Refresh() at the end. " +
            "Use '" + AssetsFindToolId + "' tool to find assets before deleting.")]
        public DeleteAssetsResponse Delete
        (
            [Description("The paths of the assets")]
            string[] paths
        )
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                var logger = BridgeLoggerFactory.CreateLogger<Tool_Assets>();

                if (paths.Length == 0)
                    throw new System.Exception(Error.SourcePathsArrayIsEmpty());

                logger.LogInformation($"Deleting {paths.Length} asset(s): {string.Join(", ", paths)}");

                var response = new DeleteAssetsResponse();
                var outFailedPaths = new List<string>();
                var success = AssetDatabase.DeleteAssets(paths, outFailedPaths);

                if (!success)
                {
                    response.Errors ??= new();
                    foreach (var failedPath in outFailedPaths)
                    {
                        logger.LogWarning($"Failed to delete asset at '{failedPath}'");
                        response.Errors.Add($"Failed to delete asset at {failedPath}.");
                    }
                }

                // Add successfully deleted paths
                foreach (var path in paths)
                {
                    if (!outFailedPaths.Contains(path))
                    {
                        logger.LogInformation($"Successfully deleted asset at '{path}'");
                        response.DeletedPaths ??= new();
                        response.DeletedPaths.Add(path);
                    }
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorUtils.RepaintAllEditorWindows();

                return response;
            });
        }

        public class DeleteAssetsResponse
        {
            [Description("List of paths of deleted assets.")]
            public List<string>? DeletedPaths { get; set; }
            [Description("List of errors encountered during delete operations.")]
            public List<string>? Errors { get; set; }
        }
    }
}