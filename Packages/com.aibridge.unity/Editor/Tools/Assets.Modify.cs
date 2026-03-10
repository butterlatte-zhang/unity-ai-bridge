
#nullable enable
using System;
using System.ComponentModel;
using System.Linq;
using UnityAiBridge;
using UnityAiBridge.Serialization;
using UnityAiBridge.Utils;
using UnityAiBridge.Editor.Utils;
using UnityAiBridge.Data;
using UnityAiBridge.Extensions;
using UnityAiBridge.Logger;
using UnityEditor;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Assets
    {
        public const string AssetsModifyToolId = "assets-modify";
        [BridgeTool
        (
            AssetsModifyToolId,
            Title = "Assets / Modify"
        )]
        [Description("Modify asset file in the project. " +
            "Use '" + AssetsGetDataToolId + "' tool first to inspect the asset structure before modifying. " +
            "Not allowed to modify asset file in 'Packages/' folder. Please modify it in 'Assets/' folder.")]
        public string[] Modify
        (
            AssetObjectRef assetRef,
            [Description("The asset content. It overrides the existing asset content.")]
            SerializedMember content
        )
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var assetValidationError))
                throw new ArgumentException(assetValidationError, nameof(assetRef));

            if (assetRef.AssetPath?.StartsWith("Packages/") == true)
                throw new ArgumentException($"Not allowed to modify asset in '/Packages' folder. Please modify it in '/Assets' folder. Path: '{assetRef.AssetPath}'.", nameof(assetRef));

            if (assetRef.AssetPath?.StartsWith(ExtensionsRuntimeObject.UnityEditorBuiltInResourcesPath) == true)
                throw new ArgumentException($"Not allowed to modify built-in asset. Path: '{assetRef.AssetPath}'.", nameof(assetRef));

            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                var asset = assetRef.FindAssetObject(); // AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset == null)
                    throw new Exception($"Asset not found using the reference:\n{assetRef}");

                // Fixing instanceID - inject expected instance ID into the valueJsonElement
                content.valueJsonElement.SetProperty(ObjectRef.ObjectRefProperty.InstanceID, asset.GetInstanceID());

                var obj = (object)asset;
                var logs = new Logs();

                var success = BridgePlugin.Reflector.TryPopulate(
                    ref obj,
                    data: content,
                    logs: logs,
                    logger: BridgeLoggerFactory.CreateLogger<Tool_Assets>());

                if (success)
                    EditorUtility.SetDirty(asset);

                // AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorUtils.RepaintAllEditorWindows();

                return logs
                    .Select(log => log.ToString())
                    .ToArray();
            });
        }
    }
}
