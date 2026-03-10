#nullable enable

using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using UnityAiBridge.Serialization;
using UnityAiBridge.Utils;

namespace UnityAiBridge.Data
{
    /// <summary>
    /// Serialized method data for bridge tool method discovery.
    /// </summary>
    public class MethodData : MethodRef
    {
        [JsonInclude]
        [JsonPropertyName("isPublic")]
        [Description("Indicates if the method is public.")]
        public bool IsPublic { get; set; }

        [JsonInclude]
        [JsonPropertyName("isStatic")]
        [Description("Indicates if the method is static.")]
        public bool IsStatic { get; set; }

        [JsonInclude]
        [JsonPropertyName("returnType")]
        [Description("Return type of the method. It may be null if the method has no return type.")]
        public string? ReturnType { get; set; }

        public MethodData() { }

        public MethodData(BridgeReflector reflector, MethodInfo methodInfo, bool justRef = false)
            : base(methodInfo)
        {
            IsStatic = methodInfo.IsStatic;
            IsPublic = methodInfo.IsPublic;
            ReturnType = methodInfo.ReturnType == typeof(void)
                ? null
                : methodInfo.ReturnType.GetTypeId();
        }
    }
}
