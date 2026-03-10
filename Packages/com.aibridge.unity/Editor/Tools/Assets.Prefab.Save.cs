
#nullable enable
using System;
using System.ComponentModel;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityAiBridge.Editor.Utils;
using UnityAiBridge.Data;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Assets_Prefab
    {
        public const string AssetsPrefabSaveToolId = "assets-prefab-save";
        [BridgeTool
        (
            AssetsPrefabSaveToolId,
            Title = "Assets / Prefab / Save"
        )]
        [Description("Save a prefab. " +
            "Use it when you are in prefab editing mode in Unity Editor. " +
            "Use '" + AssetsPrefabOpenToolId + "' tool to open a prefab first.")]
        public AssetObjectRef Save() => UnityAiBridge.Utils.MainThread.Instance.Run(() =>
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null)
                throw new InvalidOperationException(Error.PrefabStageIsNotOpened());

            var prefabGo = prefabStage.prefabContentsRoot;
            if (prefabGo == null)
                throw new InvalidOperationException(Error.PrefabStageIsNotOpened());

            var assetPath = prefabStage.assetPath;
            var goName = prefabGo.name;

            PrefabUtility.SaveAsPrefabAsset(prefabGo, assetPath);
            prefabStage.ClearDirtiness();

            EditorUtils.RepaintAllEditorWindows();

            var assetPrefab = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(assetPath);
            return new AssetObjectRef(assetPrefab);
        });
    }
}