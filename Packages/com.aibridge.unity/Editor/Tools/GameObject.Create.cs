
#nullable enable
using System;
using System.ComponentModel;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityAiBridge.Editor.Utils;
using UnityAiBridge.Data;
using UnityAiBridge.Extensions;
using UnityEditor;
using UnityEngine;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_GameObject
    {
        public const string GameObjectCreateToolId = "gameobject-create";
        [BridgeTool
        (
            GameObjectCreateToolId,
            Title = "GameObject / Create"
        )]
        [Description("Create a new GameObject in opened Prefab or in a Scene. " +
            "If needed - provide proper 'position', 'rotation' and 'scale' to reduce amount of operations.")]
        public GameObjectRef Create
        (
            [Description("Name of the new GameObject.")]
            string name,
            [Description("Parent GameObject reference. If not provided, the GameObject will be created at the root of the scene or prefab.")]
            GameObjectRef? parentGameObjectRef = null,
            [Description("Transform position of the GameObject.")]
            Vector3? position = null,
            [Description("Transform rotation of the GameObject. Euler angles in degrees.")]
            Vector3? rotation = null,
            [Description("Transform scale of the GameObject.")]
            Vector3? scale = null,
            [Description("World or Local space of transform.")]
            bool isLocalSpace = false,
            PrimitiveType? primitiveType = null
        )
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Name cannot be null or empty.", nameof(name));

            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                var parentGo = default(GameObject);
                if (parentGameObjectRef?.IsValid(out _) == true)
                {
                    parentGo = parentGameObjectRef.FindGameObject(out var error);
                    if (error != null)
                        throw new ArgumentException(error, nameof(parentGameObjectRef));
                }

                position ??= Vector3.zero;
                rotation ??= Vector3.zero;
                scale ??= Vector3.one;

                var go = primitiveType != null
                    ? GameObject.CreatePrimitive(primitiveType.Value)
                    : new GameObject(name);

                go.name = name;

                // Set parent if provided
                if (parentGo != null)
                    go.transform.SetParent(parentGo.transform, false);

                // Set the transform properties
                go.SetTransform(
                    position: position,
                    rotation: rotation,
                    scale: scale,
                    isLocalSpace: isLocalSpace);

                EditorUtility.SetDirty(go);
                EditorUtils.RepaintAllEditorWindows();

                return new GameObjectRef(go);
            });
        }
    }
}
