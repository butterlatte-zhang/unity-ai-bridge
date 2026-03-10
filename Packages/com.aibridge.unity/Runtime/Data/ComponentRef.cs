
#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using UnityAiBridge.Serialization;
using UnityAiBridge.Utils;

namespace UnityAiBridge.Data
{
    [Description("Component reference. " +
        "Used to find a Component at GameObject.")]
    public class ComponentRef : ObjectRef
    {
        public static partial class ComponentRefProperty
        {
            public const string Index = "index";
            public const string TypeName = "typeName";

            public static IEnumerable<string> All => ObjectRefProperty.All.Concat(new[]
            {
                Index,
                TypeName
            });
        }
        [JsonInclude, JsonPropertyName(ComponentRefProperty.Index)]
        [Description("Component 'index' attached to a gameObject. The first index is '0' and that is usually Transform or RectTransform. Priority: 2. Default value is -1.")]
        public int Index { get; set; } = -1;

        [JsonInclude, JsonPropertyName(ComponentRefProperty.TypeName)]
        [Description("Component type full name. Sample 'UnityEngine.Transform'. If the gameObject has two components of the same type, the output component is unpredictable. Priority: 3. Default value is null.")]
        public string? TypeName { get; set; } = null;

        public override bool IsValid(out string? error)
        {
            if (InstanceID != 0)
            {
                error = null;
                return true;
            }
            if (Index >= 0)
            {
                error = null;
                return true;
            }
            if (!string.IsNullOrEmpty(TypeName))
            {
                error = null;
                return true;
            }
            error = $"Invalid ComponentRef: '{ObjectRefProperty.InstanceID}' is 0, '{ComponentRefProperty.Index}' is less than 0, and '{ComponentRefProperty.TypeName}' is null or empty.";
            return false;
        }

        public ComponentRef() { }
        public ComponentRef(int instanceID)
        {
            this.InstanceID = instanceID;
        }
        public ComponentRef(UnityEngine.Component? component) : base(component)
        {
            if (component == null)
                return;

            var go = component.gameObject;
            var components = go.GetComponents<UnityEngine.Component>();
            this.Index = System.Array.IndexOf(components, component);
            this.TypeName = component.GetType().GetTypeShortName();
        }

        /// <summary>
        /// Check if a Component matches this reference.
        /// </summary>
        public bool Matches(UnityEngine.Component component, int index = -1)
        {
            if (component == null)
                return false;

            if (InstanceID != 0)
                return component.GetInstanceID() == InstanceID;

            if (Index >= 0 && index >= 0)
                return Index == index;

            if (!string.IsNullOrEmpty(TypeName))
            {
                var componentType = component.GetType();
                return componentType.Name == TypeName
                    || componentType.FullName == TypeName
                    || componentType.GetTypeShortName() == TypeName;
            }

            return false;
        }

        public override string ToString()
        {
            if (InstanceID != 0)
                return $"Component {ObjectRefProperty.InstanceID}='{InstanceID}'";
            if (Index >= 0)
                return $"Component {ComponentRefProperty.Index}='{Index}'";
            if (!string.IsNullOrEmpty(TypeName))
                return $"Component {ComponentRefProperty.TypeName}='{TypeName}'";
            return "Component unknown";
        }
    }
}
