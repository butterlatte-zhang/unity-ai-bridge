
#nullable enable
using System;
using System.ComponentModel;
using UnityAiBridge;
using UnityAiBridge.Serialization;
using UnityAiBridge.Utils;
using UnityAiBridge.Editor.Utils;
using UnityAiBridge.Data;
using UnityAiBridge.Extensions;
using UnityAiBridge.Logger;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Assets
    {
        public const string AssetsGetDataToolId = "assets-get-data";
        [BridgeTool
        (
            AssetsGetDataToolId,
            Title = "Assets / Get Data"
        )]
        [Description("Get asset data from the asset file in the Unity project. " +
            "It includes all serializable fields and properties of the asset. " +
            "Use '" + AssetsFindToolId + "' tool to find asset before using this tool.")]
        public SerializedMember GetData(AssetObjectRef assetRef)
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(assetRef));

            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                var asset = assetRef.FindAssetObject();
                if (asset == null)
                {
                    // Built-in assets fallback (uses cached assets to avoid repeated expensive LoadAllAssetsAtPath calls)
                    if (!string.IsNullOrEmpty(assetRef.AssetPath) && assetRef.AssetPath!.StartsWith(ExtensionsRuntimeObject.UnityEditorBuiltInResourcesPath))
                    {
                        var targetName = System.IO.Path.GetFileNameWithoutExtension(assetRef.AssetPath);
                        var ext = System.IO.Path.GetExtension(assetRef.AssetPath);
                        asset = BuiltInAssetCache.FindAssetByExtension(targetName, ext);
                    }
                }

                if (asset == null)
                    throw new Exception(Error.NotFoundAsset(assetRef.AssetPath!, assetRef.AssetGuid ?? "N/A"));

                var reflector = BridgePlugin.Reflector;

                return reflector.Serialize(
                    obj: asset,
                    name: asset.name,
                    recursive: true,
                    logger: BridgeLoggerFactory.CreateLogger<Tool_Assets>()
                );
            });
        }
    }
}