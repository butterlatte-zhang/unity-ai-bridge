
#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace UnityAiBridge.Data
{
    [System.Serializable]
    [Description("Reference to UnityEngine.Object instance. " +
        "It could be GameObject, Component, Asset, etc. " +
        "Anything extended from UnityEngine.Object.")]
    public class ObjectRef
    {
        public static partial class ObjectRefProperty
        {
            public const string InstanceID = "instanceID";

            public static IEnumerable<string> All => new[] { InstanceID };
        }
        [JsonInclude, JsonPropertyName(ObjectRefProperty.InstanceID)]
        [Description("instanceID of the UnityEngine.Object. If this is '0', then it will be used as 'null'.")]
        public virtual int InstanceID { get; set; } = 0;

        public ObjectRef() : this(instanceID: 0) { }
        public ObjectRef(int instanceID) => InstanceID = instanceID;
        public ObjectRef(UnityEngine.Object? obj)
        {
            InstanceID = obj?.GetInstanceID() ?? 0;
        }

        public virtual bool IsValid() => IsValid(out var error);
        public virtual bool IsValid(out string? error)
        {
            if (InstanceID != 0)
            {
                error = null;
                return true;
            }

            error = $"'{nameof(InstanceID)}' is '0', this is invalid value for any UnityEngine.Object.";
            return false;
        }

        public override string ToString()
        {
            return $"ObjectRef {ObjectRefProperty.InstanceID}='{InstanceID}'";
        }
    }
}
