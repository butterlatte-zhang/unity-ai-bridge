
#nullable enable
using UnityAiBridge.Data;
using UnityAiBridge.Utils;
using UnityEngine;

namespace UnityAiBridge.Extensions
{
    public static class ExtensionsRuntimeGameObjectRef
    {
        public static GameObject? FindGameObject(this GameObjectRef? objectRef)
            => FindGameObject(objectRef, out _);

        public static GameObject? FindGameObject(this GameObjectRef? objectRef, out string? error)
        {
            if (objectRef == null)
            {
                error = null;
                return null;
            }

            var go = GameObjectUtils.FindBy(objectRef, out error);
            if (go == null)
                go = ExtensionsRuntimeAssetObjectRef.FindAssetObject(objectRef) as GameObject;

            if (go != null)
                error = null;

            return go;
        }
        public static GameObjectRef? ToGameObjectRef(this GameObject? obj)
        {
            return new GameObjectRef(obj);
        }
    }
}
