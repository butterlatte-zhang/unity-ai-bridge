
#nullable enable
using System;
using System.ComponentModel;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityAiBridge.Editor.Utils;
using UnityAiBridge.Data;
using UnityEditor;
using UnityEngine;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Assets_Prefab
    {
        public const string AssetsPrefabInstantiateToolId = "assets-prefab-instantiate";
        [BridgeTool
        (
            AssetsPrefabInstantiateToolId,
            Title = "Assets / Prefab / Instantiate"
        )]
        [Description("Instantiates prefab in the current active scene. " +
            "Use '" + Tool_Assets.AssetsFindToolId + "' tool to find prefab assets in the project.")]
        public GameObjectRef Instantiate
        (
            [Description("Prefab asset path.")]
            string prefabAssetPath,
            [Description("GameObject path in the current active scene.")]
            string gameObjectPath,
            [Description("Transform position of the GameObject.")]
            Vector3? position = default,
            [Description("Transform rotation of the GameObject. Euler angles in degrees.")]
            Vector3? rotation = default,
            [Description("Transform scale of the GameObject.")]
            Vector3? scale = default,
            [Description("World or Local space of transform.")]
            bool isLocalSpace = false
        )
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
                if (prefab == null)
                    throw new ArgumentException(Error.NotFoundPrefabAtPath(prefabAssetPath), nameof(prefabAssetPath));

                var parentGo = default(GameObject);
                if (StringUtils.Path_ParseParent(gameObjectPath, out var parentPath, out var name))
                {
                    parentGo = GameObjectUtils.FindByPath(parentPath);
                    if (parentGo == null)
                        throw new ArgumentException(Tool_GameObject.Error.NotFoundGameObjectAtPath(parentPath!), nameof(gameObjectPath));
                }

                position ??= Vector3.zero;
                rotation ??= Vector3.zero;
                scale ??= Vector3.one;

                var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (go == null)
                    throw new Exception($"[Error] Failed to instantiate prefab from path '{prefabAssetPath}'.");

                go.name = name ?? prefab.name;
                if (parentGo != null)
                    go.transform.SetParent(parentGo.transform, false);
                go.SetTransform(position, rotation, scale, isLocalSpace);

                EditorUtility.SetDirty(go);
                EditorUtils.RepaintAllEditorWindows();

                return new GameObjectRef(go);
            });
        }
    }
}
