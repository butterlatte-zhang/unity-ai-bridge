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
        public const string ProfilerHotPathToolId = "profiler-hotpath";

        /// <summary>
        /// 热点函数排行结果
        /// </summary>
        public class HotPathResult
        {
            public int                      frameIndex;
            public double                   frameTotalMs;
            public string                   sortedBy = string.Empty;
            public List<ProfilerFlatEntry>? entries;
        }

        [BridgeTool
        (
            ProfilerHotPathToolId,
            Title = "Profiler / Hot Path"
        )]
        [Description("Returns the top N most expensive functions in a profiler frame, " +
            "sorted by self time or total time. " +
            "Useful for quickly identifying performance bottlenecks without reading the full hierarchy.")]
        public HotPathResult GetHotPath
        (
            [Description("Frame index to analyze. Use -1 for the latest available frame. Default: -1")]
            int frameIndex = -1,
            [Description("Thread index. 0 = Main Thread. Default: 0")]
            int threadIndex = 0,
            [Description("Number of top entries to return. Default: 20")]
            int topN = 20,
            [Description("Sort by 'selfTime' or 'totalTime'. Default: selfTime")]
            string sortBy = "selfTime",
            [Description("Maximum tree depth to scan. Deeper = more complete but slower. Default: 15")]
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

                // 构建完整树（不过滤 minTotalMs，确保不遗漏）
                int rootId = frameData.GetRootItemID();
                var childIds = new List<int>();
                frameData.GetItemChildren(rootId, childIds);

                var allNodes = new List<ProfilerFrameNode>();
                double frameTotalMs = 0;
                foreach (int childId in childIds)
                {
                    var node = BuildNodeTree(frameData, childId, 1, maxDepth, 0f);
                    allNodes.Add(node);
                    frameTotalMs += node.totalMs;
                }

                // 扁平化
                var flat = new List<ProfilerFlatEntry>();
                foreach (var node in allNodes)
                    FlattenTree(node, flat);

                // 按指定字段排序
                bool useSelfTime = !string.Equals(sortBy, "totalTime", StringComparison.OrdinalIgnoreCase);
                var sorted = useSelfTime
                    ? flat.OrderByDescending(e => e.selfMs).Take(topN).ToList()
                    : flat.OrderByDescending(e => e.totalMs).Take(topN).ToList();

                return new HotPathResult
                {
                    frameIndex   = frameIndex,
                    frameTotalMs = Math.Round(frameTotalMs, 3),
                    sortedBy     = useSelfTime ? "selfTime" : "totalTime",
                    entries      = sorted,
                };
            });
        }
    }
}
