#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityEditor.Profiling;
using UnityEditorInternal;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Profiler
    {
        public const string ProfilerFrameHierarchyToolId = "profiler-frame-hierarchy";

        /// <summary>
        /// 帧层级数据结果
        /// </summary>
        public class FrameHierarchyResult
        {
            public int                       frameIndex;
            public int                       threadIndex;
            public string                    threadName = string.Empty;
            public double                    frameTotalMs;
            public List<ProfilerFrameNode>?  rootChildren;
        }

        [BridgeTool
        (
            ProfilerFrameHierarchyToolId,
            Title = "Profiler / Frame Hierarchy"
        )]
        [Description("Retrieves the full profiler call hierarchy tree for a specific frame. " +
            "Returns function names, total time, self time, call counts, and GC allocations " +
            "in a tree structure — equivalent to the Unity Profiler Hierarchy view. " +
            "Use this to analyze detailed per-function performance.")]
        public FrameHierarchyResult GetFrameHierarchy
        (
            [Description("Frame index to analyze. Use -1 for the latest available frame. Default: -1")]
            int frameIndex = -1,
            [Description("Thread index. 0 = Main Thread, 1 = Render Thread, etc. Default: 0")]
            int threadIndex = 0,
            [Description("Maximum depth of the call tree to return. Use smaller values for overview, " +
                "larger values for detailed analysis. Default: 5")]
            int maxDepth = 5,
            [Description("Minimum total time (ms) to include a node. Filters out insignificant calls. Default: 0.1")]
            float minTotalMs = 0.1f
        )
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                if (frameIndex < 0)
                    frameIndex = GetLastFrameIndex();

                using var frameData = GetValidFrameData(frameIndex, threadIndex);

                int rootId = frameData.GetRootItemID();
                var childIds = new List<int>();
                frameData.GetItemChildren(rootId, childIds);

                var rootChildren = new List<ProfilerFrameNode>();
                foreach (int childId in childIds)
                {
                    float childTotal = frameData.GetItemColumnDataAsFloat(
                        childId, HierarchyFrameDataView.columnTotalTime);
                    if (childTotal >= minTotalMs)
                    {
                        rootChildren.Add(BuildNodeTree(frameData, childId, 1, maxDepth, minTotalMs));
                    }
                }

                // 获取线程名
                string threadName = "Unknown";
                try
                {
                    using var iter = new ProfilerFrameDataIterator();
                    iter.SetRoot(frameIndex, threadIndex);
                    threadName = iter.GetThreadName();
                }
                catch (Exception e) { UnityEngine.Debug.LogWarning($"[Profiler] Failed to get thread name: {e.Message}"); }

                // 计算帧总耗时
                double frameTotalMs = 0;
                foreach (var child in rootChildren)
                    frameTotalMs += child.totalMs;

                return new FrameHierarchyResult
                {
                    frameIndex   = frameIndex,
                    threadIndex  = threadIndex,
                    threadName   = threadName,
                    frameTotalMs = Math.Round(frameTotalMs, 3),
                    rootChildren = rootChildren,
                };
            });
        }
    }
}
