#nullable enable
using System;
using System.Collections.Generic;
using UnityAiBridge;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;

namespace UnityAiBridge.Editor.Tools
{
    [BridgeToolType]
    public partial class Tool_Profiler
    {
        public static class Error
        {
            public static string ProfilerNotEnabled()
                => "[Error] Unity Profiler is not enabled. Enable it via Window > Analysis > Profiler.";

            public static string ProfilerJustEnabled()
                => "Profiler has been enabled and data collection started. Please call this tool again in a moment.";

            public static string InvalidFrameIndex(int index, int lastFrame)
                => $"[Error] Invalid frame index: {index}. Last available frame: {lastFrame}.";

            public static string NoFrameData(int frameIndex, int threadIndex)
                => $"[Error] No valid profiler frame data for frame {frameIndex}, thread {threadIndex}.";
        }

        #region Data Models

        /// <summary>
        /// Profiler 调用层级节点（带子节点树）
        /// </summary>
        public class ProfilerFrameNode
        {
            public string name      = string.Empty;
            public double totalMs;
            public double selfMs;
            public int    calls;
            public long   gcAllocBytes;
            public List<ProfilerFrameNode>? children;
        }

        /// <summary>
        /// Profiler 扁平条目（用于排行榜）
        /// </summary>
        public class ProfilerFlatEntry
        {
            public string name      = string.Empty;
            public string callPath  = string.Empty;
            public double totalMs;
            public double selfMs;
            public int    calls;
            public long   gcAllocBytes;
        }

        /// <summary>
        /// 性能快照结果
        /// </summary>
        public class ProfilerSnapshotResult
        {
            public int    frameIndex;
            public float  fps;
            public float  frameTimeMs;
            public long   totalAllocatedMemory;
            public long   totalReservedMemory;
            public long   monoHeapSize;
            public long   monoUsedSize;
            public long   drawCalls;
            public long   setPassCalls;
            public long   triangles;
            public long   vertices;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// 递归遍历 HierarchyFrameDataView 构建调用树
        /// </summary>
        protected static ProfilerFrameNode BuildNodeTree(
            HierarchyFrameDataView frameData,
            int                    itemId,
            int                    currentDepth,
            int                    maxDepth,
            float                  minTotalMs)
        {
            var node = new ProfilerFrameNode
            {
                name         = frameData.GetItemName(itemId),
                totalMs      = Math.Round(frameData.GetItemColumnDataAsFloat(itemId, HierarchyFrameDataView.columnTotalTime), 3),
                selfMs       = Math.Round(frameData.GetItemColumnDataAsFloat(itemId, HierarchyFrameDataView.columnSelfTime), 3),
                calls        = (int)frameData.GetItemColumnDataAsFloat(itemId, HierarchyFrameDataView.columnCalls),
                gcAllocBytes = (long)frameData.GetItemColumnDataAsFloat(itemId, HierarchyFrameDataView.columnGcMemory),
            };

            if (currentDepth < maxDepth)
            {
                var childIds = new List<int>();
                frameData.GetItemChildren(itemId, childIds);

                if (childIds.Count > 0)
                {
                    node.children = new List<ProfilerFrameNode>();
                    foreach (int childId in childIds)
                    {
                        float childTotal = frameData.GetItemColumnDataAsFloat(childId, HierarchyFrameDataView.columnTotalTime);
                        if (childTotal >= minTotalMs)
                        {
                            node.children.Add(BuildNodeTree(frameData, childId, currentDepth + 1, maxDepth, minTotalMs));
                        }
                    }
                }
            }

            return node;
        }

        /// <summary>
        /// 将调用树扁平化为列表，保留调用路径
        /// </summary>
        protected static void FlattenTree(ProfilerFrameNode node, List<ProfilerFlatEntry> flat, string parentPath = "")
        {
            string currentPath = string.IsNullOrEmpty(parentPath)
                ? node.name
                : parentPath + " > " + node.name;

            flat.Add(new ProfilerFlatEntry
            {
                name         = node.name,
                callPath     = currentPath,
                totalMs      = node.totalMs,
                selfMs       = node.selfMs,
                calls        = node.calls,
                gcAllocBytes = node.gcAllocBytes,
            });

            if (node.children != null)
            {
                foreach (var child in node.children)
                    FlattenTree(child, flat, currentPath);
            }
        }

        /// <summary>
        /// 获取有效的 HierarchyFrameDataView，失败时抛出异常
        /// </summary>
        protected static HierarchyFrameDataView GetValidFrameData(int frameIndex, int threadIndex)
        {
            var frameData = UnityEditorInternal.ProfilerDriver.GetHierarchyFrameDataView(
                frameIndex,
                threadIndex,
                HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                HierarchyFrameDataView.columnTotalTime,
                false
            );

            if (frameData == null || !frameData.valid)
                throw new InvalidOperationException(Error.NoFrameData(frameIndex, threadIndex));

            return frameData;
        }

        /// <summary>
        /// 确保 Profiler 已启用并打开窗口。
        /// 如果是运行时（Play Mode），自动开始采集。
        /// </summary>
        /// <returns>true 表示 Profiler 已有帧数据可用；false 表示刚刚启用，需等待下一帧。</returns>
        protected static bool EnsureProfilerEnabled()
        {
            // 打开 Profiler 窗口（如果未打开）
            try
            {
                EditorApplication.ExecuteMenuItem("Window/Analysis/Profiler");
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning($"[Profiler] Failed to open Profiler window via menu: {e.Message}"); }

            // 启用 Profiler
            if (!ProfilerDriver.enabled)
                ProfilerDriver.enabled = true;

            // 设置采集目标：运行时采集 Player，编辑时采集 Editor
            ProfilerDriver.profileEditor = !EditorApplication.isPlaying;

            return ProfilerDriver.lastFrameIndex >= 0;
        }

        /// <summary>
        /// 获取最新有效帧索引。如果 Profiler 未启用，自动启用并提示重试。
        /// </summary>
        protected static int GetLastFrameIndex()
        {
            int lastFrame = ProfilerDriver.lastFrameIndex;
            if (lastFrame < 0)
            {
                // 自动启用 Profiler
                if (EnsureProfilerEnabled())
                {
                    // 启用后立即有数据（之前已有录制）
                    lastFrame = ProfilerDriver.lastFrameIndex;
                    if (lastFrame >= 0)
                        return lastFrame;
                }
                // 刚启用，尚无帧数据
                throw new InvalidOperationException(Error.ProfilerJustEnabled());
            }
            return lastFrame;
        }

        #endregion
    }
}
