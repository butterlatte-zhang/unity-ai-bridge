
#nullable enable
using UnityAiBridge.Data;

namespace UnityAiBridge.Extensions
{
    public static class ExtensionsRuntimeObjectRef
    {
        public static UnityEngine.Object? FindObject(this ObjectRef? objectRef)
        {
            if (objectRef == null)
                return null;

#if UNITY_EDITOR
            if (objectRef.InstanceID != 0)
            {
#if UNITY_6000_3_OR_NEWER
                return UnityEditor.EditorUtility.EntityIdToObject((UnityEngine.EntityId)objectRef.InstanceID);
#else
                return UnityEditor.EditorUtility.InstanceIDToObject(objectRef.InstanceID);
#endif
            }
#endif
            return null;
        }
        public static ObjectRef? ToObjectRef(this UnityEngine.Object? obj)
        {
            return new ObjectRef(obj);
        }
    }
}
