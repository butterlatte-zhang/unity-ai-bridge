#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
            LightProbeGenerateToolId,
            Title = "LightProbe / Generate Grid"
        )]
        [Description("Generate Light Probes in a 3D grid within local light influence ranges. " +
            "Supports density gradient: denser probes near light centers, sparser at edges. " +
            "Scans Point/Spot/Area lights, skips Directional lights. " +
            "Uses raycasting to find ground level and Physics.CheckSphere to avoid placing probes inside geometry. " +
            "Use '" + LightProbeAnalyzeToolId + "' first to get recommended spacing values.")]
        public GenerateResult GenerateGrid
        (
            [Description("Horizontal spacing between probes (in world units). With density gradient, this is the 'standard' spacing.")]
            float spacingXZ = 3f,
            [Description("Vertical spacing between probe layers (in world units).")]
            float spacingY = 2f,
            [Description("Number of vertical layers of probes above ground.")]
            int heightLevels = 3,
            [Description("Height offset from ground for the first probe layer.")]
            float groundOffset = 0.5f,
            [Description("Name of the LightProbeGroup GameObject to create.")]
            string groupName = "LightProbeGroup_Auto",
            [Description("Radius for CheckSphere to reject probes inside geometry. Set to 0 to disable.")]
            float insideCheckRadius = 0.3f,
            [Description("Enable density gradient: denser probes near light centers (d<0.4 range → half spacing), " +
                "standard spacing at mid-range (0.4-0.8), sparser at edges (>0.8 → double spacing). " +
                "When false, uses uniform spacingXZ everywhere.")]
            bool useDensityGradient = true
        )
        {
            if (spacingXZ <= 0f)
                throw new ArgumentException("spacingXZ must be greater than 0.", nameof(spacingXZ));
            if (spacingY <= 0f)
                throw new ArgumentException("spacingY must be greater than 0.", nameof(spacingY));
            if (heightLevels < 1)
                throw new ArgumentException("heightLevels must be at least 1.", nameof(heightLevels));

            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                // 收集所有非 Directional 的活跃灯光
                var allLights = UnityEngine.Object.FindObjectsByType<Light>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);

                var localLights = new List<Light>();
                int skippedDirectional = 0;
                foreach (var light in allLights)
                {
                    if (light.type == LightType.Directional)
                    {
                        skippedDirectional++;
                        continue;
                    }
                    localLights.Add(light);
                }

                if (localLights.Count == 0)
                {
                    return new GenerateResult
                    {
                        Success = false,
                        ProbeCount = 0,
                        GroupName = groupName,
                        Errors = new List<string>
                        {
                            $"[Error] No local lights (Point/Spot/Area) found in scene. " +
                            $"Found {skippedDirectional} Directional light(s) which are skipped."
                        }
                    };
                }

                // 基于灯光位置 + range 计算合并 bounds
                var firstPos = localLights[0].transform.position;
                float firstRange = localLights[0].range;
                var sceneBounds = new Bounds(firstPos, Vector3.one * firstRange * 2f);
                foreach (var light in localLights)
                {
                    var pos = light.transform.position;
                    sceneBounds.Encapsulate(new Bounds(pos, Vector3.one * light.range * 2f));
                }

                var min = sceneBounds.min;
                var max = sceneBounds.max;

                // 密度梯度：用半间距细网格迭代，按距离区域过滤
                float stepXZ = useDensityGradient ? spacingXZ * 0.5f : spacingXZ;

                // 安全上限检查
                int gridCountX = Mathf.CeilToInt((max.x - min.x) / stepXZ) + 1;
                int gridCountZ = Mathf.CeilToInt((max.z - min.z) / stepXZ) + 1;
                long estimatedPoints = (long)gridCountX * gridCountZ * heightLevels;
                const long MaxProbeEstimate = 500_000;
                if (estimatedPoints > MaxProbeEstimate)
                {
                    return new GenerateResult
                    {
                        Success = false,
                        ProbeCount = 0,
                        GroupName = groupName,
                        Errors = new List<string>
                        {
                            $"[Error] Estimated {estimatedPoints} probe positions exceeds safety limit of {MaxProbeEstimate}. " +
                            $"Increase spacingXZ (current: {spacingXZ}) or reduce heightLevels (current: {heightLevels})."
                        }
                    };
                }

                // 在 XZ 平面按 stepXZ 生成网格点，只保留在至少一个灯光 range 内的点
                var probePositions = new List<Vector3>();
                int rejectedInside = 0;
                int rejectedNoGround = 0;
                int rejectedOutOfRange = 0;
                int rejectedDensityFilter = 0;
                int denseDensityCount = 0;
                int standardDensityCount = 0;
                int sparseDensityCount = 0;
                float raycastOriginY = max.y + 10f;

                int ix = 0;
                for (float x = min.x; x <= max.x; x += stepXZ, ix++)
                {
                    int iz = 0;
                    for (float z = min.z; z <= max.z; z += stepXZ, iz++)
                    {
                        // 检查该 XZ 点到最近灯光的归一化距离
                        float minNormDist = float.MaxValue;
                        bool inRange = false;
                        foreach (var light in localLights)
                        {
                            var lp = light.transform.position;
                            float dx = x - lp.x;
                            float dz = z - lp.z;
                            float distSq = dx * dx + dz * dz;
                            float rangeSq = light.range * light.range;
                            if (distSq <= rangeSq)
                            {
                                inRange = true;
                                float normDist = Mathf.Sqrt(distSq) / light.range;
                                if (normDist < minNormDist)
                                    minNormDist = normDist;
                            }
                        }
                        if (!inRange)
                        {
                            rejectedOutOfRange++;
                            continue;
                        }

                        // 密度梯度过滤（仅在启用时生效）
                        if (useDensityGradient)
                        {
                            if (minNormDist < 0.4f)
                            {
                                // 密区：保留所有细网格点（半间距）
                                denseDensityCount++;
                            }
                            else if (minNormDist < 0.8f)
                            {
                                // 标准区：只保留对齐到 spacingXZ 网格的点（索引为偶数）
                                if (ix % 2 != 0 || iz % 2 != 0)
                                {
                                    rejectedDensityFilter++;
                                    continue;
                                }
                                standardDensityCount++;
                            }
                            else
                            {
                                // 疏区：只保留对齐到 2*spacingXZ 网格的点（索引为4的倍数）
                                if (ix % 4 != 0 || iz % 4 != 0)
                                {
                                    rejectedDensityFilter++;
                                    continue;
                                }
                                sparseDensityCount++;
                            }
                        }

                        // 向下发射 Raycast 获取地面高度
                        var rayOrigin = new Vector3(x, raycastOriginY, z);
                        if (!Physics.Raycast(rayOrigin, Vector3.down, out var hit, raycastOriginY - min.y + 20f))
                        {
                            rejectedNoGround++;
                            continue;
                        }

                        float groundY = hit.point.y;

                        // 从地面 + groundOffset 起，按 spacingY 向上生成 heightLevels 层
                        for (int level = 0; level < heightLevels; level++)
                        {
                            float probeY = groundY + groundOffset + level * spacingY;

                            if (probeY > max.y + spacingY)
                                break;

                            var probePos = new Vector3(x, probeY, z);

                            // 用 CheckSphere 剔除在 Collider 内部的点
                            if (insideCheckRadius > 0f && Physics.CheckSphere(probePos, insideCheckRadius, ~0, QueryTriggerInteraction.Ignore))
                            {
                                rejectedInside++;
                                continue;
                            }

                            probePositions.Add(probePos);
                        }
                    }
                }

                if (probePositions.Count == 0)
                {
                    return new GenerateResult
                    {
                        Success = false,
                        ProbeCount = 0,
                        GroupName = groupName,
                        RejectedInsideGeometry = rejectedInside,
                        RejectedNoGround = rejectedNoGround,
                        RejectedOutOfLightRange = rejectedOutOfRange,
                        Errors = new List<string> { "[Error] No valid probe positions found. Try adjusting spacing or groundOffset." }
                    };
                }

                // 清除同名的旧 LightProbeGroup
                var existing = GameObject.Find(groupName);
                if (existing != null)
                    UnityEngine.Object.DestroyImmediate(existing);

                // 创建 GameObject + LightProbeGroup 组件
                var go = new GameObject(groupName);
                var lpg = go.AddComponent<LightProbeGroup>();
                lpg.probePositions = probePositions.ToArray();

                EditorUtility.SetDirty(go);
                EditorUtils.RepaintAllEditorWindows();

                var messages = new List<string>
                {
                    $"[Success] Created LightProbeGroup '{groupName}' with {probePositions.Count} probes.",
                    $"Based on {localLights.Count} local lights (skipped {skippedDirectional} Directional).",
                    $"Grid: spacingXZ={spacingXZ}, spacingY={spacingY}, heightLevels={heightLevels}, groundOffset={groundOffset}.",
                    $"Rejected: {rejectedOutOfRange} out of light range, {rejectedInside} inside geometry, {rejectedNoGround} no ground hit."
                };

                if (useDensityGradient)
                {
                    messages.Add($"Density gradient: {denseDensityCount} dense (d<0.4, step={stepXZ:F1}), " +
                        $"{standardDensityCount} standard (0.4-0.8, step={spacingXZ:F1}), " +
                        $"{sparseDensityCount} sparse (>0.8, step={spacingXZ * 2f:F1}), " +
                        $"{rejectedDensityFilter} filtered by density.");
                }

                return new GenerateResult
                {
                    Success = true,
                    ProbeCount = probePositions.Count,
                    GroupName = groupName,
                    UsedDensityGradient = useDensityGradient,
                    RejectedInsideGeometry = rejectedInside,
                    RejectedNoGround = rejectedNoGround,
                    RejectedOutOfLightRange = rejectedOutOfRange,
                    RejectedDensityFilter = rejectedDensityFilter,
                    SceneBoundsMin = $"({min.x:F1}, {min.y:F1}, {min.z:F1})",
                    SceneBoundsMax = $"({max.x:F1}, {max.y:F1}, {max.z:F1})",
                    Messages = messages
                };
            });
        }

        #region GenerateGrid Response Types

        public class GenerateResult
        {
            [Description("Whether the generation was successful.")]
            public bool Success { get; set; }

            [Description("Number of probes placed.")]
            public int ProbeCount { get; set; }

            [Description("Name of the created LightProbeGroup GameObject.")]
            public string GroupName { get; set; } = string.Empty;

            [Description("Whether density gradient was used (denser near lights, sparser at edges).")]
            public bool UsedDensityGradient { get; set; }

            [Description("Number of probe positions rejected for being inside geometry.")]
            public int RejectedInsideGeometry { get; set; }

            [Description("Number of XZ grid points rejected for having no ground hit.")]
            public int RejectedNoGround { get; set; }

            [Description("Number of XZ grid points rejected for being outside all light ranges.")]
            public int RejectedOutOfLightRange { get; set; }

            [Description("Number of XZ grid points rejected by density gradient filter (mid/sparse zones).")]
            public int RejectedDensityFilter { get; set; }

            [Description("Light-based bounds minimum corner.")]
            public string? SceneBoundsMin { get; set; }

            [Description("Light-based bounds maximum corner.")]
            public string? SceneBoundsMax { get; set; }

            [Description("Success messages and generation details.")]
            public List<string>? Messages { get; set; }

            [Description("Error messages if generation failed.")]
            public List<string>? Errors { get; set; }
        }

        #endregion
    }
}
