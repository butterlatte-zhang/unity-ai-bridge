#nullable enable
using UnityAiBridge;

namespace UnityAiBridge.Editor.Tools
{
    [BridgeToolType]
    public partial class Tool_LightProbe
    {
        public const string LightProbeAnalyzeToolId  = "lightprobe-analyze";
        public const string LightProbeGenerateToolId = "lightprobe-generate-grid";
        public const string LightProbeClearToolId            = "lightprobe-clear";
        public const string LightProbeConfigureLightsToolId = "lightprobe-configure-lights";
        public const string LightProbeBakeToolId            = "lightprobe-bake";
    }
}
