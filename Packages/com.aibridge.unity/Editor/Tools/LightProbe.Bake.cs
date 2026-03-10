#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityEditor;
using UnityEngine;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_LightProbe
    {
        [BridgeTool
        (
            LightProbeBakeToolId,
            Title = "LightProbe / Bake"
        )]
        [Description("Trigger light baking in the current scene. " +
            "Bakes Light Probe data (and Lightmaps if static objects exist). " +
            "Make sure lights are set to Baked or Mixed mode before baking " +
            "(use '" + LightProbeConfigureLightsToolId + "' to configure). " +
            "Use '" + LightProbeAnalyzeToolId + "' and console-get-logs to verify results after baking completes.")]
        public BakeResult Bake
        (
            [Description("If true, bake asynchronously (non-blocking). If false, bake synchronously (blocks editor until done).")]
            bool async = true,
            [Description("Enable Baked Global Illumination in Lighting Settings.")]
            bool enableBakedGI = true,
            [Description("Enable Realtime Global Illumination in Lighting Settings (Enlighten). Usually not needed with Baked GI.")]
            bool enableRealtimeGI = false
        )
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                // 检查场景中是否有 LightProbeGroup
                var probeGroups = UnityEngine.Object.FindObjectsByType<LightProbeGroup>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

                // 检查是否有 Baked/Mixed 灯光
                var allLights = UnityEngine.Object.FindObjectsByType<Light>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                int bakedCount = 0;
                int mixedCount = 0;
                int realtimeCount = 0;
                foreach (var light in allLights)
                {
                    switch (light.lightmapBakeType)
                    {
                        case LightmapBakeType.Baked: bakedCount++; break;
                        case LightmapBakeType.Mixed: mixedCount++; break;
                        case LightmapBakeType.Realtime: realtimeCount++; break;
                    }
                }

                // 检查场景中是否有 ContributeGI 的静态几何体
                var allRenderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                int contributeGICount = 0;
                foreach (var renderer in allRenderers)
                {
                    var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                    if ((flags & StaticEditorFlags.ContributeGI) != 0)
                        contributeGICount++;
                }

                var warnings = new List<string>();
                if (probeGroups.Length == 0)
                    warnings.Add("[Warning] No LightProbeGroup found in scene. Bake will not produce probe data.");
                if (bakedCount == 0 && mixedCount == 0)
                    warnings.Add("[Warning] No Baked or Mixed lights found. Light Probes will not capture any baked lighting data.");
                if (contributeGICount == 0)
                    warnings.Add("[Warning] No MeshRenderers with 'Contribute GI' static flag found. " +
                        "Light Probes need static geometry for indirect light bouncing and occlusion. " +
                        "Without it, bake completes instantly with minimal/no probe data. " +
                        "Mark key scene objects (terrain, buildings, ground) as Static with ContributeGI enabled.");

                // 配置 Lighting Settings
                Lightmapping.bakedGI = enableBakedGI;
                Lightmapping.realtimeGI = enableRealtimeGI;

                // 取消正在进行的烘焙
                if (Lightmapping.isRunning)
                {
                    Lightmapping.Cancel();
                    warnings.Add("[Info] Cancelled previous bake in progress.");
                }

                // 启动烘焙
                bool started;
                if (async)
                {
                    started = Lightmapping.BakeAsync();
                }
                else
                {
                    started = Lightmapping.Bake();
                }

                var messages = new List<string>();
                if (started)
                {
                    messages.Add(async
                        ? "[Success] Async bake started. Use console-get-logs to monitor progress."
                        : "[Success] Synchronous bake completed.");
                }
                else
                {
                    messages.Add("[Error] Failed to start bake. Check Lighting Settings and Console for details.");
                }

                messages.Add($"Lights: {bakedCount} Baked, {mixedCount} Mixed, {realtimeCount} Realtime.");
                messages.Add($"LightProbeGroups: {probeGroups.Length}.");
                messages.Add($"Static ContributeGI MeshRenderers: {contributeGICount}.");
                messages.Add($"Settings: BakedGI={enableBakedGI}, RealtimeGI={enableRealtimeGI}.");

                return new BakeResult
                {
                    Started = started,
                    IsAsync = async,
                    BakedLightCount = bakedCount,
                    MixedLightCount = mixedCount,
                    RealtimeLightCount = realtimeCount,
                    ProbeGroupCount = probeGroups.Length,
                    ContributeGICount = contributeGICount,
                    Messages = messages,
                    Warnings = warnings.Count > 0 ? warnings : null
                };
            });
        }

        #region Bake Response Types

        public class BakeResult
        {
            [Description("Whether the bake was successfully started (async) or completed (sync).")]
            public bool Started { get; set; }

            [Description("Whether the bake is running asynchronously.")]
            public bool IsAsync { get; set; }

            [Description("Number of Baked mode lights in the scene.")]
            public int BakedLightCount { get; set; }

            [Description("Number of Mixed mode lights in the scene.")]
            public int MixedLightCount { get; set; }

            [Description("Number of Realtime mode lights in the scene.")]
            public int RealtimeLightCount { get; set; }

            [Description("Number of LightProbeGroups in the scene.")]
            public int ProbeGroupCount { get; set; }

            [Description("Number of MeshRenderers with ContributeGI static flag. " +
                "If 0, bake will produce minimal/no probe data because there is no geometry for indirect light bouncing.")]
            public int ContributeGICount { get; set; }

            [Description("Status messages.")]
            public List<string>? Messages { get; set; }

            [Description("Warnings about potential issues (missing probes, no baked lights, etc).")]
            public List<string>? Warnings { get; set; }
        }

        #endregion
    }
}
