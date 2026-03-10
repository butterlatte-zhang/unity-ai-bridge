#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityAiBridge.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_LightProbe
    {
        [BridgeTool
        (
            LightProbeConfigureLightsToolId,
            Title = "LightProbe / Configure Lights"
        )]
        [Description("Batch-configure Light bake modes in the scene. " +
            "Each entry specifies a name pattern (supports * wildcard) and a target mode (Realtime, Baked, Mixed). " +
            "Use '" + LightProbeAnalyzeToolId + "' first to inspect all lights and their current modes, " +
            "then decide which lights to change based on the analysis.")]
        public ConfigureLightsResult ConfigureLights
        (
            [Description("Array of light configuration entries. Each has 'namePattern' (supports * wildcard, e.g. 'StreetLight*') and 'mode' ('Realtime', 'Baked', or 'Mixed').")]
            LightConfigEntry[] lights
        )
        {
            if (lights == null || lights.Length == 0)
                throw new ArgumentException("At least one light configuration entry is required.", nameof(lights));

            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                var allLights = UnityEngine.Object.FindObjectsByType<Light>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

                var changed = new List<string>();
                var skipped = new List<string>();
                var errors = new List<string>();

                foreach (var entry in lights)
                {
                    if (string.IsNullOrEmpty(entry.namePattern))
                    {
                        errors.Add("[Error] Empty namePattern in entry.");
                        continue;
                    }

                    if (!TryParseBakeMode(entry.mode, out var bakeType))
                    {
                        errors.Add($"[Error] Invalid mode '{entry.mode}' for pattern '{entry.namePattern}'. Use 'Realtime', 'Baked', or 'Mixed'.");
                        continue;
                    }

                    // 将通配符模式转为正则
                    var regex = WildcardToRegex(entry.namePattern);
                    bool anyMatch = false;

                    foreach (var light in allLights)
                    {
                        if (!regex.IsMatch(light.gameObject.name))
                            continue;

                        anyMatch = true;

                        if (light.lightmapBakeType == bakeType)
                        {
                            skipped.Add($"{light.gameObject.name}: already {entry.mode}");
                            continue;
                        }

                        var oldMode = light.lightmapBakeType.ToString();
                        light.lightmapBakeType = bakeType;
                        EditorUtility.SetDirty(light);
                        changed.Add($"{light.gameObject.name}: {oldMode} → {entry.mode}");
                    }

                    if (!anyMatch)
                        errors.Add($"[Warning] No lights matched pattern '{entry.namePattern}'.");
                }

                if (changed.Count > 0)
                    EditorUtils.RepaintAllEditorWindows();

                return new ConfigureLightsResult
                {
                    ChangedCount = changed.Count,
                    SkippedCount = skipped.Count,
                    Changed = changed.Count > 0 ? changed : null,
                    Skipped = skipped.Count > 0 ? skipped : null,
                    Errors = errors.Count > 0 ? errors : null
                };
            });
        }

        private static bool TryParseBakeMode(string? mode, out LightmapBakeType result)
        {
            result = LightmapBakeType.Realtime;
            if (string.IsNullOrEmpty(mode))
                return false;

            switch (mode.ToLowerInvariant())
            {
                case "realtime":
                    result = LightmapBakeType.Realtime;
                    return true;
                case "baked":
                    result = LightmapBakeType.Baked;
                    return true;
                case "mixed":
                    result = LightmapBakeType.Mixed;
                    return true;
                default:
                    return false;
            }
        }

        private static Regex WildcardToRegex(string pattern)
        {
            var escaped = Regex.Escape(pattern).Replace("\\*", ".*");
            return new Regex($"^{escaped}$", RegexOptions.IgnoreCase);
        }

        #region ConfigureLights Types

        public class LightConfigEntry
        {
            [Description("Light name pattern. Supports * wildcard (e.g. 'StreetLight*', '*_Spot', '*').")]
            public string namePattern { get; set; } = string.Empty;

            [Description("Target bake mode: 'Realtime', 'Baked', or 'Mixed'.")]
            public string mode { get; set; } = string.Empty;
        }

        public class ConfigureLightsResult
        {
            [Description("Number of lights whose mode was changed.")]
            public int ChangedCount { get; set; }

            [Description("Number of lights already in the target mode (no change needed).")]
            public int SkippedCount { get; set; }

            [Description("List of changed lights with old → new mode.")]
            public List<string>? Changed { get; set; }

            [Description("List of skipped lights (already correct mode).")]
            public List<string>? Skipped { get; set; }

            [Description("Errors and warnings encountered.")]
            public List<string>? Errors { get; set; }
        }

        #endregion
    }
}
