#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityAiBridge;
using UnityAiBridge.Utils;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Profiler
    {
        public const string ProfilerGcAllocToolId = "profiler-gc-alloc";

        /// <summary>
        /// GC 分配排行结果
        /// </summary>
        public class GcAllocResult
        {
            public int                      frameIndex;
            public long                     totalGcAllocBytes;
            public List<ProfilerFlatEntry>? entries;
        }

        [BridgeTool
        (
            ProfilerGcAllocToolId,
            Title = "Profiler / GC Allocations"
        )]
        [Description("Returns the top N functions with the highest GC (garbage collection) allocations " +
            "in a profiler frame, including their call paths. " +
            "GC allocations are a major source of frame hitches in Unity — use this to find and eliminate them.")]
        public GcAllocResult GetGcAlloc
        (
            [Description("Frame index to analyze. Use -1 for the latest available frame. Default: -1")]
            int frameIndex = -1,
            [Description("Thread index. 0 = Main Thread. Default: 0")]
            int threadIndex = 0,
            [Description("Number of top entries to return. Default: 20")]
            int topN = 20,
            [Description("Minimum GC allocation bytes to include. Filters out trivial allocations. Default: 0")]
            long minBytes = 0,
            [Description("Maximum tree depth to scan. Default: 15")]
            int maxDepth = 15
        )
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                if (frameIndex < 0)
                    frameIndex = GetLastFrameIndex();

                if (topN < 1) topN = 1;
                if (topN > 200) topN = 200;

                using var frameData = GetValidFrameData(frameIndex, threadIndex);

                // 构建完整树
                int rootId = frameData.GetRootItemID();
                var childIds = new List<int>();
                frameData.GetItemChildren(rootId, childIds);

                var allNodes = new List<ProfilerFrameNode>();
                foreach (int childId in childIds)
                    allNodes.Add(BuildNodeTree(frameData, childId, 1, maxDepth, 0f));

                // 扁平化
                var flat = new List<ProfilerFlatEntry>();
                foreach (var node in allNodes)
                    FlattenTree(node, flat);

                // 过滤并排序
                var sorted = flat
                    .Where(e => e.gcAllocBytes > minBytes)
                    .OrderByDescending(e => e.gcAllocBytes)
                    .Take(topN)
                    .ToList();

                // 计算总 GC 分配（只统计根节点，避免 inclusive 值重复计算）
                long totalGcAlloc = 0;
                foreach (var node in allNodes)
                    totalGcAlloc += node.gcAllocBytes;

                return new GcAllocResult
                {
                    frameIndex        = frameIndex,
                    totalGcAllocBytes = totalGcAlloc,
                    entries           = sorted,
                };
            });
        }
    }
}
