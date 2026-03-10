
#nullable enable
using UnityAiBridge.Serialization;
using UnityAiBridge.Data;

namespace UnityAiBridge.Extensions
{
    public static class ExtensionsSerializedMember
    {
        // Shared reflector for deserialization in extensions.
        // Using a static instance avoids referencing the Editor assembly.
        private static readonly BridgeReflector _reflector = new();

        public static bool TryGetInstanceID(this SerializedMember member, out int instanceID)
        {
            try
            {
                var objectRef = member.GetValue<ObjectRef>(_reflector);
                if (objectRef != null)
                {
                    instanceID = objectRef.InstanceID;
                    return true;
                }
            }
            catch
            {
                // Ignore exceptions, fallback to instanceID field
            }

            try
            {
                var fieldValue = member.GetField(ObjectRef.ObjectRefProperty.InstanceID);
                if (fieldValue != null)
                {
                    instanceID = fieldValue.GetValue<int>(_reflector);
                    return true;
                }
            }
            catch
            {
                // Ignore exceptions, fallback to instanceID field
            }

            instanceID = 0;
            return false;
        }
        public static bool TryGetGameObjectInstanceID(this SerializedMember member, out int instanceID)
        {
            try
            {
                var objectRef = member.GetValue<GameObjectRef>(_reflector);
                if (objectRef != null)
                {
                    instanceID = objectRef.InstanceID;
                    return true;
                }
            }
            catch
            {
                // Ignore exceptions, fallback to instanceID field
            }

            try
            {
                var fieldValue = member.GetField(ObjectRef.ObjectRefProperty.InstanceID);
                if (fieldValue != null)
                {
                    instanceID = fieldValue.GetValue<int>(_reflector);
                    return true;
                }
            }
            catch
            {
                // Ignore exceptions, fallback to instanceID field
            }

            instanceID = 0;
            return false;
        }
    }
}
