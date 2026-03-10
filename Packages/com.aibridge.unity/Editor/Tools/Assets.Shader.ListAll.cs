
#nullable enable
using System.ComponentModel;
using System.Linq;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityEditor;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Assets_Shader
    {
        public const string AssetsShaderListAllToolId = "assets-shader-list-all";
        [BridgeTool
        (
            AssetsShaderListAllToolId,
            Title = "Assets / List Shaders"
        )]
        [Description("List all available shaders in the project assets and packages. " +
            "Returns their names. " +
            "Use this to find a shader name for '" + Tool_Assets.AssetsMaterialCreateToolId + "' tool.")]
        public string[] ListAll() => UnityAiBridge.Utils.MainThread.Instance.Run(() =>
        {
            return ShaderUtils.GetAllShaders()
                .Where(shader => shader != null)
                .Select(shader => shader.name)
                .OrderBy(name => name)
                .ToArray();
        });
    }
}