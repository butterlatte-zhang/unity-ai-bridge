#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityEngine;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_LightProbe
    {
        [BridgeTool
        (
            LightProbeAnalyzeToolId,
            Title = "LightProbe / Analyze Scene"
        )]
        [Description("Analyze the current scene's local lights (Point, Spot, Area) and return their influence bounds, " +
            "spatial density, and recommended probe placement parameters. " +
            "Directional lights are skipped as they have no localized influence. " +
            "Use this before '" + LightProbeGenerateToolId + "' to determine optimal spacing.")]
        public AnalyzeResult AnalyzeScene
        (
            [Description("Grid cell size for spatial density analysis (in world units).")]
            float cellSize = 5f
        )
        {
            if (cellSize <= 0f)
                throw new ArgumentException("cellSize must be greater than 0.", nameof(cellSize));

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
                    return new AnalyzeResult
                    {
                        TotalLights = allLights.Length,
                        LocalLights = 0,
                        SkippedDirectional = skippedDirectional,
                        SceneBounds = null,
                        DensityCells = Array.Empty<DensityCell>(),
                        LightDetails = Array.Empty<LightInfo>(),
                        RecommendedSpacingXZ = cellSize,
                        RecommendedSpacingY = cellSize
                    };
                }

                // 基于灯光位置 + range 计算影响区域 bounds
                var firstLight = localLights[0];
                var firstPos = firstLight.transform.position;
                float firstRange = firstLight.range;
                var sceneBounds = new Bounds(firstPos, Vector3.one * firstRange * 2f);

                var lightDetails = new List<LightInfo>();
                foreach (var light in localLights)
                {
                    var pos = light.transform.position;
                    float range = light.range;
                    var lightBounds = new Bounds(pos, Vector3.one * range * 2f);
                    sceneBounds.Encapsulate(lightBounds);

                    var color = light.color;
                    lightDetails.Add(new LightInfo
                    {
                        Name = light.gameObject.name,
                        Type = light.type.ToString(),
                        Mode = light.lightmapBakeType.ToString(),
                        ShadowType = light.shadows.ToString(),
                        Enabled = light.enabled,
                        IsStatic = light.gameObject.isStatic,
                        X = (float)Math.Round(pos.x, 2),
                        Y = (float)Math.Round(pos.y, 2),
                        Z = (float)Math.Round(pos.z, 2),
                        Range = (float)Math.Round(range, 2),
                        Intensity = (float)Math.Round(light.intensity, 2),
                        Color = $"({color.r:F2}, {color.g:F2}, {color.b:F2})"
                    });
                }

                // 空间密度网格分析
                var min = sceneBounds.min;
                var max = sceneBounds.max;
                int cellsX = Mathf.Max(Mathf.CeilToInt((max.x - min.x) / cellSize), 1);
                int cellsZ = Mathf.Max(Mathf.CeilToInt((max.z - min.z) / cellSize), 1);

                var grid = new DensityCellData[cellsX, cellsZ];
                for (int x = 0; x < cellsX; x++)
                for (int z = 0; z < cellsZ; z++)
                {
                    grid[x, z] = new DensityCellData
                    {
                        MinY = float.MaxValue,
                        MaxY = float.MinValue
                    };
                }

                // 每个灯光按其 range 影响的 cell
                foreach (var light in localLights)
                {
                    var pos = light.transform.position;
                    float range = light.range;
                    float lMinX = pos.x - range;
                    float lMaxX = pos.x + range;
                    float lMinZ = pos.z - range;
                    float lMaxZ = pos.z + range;
                    float lMinY = pos.y - range;
                    float lMaxY = pos.y + range;

                    int x0 = Mathf.Clamp(Mathf.FloorToInt((lMinX - min.x) / cellSize), 0, cellsX - 1);
                    int x1 = Mathf.Clamp(Mathf.FloorToInt((lMaxX - min.x) / cellSize), 0, cellsX - 1);
                    int z0 = Mathf.Clamp(Mathf.FloorToInt((lMinZ - min.z) / cellSize), 0, cellsZ - 1);
                    int z1 = Mathf.Clamp(Mathf.FloorToInt((lMaxZ - min.z) / cellSize), 0, cellsZ - 1);

                    for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        grid[x, z].Count++;
                        grid[x, z].MinY = Mathf.Min(grid[x, z].MinY, lMinY);
                        grid[x, z].MaxY = Mathf.Max(grid[x, z].MaxY, lMaxY);
                    }
                }

                // 收集非空 cell
                var cells = new List<DensityCell>();
                for (int x = 0; x < cellsX; x++)
                for (int z = 0; z < cellsZ; z++)
                {
                    if (grid[x, z].Count <= 0)
                        continue;

                    cells.Add(new DensityCell
                    {
                        X = min.x + (x + 0.5f) * cellSize,
                        Z = min.z + (z + 0.5f) * cellSize,
                        LightCount = grid[x, z].Count,
                        MinY = grid[x, z].MinY,
                        MaxY = grid[x, z].MaxY
                    });
                }

                // 推荐间距
                float recommendedXZ = cellSize;
                float recommendedY = cellSize;
                if (cells.Count > 0)
                {
                    var sorted = cells.OrderByDescending(c => c.LightCount).ToList();
                    float medianDensity = sorted[sorted.Count / 2].LightCount;
                    recommendedXZ = Mathf.Clamp(cellSize / Mathf.Max(1f, Mathf.Sqrt(medianDensity)), 1f, cellSize);

                    var heights = cells.Select(c => c.MaxY - c.MinY).OrderBy(h => h).ToList();
                    float medianHeight = heights[heights.Count / 2];
                    recommendedY = Mathf.Clamp(medianHeight / 3f, 1f, cellSize);
                }

                return new AnalyzeResult
                {
                    TotalLights = allLights.Length,
                    LocalLights = localLights.Count,
                    SkippedDirectional = skippedDirectional,
                    SceneBounds = new BoundsData
                    {
                        MinX = (float)Math.Round(sceneBounds.min.x, 2),
                        MinY = (float)Math.Round(sceneBounds.min.y, 2),
                        MinZ = (float)Math.Round(sceneBounds.min.z, 2),
                        MaxX = (float)Math.Round(sceneBounds.max.x, 2),
                        MaxY = (float)Math.Round(sceneBounds.max.y, 2),
                        MaxZ = (float)Math.Round(sceneBounds.max.z, 2),
                        CenterX = (float)Math.Round(sceneBounds.center.x, 2),
                        CenterY = (float)Math.Round(sceneBounds.center.y, 2),
                        CenterZ = (float)Math.Round(sceneBounds.center.z, 2),
                        SizeX = (float)Math.Round(sceneBounds.size.x, 2),
                        SizeY = (float)Math.Round(sceneBounds.size.y, 2),
                        SizeZ = (float)Math.Round(sceneBounds.size.z, 2)
                    },
                    DensityCells = cells.ToArray(),
                    LightDetails = lightDetails.ToArray(),
                    RecommendedSpacingXZ = (float)Math.Round(recommendedXZ, 2),
                    RecommendedSpacingY = (float)Math.Round(recommendedY, 2)
                };
            });
        }

        private struct DensityCellData
        {
            public int Count;
            public float MinY;
            public float MaxY;
        }

        #region Analyze Response Types

        public class AnalyzeResult
        {
            [Description("Total number of Light components in the scene (including Directional).")]
            public int TotalLights { get; set; }

            [Description("Number of local lights (Point, Spot, Area) used for analysis.")]
            public int LocalLights { get; set; }

            [Description("Number of Directional lights skipped.")]
            public int SkippedDirectional { get; set; }

            [Description("Overall bounding box of all local light influence ranges. Null if no local lights found.")]
            public BoundsData? SceneBounds { get; set; }

            [Description("Spatial density grid cells based on light influence overlap (only non-empty cells).")]
            public DensityCell[] DensityCells { get; set; } = Array.Empty<DensityCell>();

            [Description("Details of each local light in the scene.")]
            public LightInfo[] LightDetails { get; set; } = Array.Empty<LightInfo>();

            [Description("Recommended horizontal probe spacing based on light density.")]
            public float RecommendedSpacingXZ { get; set; }

            [Description("Recommended vertical probe spacing based on light height distribution.")]
            public float RecommendedSpacingY { get; set; }
        }

        public class BoundsData
        {
            public float MinX { get; set; }
            public float MinY { get; set; }
            public float MinZ { get; set; }
            public float MaxX { get; set; }
            public float MaxY { get; set; }
            public float MaxZ { get; set; }
            public float CenterX { get; set; }
            public float CenterY { get; set; }
            public float CenterZ { get; set; }
            public float SizeX { get; set; }
            public float SizeY { get; set; }
            public float SizeZ { get; set; }
        }

        public class LightInfo
        {
            [Description("GameObject name of the light.")]
            public string Name { get; set; } = string.Empty;

            [Description("Light type (Point, Spot, Area, Rectangle, Disc).")]
            public string Type { get; set; } = string.Empty;

            [Description("Current bake mode (Realtime, Baked, Mixed). Key for AI to decide configure-lights changes.")]
            public string Mode { get; set; } = string.Empty;

            [Description("Shadow type (None, Hard, Soft).")]
            public string ShadowType { get; set; } = string.Empty;

            [Description("Whether the Light component is enabled.")]
            public bool Enabled { get; set; }

            [Description("Whether the GameObject is marked as Static.")]
            public bool IsStatic { get; set; }

            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }

            [Description("Light range in world units.")]
            public float Range { get; set; }

            [Description("Light intensity.")]
            public float Intensity { get; set; }

            [Description("Light color as (r, g, b) in 0-1 range.")]
            public string Color { get; set; } = string.Empty;
        }

        public class DensityCell
        {
            [Description("World X position of the cell center.")]
            public float X { get; set; }

            [Description("World Z position of the cell center.")]
            public float Z { get; set; }

            [Description("Number of local lights whose range overlaps this cell.")]
            public int LightCount { get; set; }

            [Description("Minimum Y of light influence in this cell.")]
            public float MinY { get; set; }

            [Description("Maximum Y of light influence in this cell.")]
            public float MaxY { get; set; }
        }

        #endregion
    }
}
