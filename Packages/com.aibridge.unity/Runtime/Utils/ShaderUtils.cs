
#nullable enable
#if !UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityAiBridge.Utils
{
    public static partial class ShaderUtils
    {
        public static IEnumerable<Shader> GetAllShaders()
            => Enumerable.Empty<Shader>();
    }
}
#endif
