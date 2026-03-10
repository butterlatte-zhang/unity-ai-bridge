
#nullable enable
using System;
using System.ComponentModel;
using System.IO;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityAiBridge.Editor.Utils;
using UnityAiBridge.Data;
using UnityAiBridge.Logger;
using UnityEditor;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Assets
    {
        public const string AssetsMaterialCreateToolId = "assets-material-create";
        [BridgeTool
        (
            AssetsMaterialCreateToolId,
            Title = "Assets / Create Material"
        )]
        [Description("Create new material asset with default parameters. " +
            "Creates folders recursively if they do not exist. " +
            "Provide proper 'shaderName' - use '" + Tool_Assets_Shader.AssetsShaderListAllToolId + "' tool to find available shaders.")]
        public AssetObjectRef CreateMaterial
        (
            [Description("Asset path. Starts with 'Assets/'. Ends with '.mat'.")]
            string assetPath,
            [Description("Name of the shader that need to be used to create the material.")]
            string shaderName
        )
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(assetPath))
                    throw new ArgumentException(Error.EmptyAssetPath(), nameof(assetPath));

                if (!assetPath.StartsWith("Assets/"))
                    throw new ArgumentException(Error.AssetPathMustStartWithAssets(assetPath), nameof(assetPath));

                if (!assetPath.EndsWith(".mat"))
                    throw new ArgumentException(Error.AssetPathMustEndWithMat(assetPath), nameof(assetPath));

                var shader = UnityEngine.Shader.Find(shaderName);
                if (shader == null)
                    throw new ArgumentException(Error.ShaderNotFound(shaderName), nameof(shaderName));

                var material = new UnityEngine.Material(shader);

                // Create all folders in the path if they do not exist
                var directory = Path.GetDirectoryName(assetPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }

                AssetDatabase.CreateAsset(material, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                EditorUtils.RepaintAllEditorWindows();

                return new AssetObjectRef(material);
            });
        }
    }
}
