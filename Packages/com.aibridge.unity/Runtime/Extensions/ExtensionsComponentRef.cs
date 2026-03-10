
#nullable enable
using UnityAiBridge.Serialization;
using UnityAiBridge.Utils;
using UnityAiBridge.Data;

namespace UnityAiBridge.Extensions
{
    public static class ExtensionsComponentRef
    {
        public static bool Matches(this ComponentRef componentRef, UnityEngine.Component component, int? index = null)
        {
            if (componentRef.InstanceID != 0)
            {
                return componentRef.InstanceID == (component?.GetInstanceID() ?? 0);
            }
            if (componentRef.Index >= 0 && index != null)
            {
                return componentRef.Index == index.Value;
            }
            if (!StringUtils.IsNullOrEmpty(componentRef.TypeName))
            {
                var type = component?.GetType() ?? typeof(UnityEngine.Component);
                return type.IsMatch(componentRef.TypeName);
            }
            if (componentRef.InstanceID == 0 && component == null)
            {
                return true; // Matches null component
            }
            return false;
        }
    }
}
