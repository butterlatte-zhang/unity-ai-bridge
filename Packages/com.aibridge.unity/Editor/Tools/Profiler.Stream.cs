#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using UnityAiBridge;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Profiler
    {
        public const string ProfilerStreamToolId = "profiler-stream";

        #region Data Models

        public class ProfilerStreamSample
        {
            public int    sampleIndex;
            public int    frameIndex;
            public long   timestamp;
            public ProfilerSnapshotResult? snapshot;
            public StreamGcAllocData?      gcAlloc;
            public StreamHotPathData?      hotpath;
            public StreamCallCountData?    callCounts;
        }

        public class StreamGcAllocData
        {
            public long                    totalBytes;
            public List<StreamFlatEntry>?  top;
        }

        public class StreamHotPathData
        {
            public List<StreamFlatEntry>?  top;
        }

        public class StreamCallCountData
        {
            public List<StreamFlatEntry>?  top;
        }

        public class StreamFlatEntry
        {
            public string name     = string.Empty;
            public string callPath = string.Empty;
            public long   bytes;
            public double selfTimeMs;
            public double selfTimePercent;
            public int    calls;
        }

        // [P2 Step 1] JSONL 摘要行 — 每帧 ~250 字节，支持长时采样
        public class ProfilerStreamSummaryLine
        {
            public int     si;      // sampleIndex
            public int     fi;      // frameIndex
            public long    ts;      // timestamp
            public float   fps;
            public long    dc;      // drawCalls
            public long    tri;     // triangles
            public long    mem;     // monoHeapSize bytes
            public long    gc;      // gcAllocBytes this frame
            public string? hp;      // hotpath top1 name (if self% > 5%)
            public double  hp_pct;  // hotpath top1 self%
            public int     cc;      // callCount top1 calls
        }

        public class ProfilerStreamResult
        {
            public string status      = "completed";
            public int    totalSamples;
            public string outputPath  = string.Empty;
            public string statsPath   = string.Empty;
            public string sceneName   = string.Empty;
            public long   durationMs;
            public ProfilerStreamStats? stats;
            // [P2 Step 4] 截图信息
            public int    screenshotCount;
            public string screenshotDir = string.Empty;
        }

        public class ProfilerStreamStats
        {
            public StreamStatPair drawCalls        = new();
            public StreamStatPair setPassCalls     = new();
            public StreamStatPair triangles        = new();
            public StreamStatPair vertices         = new();
            public StreamStatPair monoHeapSizeMB   = new();
            public StreamStatPair gcAllocBytesPerFrame = new();
            public double hotpathMaxSelfTimePercent;
            public int    functionMaxCallCount;
            public List<StreamFlatEntry>? gcAllocTopFunctions;
            public List<StreamFlatEntry>? hotpathTopFunctions;
            public List<StreamFlatEntry>? callCountTopFunctions;
            // [P2 Step 2] 分位数统计
            public double gcAllocP95;
            public double gcAllocP99;
            public float  fpsP5;   // 5th percentile FPS (worst 5%)
            public float  fpsP1;   // 1st percentile FPS (worst 1%)
            // [P2 Step 3] 采样自身开销度量
            public double samplingOverheadAvgMs;
            public double samplingOverheadMaxMs;
        }

        public class StreamStatPair
        {
            public double avg;
            public double max;
        }

        #endregion

        [BridgeTool
        (
            ProfilerStreamToolId,
            Title = "Profiler / Stream"
        )]
        [Description("Continuously samples Profiler data across multiple frames " +
            "(snapshot + GC allocations + hot paths + call counts) and writes results to a JSONL file. " +
            "frames <= 0 starts continuous mode (returns immediately, runs until stop signal). " +
            "frames > 0 runs for exactly N frames then returns aggregated stats.")]
        public Task<ProfilerStreamResult> StreamProfile(
            [Description("Number of frames to sample. 0 or negative = continuous mode (runs until stop signal file). Default: 0")]
            int frames = 0,
            [Description("Frame interval (Unity frames to skip between samples). Default: 2")]
            int frameInterval = 2,
            [Description("Top N GC allocation entries per frame. Default: 20")]
            int gcTopN = 20,
            [Description("Top N hot path entries per frame. Default: 20")]
            int hotpathTopN = 20,
            [Description("Maximum call hierarchy depth. Default: 8")]
            int hierarchyMaxDepth = 8
        )
        {
            // 参数校验
            if (frameInterval < 1) frameInterval = 1;
            if (gcTopN < 1) gcTopN = 1;
            if (hotpathTopN < 1) hotpathTopN = 1;
            if (hierarchyMaxDepth < 1) hierarchyMaxDepth = 1;

            bool isContinuous = frames <= 0;
            if (!isContinuous && frames > 10000) frames = 10000;

            var cfg = new StreamConfig
            {
                Frames            = frames,
                FrameInterval     = frameInterval,
                GcTopN            = gcTopN,
                HotpathTopN       = hotpathTopN,
                HierarchyMaxDepth = hierarchyMaxDepth,
                IsContinuous      = isContinuous,
                HierarchyInterval = isContinuous ? 5 : 1, // 持续模式每 5 帧采一次层级，降低开销
            };

            if (isContinuous)
            {
                // 持续模式：立即返回，采样在后台持续运行
                string sceneName = SceneManager.GetActiveScene().name;
                EditorApplication.delayCall += () => StartStreamSampling(null, cfg);
                return Task.FromResult(new ProfilerStreamResult
                {
                    status    = "streaming",
                    outputPath = GetJsonlPath(),
                    statsPath  = GetStatsPath(),
                    sceneName  = sceneName,
                });
            }
            else
            {
                // 定帧模式：返回 Task，采样完成后 resolve
                var tcs = new TaskCompletionSource<ProfilerStreamResult>();
                EditorApplication.delayCall += () => StartStreamSampling(tcs, cfg);
                return tcs.Task;
            }
        }

        #region Stream Internal

        private struct StreamConfig
        {
            public int  Frames;
            public int  FrameInterval;
            public int  GcTopN;
            public int  HotpathTopN;
            public int  HierarchyMaxDepth;
            public bool IsContinuous;
            public int  HierarchyInterval; // 每 N 帧采一次层级数据（1=每帧，>1 降低开销）
        }

        private class StreamState
        {
            public TaskCompletionSource<ProfilerStreamResult>? Tcs;
            public StreamConfig Cfg;
            public string OutputPath = string.Empty;
            public StreamWriter? Writer;
            public int SampleIndex;
            public int LastSampledProfilerFrame = -1;
            public long StartTimestamp;
            public bool OpenedByTool;
            public bool WasPlaying;
            public bool ProfilerWindowConfirmed;
            public StreamAccumulator Accumulator = new();

            // 持久 ProfilerRecorder（生命周期与 stream 一致）
            public ProfilerRecorder RecDrawCalls;
            public ProfilerRecorder RecSetPassCalls;
            public ProfilerRecorder RecTriangles;
            public ProfilerRecorder RecVertices;

            // [P2 Step 4] 围墙预警截图
            public Dictionary<string, double>? Thresholds;
            public string ScreenshotDir = string.Empty;
            public Dictionary<string, long> LastScreenshotReasons = new();
            public int ScreenshotCount;
        }

        /// <summary>
        /// 实时聚合统计器，避免完成时重新遍历 JSONL。
        /// </summary>
        private class StreamAccumulator
        {
            public int Count;

            // Snapshot stats
            public long SumDrawCalls, MaxDrawCalls;
            public long SumSetPassCalls, MaxSetPassCalls;
            public long SumTriangles, MaxTriangles;
            public long SumVertices, MaxVertices;
            public long SumMonoHeapSize, MaxMonoHeapSize;

            // GC alloc per frame（仅层级采样帧有数据）
            public int  GcSampleCount;
            public long SumGcAllocBytes, MaxGcAllocBytes;
            public List<StreamFlatEntry>? WorstGcTop;

            // Hotpath
            public double MaxSelfTimePercent;
            public List<StreamFlatEntry>? WorstHotpathTop;

            // Call count
            public int MaxCallCount;
            public List<StreamFlatEntry>? WorstCallCountTop;

            // [P2 Step 2] 分位数用：存储每帧值
            public List<long>   FrameGcAllocs    = new();
            public List<double> FrameHotpathPcts = new();
            public List<int>    FrameCallCounts  = new();
            public List<float>  FrameFps         = new();

            // [P2 Step 3] 采样自身开销度量
            public double SumOverheadMs;
            public double MaxOverheadMs;

            public void Accumulate(ProfilerStreamSample sample)
            {
                Count++;

                if (sample.snapshot != null)
                {
                    var s = sample.snapshot;
                    SumDrawCalls    += s.drawCalls;    MaxDrawCalls    = Math.Max(MaxDrawCalls, s.drawCalls);
                    SumSetPassCalls += s.setPassCalls;  MaxSetPassCalls = Math.Max(MaxSetPassCalls, s.setPassCalls);
                    SumTriangles    += s.triangles;     MaxTriangles    = Math.Max(MaxTriangles, s.triangles);
                    SumVertices     += s.vertices;      MaxVertices     = Math.Max(MaxVertices, s.vertices);
                    SumMonoHeapSize += s.monoHeapSize;  MaxMonoHeapSize = Math.Max(MaxMonoHeapSize, s.monoHeapSize);

                    // [P2 Step 2] FPS 分位数
                    if (s.fps > 0)
                        FrameFps.Add(s.fps);
                }

                if (sample.gcAlloc != null)
                {
                    GcSampleCount++;
                    SumGcAllocBytes += sample.gcAlloc.totalBytes;
                    if (sample.gcAlloc.totalBytes > MaxGcAllocBytes)
                    {
                        MaxGcAllocBytes = sample.gcAlloc.totalBytes;
                        WorstGcTop = sample.gcAlloc.top;
                    }
                    FrameGcAllocs.Add(sample.gcAlloc.totalBytes);
                }

                if (sample.hotpath?.top != null && sample.hotpath.top.Count > 0)
                {
                    double frameMax = sample.hotpath.top[0].selfTimePercent;
                    if (frameMax > MaxSelfTimePercent)
                    {
                        MaxSelfTimePercent = frameMax;
                        WorstHotpathTop = sample.hotpath.top;
                    }
                    // [P2 Step 2] Hotpath 分位数
                    FrameHotpathPcts.Add(frameMax);
                }

                if (sample.callCounts?.top != null && sample.callCounts.top.Count > 0)
                {
                    int frameMax = sample.callCounts.top[0].calls;
                    if (frameMax > MaxCallCount)
                    {
                        MaxCallCount = frameMax;
                        WorstCallCountTop = sample.callCounts.top;
                    }
                    // [P2 Step 2] CallCount 分位数
                    FrameCallCounts.Add(frameMax);
                }
            }

            // [P2 Step 3] 记录采样开销
            public void AccumulateOverhead(double ms)
            {
                SumOverheadMs += ms;
                MaxOverheadMs = Math.Max(MaxOverheadMs, ms);
            }

            public ProfilerStreamStats ToStats()
            {
                double n = Count > 0 ? Count : 1;
                var stats = new ProfilerStreamStats
                {
                    drawCalls    = new StreamStatPair { avg = Math.Round(SumDrawCalls / n, 1),    max = MaxDrawCalls },
                    setPassCalls = new StreamStatPair { avg = Math.Round(SumSetPassCalls / n, 1), max = MaxSetPassCalls },
                    triangles    = new StreamStatPair { avg = Math.Round(SumTriangles / n, 1),    max = MaxTriangles },
                    vertices     = new StreamStatPair { avg = Math.Round(SumVertices / n, 1),     max = MaxVertices },
                    monoHeapSizeMB = new StreamStatPair
                    {
                        avg = Math.Round(SumMonoHeapSize / n / 1048576.0, 2),
                        max = Math.Round(MaxMonoHeapSize / 1048576.0, 2),
                    },
                    gcAllocBytesPerFrame = new StreamStatPair { avg = Math.Round(SumGcAllocBytes / (GcSampleCount > 0 ? GcSampleCount : 1.0), 1), max = MaxGcAllocBytes },
                    hotpathMaxSelfTimePercent = MaxSelfTimePercent,
                    functionMaxCallCount      = MaxCallCount,
                    gcAllocTopFunctions       = WorstGcTop,
                    hotpathTopFunctions       = WorstHotpathTop,
                    callCountTopFunctions     = WorstCallCountTop,
                    // [P2 Step 2] 分位数
                    gcAllocP95 = Percentile(FrameGcAllocs, 0.95),
                    gcAllocP99 = Percentile(FrameGcAllocs, 0.99),
                    fpsP5      = PercentileFloat(FrameFps, 0.05),
                    fpsP1      = PercentileFloat(FrameFps, 0.01),
                    // [P2 Step 3] 采样开销
                    samplingOverheadAvgMs = Count > 0 ? Math.Round(SumOverheadMs / Count, 3) : 0,
                    samplingOverheadMaxMs = Math.Round(MaxOverheadMs, 3),
                };
                return stats;
            }

            // [P2 Step 2] 分位数计算
            private static double Percentile(List<long> values, double p)
            {
                if (values.Count == 0) return 0;
                var sorted = new List<long>(values);
                sorted.Sort();
                int index = (int)Math.Ceiling(p * sorted.Count) - 1;
                return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
            }

            private static float PercentileFloat(List<float> values, double p)
            {
                if (values.Count == 0) return 0;
                var sorted = new List<float>(values);
                sorted.Sort();
                int index = (int)Math.Ceiling(p * sorted.Count) - 1;
                return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
            }
        }

        private static StreamState? _activeStream;

        #region Paths

        private static string GetBridgeRoot()
            => Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Temp", "UnityBridge");

        private static string GetJsonlPath()
            => Path.Combine(GetBridgeRoot(), "profiler-stream.jsonl");

        private static string GetStatsPath()
            => Path.Combine(GetBridgeRoot(), "profiler-stream-stats.json");

        private static string GetStopSignalPath()
            => Path.Combine(GetBridgeRoot(), "profiler-stream.stop");

        #endregion

        #region Noise Filter

        // 按前缀匹配排除的函数（整棵子树或自身）
        private static readonly string[] _editorExcludePatterns = new[]
        {
            "EditorLoop",
            "Profiler.",
            "ProfilerRecorder",
            "LogStringToConsole",
            "GUIRepaint",
            "GUI.Repaint",
            "EditorSubScene",
            "LiveConversion",
            "InspectorWindow",
            "SceneView",
            "ConsoleWindow",
            "GameView.Repaint",
        };

        // 精确匹配排除 self time 的函数（仅跳过自身，保留子节点）
        // PlayerLoop: self time 是 VSync 空闲等待，不是 CPU 瓶颈
        // Semaphore.WaitForSignal: 主线程等渲染线程的同步点
        private static readonly HashSet<string> _selfTimeExcludeNames = new()
        {
            "PlayerLoop",
            "Semaphore.WaitForSignal",
        };

        private static bool IsEditorInternal(string name, string callPath)
        {
            // 顶层 EditorLoop 的整棵子树都排除
            if (callPath.StartsWith("EditorLoop"))
                return true;
            // 按前缀匹配
            foreach (var pattern in _editorExcludePatterns)
                if (name.StartsWith(pattern))
                    return true;
            return false;
        }

        // 仅在 hotpath（self time 排序）中排除，不影响 GC/callCount
        private static bool IsSelfTimeNoise(string name)
        {
            return _selfTimeExcludeNames.Contains(name);
        }

        #endregion

        private static void StartStreamSampling(TaskCompletionSource<ProfilerStreamResult>? tcs, StreamConfig cfg)
        {
            try
            {
                // 取消已有的活跃采样流
                if (_activeStream != null)
                {
                    var oldState = _activeStream;
                    StopAndCleanup(oldState);
                    oldState.Tcs?.TrySetException(
                        new InvalidOperationException("Stream cancelled: a new stream was started."));
                }

                // 清除残留的停止信号
                string stopPath = GetStopSignalPath();
                if (File.Exists(stopPath))
                    try { File.Delete(stopPath); } catch (Exception e) { Debug.LogWarning($"[ProfilerStream] Failed to delete stop signal: {e.Message}"); }

                // 清除上次的 stats 文件，防止读到 stale 数据
                string statsPath = GetStatsPath();
                if (File.Exists(statsPath))
                    try { File.Delete(statsPath); } catch (Exception e) { Debug.LogWarning($"[ProfilerStream] Failed to delete stats file: {e.Message}"); }

                // 记录 Profiler 窗口状态
                bool profilerWasOpen = EditorWindow.HasOpenInstances<ProfilerWindow>();

                // 自动打开 Profiler 并启用录制
                EnsureProfilerEnabled();

                // 准备 JSONL 输出文件
                string bridgeRoot = GetBridgeRoot();
                Directory.CreateDirectory(bridgeRoot);
                string outputPath = GetJsonlPath();

                // AutoFlush=false: 缓冲写入，避免每帧同步磁盘 I/O 干扰 Semaphore.WaitForSignal
                var writer = new StreamWriter(outputPath, append: false, Encoding.UTF8, bufferSize: 65536);

                // [P2 Step 4] 加载围墙阈值 + 创建截图目录
                var thresholds = LoadBaselineThresholds();
                string screenshotDir = string.Empty;
                if (thresholds != null)
                {
                    screenshotDir = Path.Combine(bridgeRoot, "profiler-screenshots",
                        DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));
                    Directory.CreateDirectory(screenshotDir);
                }

                _activeStream = new StreamState
                {
                    Tcs                      = tcs,
                    Cfg                      = cfg,
                    OutputPath               = outputPath,
                    Writer                   = writer,
                    SampleIndex              = 0,
                    LastSampledProfilerFrame  = -1,
                    StartTimestamp            = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    OpenedByTool             = !profilerWasOpen,
                    WasPlaying               = EditorApplication.isPlaying,
                    Thresholds               = thresholds,
                    ScreenshotDir            = screenshotDir,
                };

                // 创建持久 ProfilerRecorder（生命周期与 stream 一致）
                _activeStream.RecDrawCalls    = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                _activeStream.RecSetPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                _activeStream.RecTriangles    = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
                _activeStream.RecVertices     = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");

                EditorApplication.update += OnStreamUpdate;

                string mode = cfg.IsContinuous ? "continuous" : $"{cfg.Frames} frames";
                string thresholdInfo = thresholds != null ? $", thresholds={thresholds.Count}" : "";
                Debug.Log($"[ProfilerStream] Started ({mode}), interval={cfg.FrameInterval}{thresholdInfo}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProfilerStream] Failed to start: {e.Message}\n{e.StackTrace}");
                tcs?.TrySetException(e);
            }
        }

        private static void OnStreamUpdate()
        {
            if (_activeStream == null)
            {
                EditorApplication.update -= OnStreamUpdate;
                return;
            }

            var state = _activeStream;

            try
            {
                // 检测停止信号文件
                if (File.Exists(GetStopSignalPath()))
                {
                    try { File.Delete(GetStopSignalPath()); } catch (Exception e) { Debug.LogWarning($"[ProfilerStream] Failed to delete stop signal: {e.Message}"); }
                    Debug.Log("[ProfilerStream] Stop signal received.");
                    CompleteStream(state);
                    return;
                }

                // 检测 Play Mode 中途退出
                if (state.WasPlaying && !EditorApplication.isPlaying)
                {
                    Debug.Log("[ProfilerStream] Play Mode exited, completing.");
                    CompleteStream(state);
                    return;
                }

                // 检测 Profiler 窗口状态（需先确认窗口存在过，再检测关闭）
                bool profilerOpen = EditorWindow.HasOpenInstances<ProfilerWindow>();
                if (profilerOpen && !state.ProfilerWindowConfirmed)
                {
                    state.ProfilerWindowConfirmed = true;
                }
                if (state.ProfilerWindowConfirmed && !profilerOpen)
                {
                    Debug.Log("[ProfilerStream] Profiler window closed, completing.");
                    CompleteStream(state);
                    return;
                }

                // 检查帧间隔是否满足
                int currentProfilerFrame = ProfilerDriver.lastFrameIndex;
                if (currentProfilerFrame < 0)
                    return;

                if (state.LastSampledProfilerFrame >= 0 &&
                    currentProfilerFrame - state.LastSampledProfilerFrame < state.Cfg.FrameInterval)
                    return;

                // [P2 Step 3] 采集一帧数据（含 Stopwatch 计时）
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var sample = CollectOneSample(state, currentProfilerFrame);
                sw.Stop();

                if (sample != null)
                {
                    // [P2 Step 1] 写 JSONL 摘要行（而非完整 sample）
                    var summaryLine = new ProfilerStreamSummaryLine
                    {
                        si     = sample.sampleIndex,
                        fi     = sample.frameIndex,
                        ts     = sample.timestamp,
                        fps    = sample.snapshot?.fps ?? -1,
                        dc     = sample.snapshot?.drawCalls ?? 0,
                        tri    = sample.snapshot?.triangles ?? 0,
                        mem    = sample.snapshot?.monoHeapSize ?? 0,
                        gc     = sample.gcAlloc?.totalBytes ?? 0,
                        hp     = (sample.hotpath?.top?.Count > 0 && sample.hotpath.top[0].selfTimePercent > 5)
                                 ? sample.hotpath.top[0].name : null,
                        hp_pct = (sample.hotpath?.top?.Count > 0)
                                 ? Math.Round(sample.hotpath.top[0].selfTimePercent, 2) : 0,
                        cc     = (sample.callCounts?.top?.Count > 0)
                                 ? sample.callCounts.top[0].calls : 0,
                    };
                    var json = JsonSerializer.Serialize(summaryLine, s_jsonlOptions);
                    state.Writer?.WriteLine(json);

                    // 实时聚合统计
                    state.Accumulator.Accumulate(sample);

                    // [P2 Step 3] 记录采样开销
                    state.Accumulator.AccumulateOverhead(sw.Elapsed.TotalMilliseconds);

                    // [P2 Step 4] 检查围墙超标截图
                    CheckAndCaptureScreenshot(state, sample);

                    state.LastSampledProfilerFrame = currentProfilerFrame;
                    state.SampleIndex++;

                    // [P2 Step 4] 持续模式下增量 stats 输出 + JSONL flush
                    if (state.Cfg.IsContinuous &&
                        state.SampleIndex % IncrementalStatsInterval == 0 && state.SampleIndex > 0)
                    {
                        try { state.Writer?.Flush(); } catch (Exception e) { Debug.LogWarning($"[ProfilerStream] Failed to flush writer: {e.Message}"); }
                        WriteIncrementalStats(state);
                    }
                }

                // 定帧模式：检查是否采完
                if (!state.Cfg.IsContinuous && state.SampleIndex >= state.Cfg.Frames)
                {
                    CompleteStream(state);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProfilerStream] Error during sampling: {e.Message}\n{e.StackTrace}");
                StopAndCleanup(state);
                state.Tcs?.TrySetException(e);
            }
        }

        private static ProfilerStreamSample? CollectOneSample(StreamState state, int profilerFrameIndex)
        {
            var sample = new ProfilerStreamSample
            {
                sampleIndex = state.SampleIndex,
                frameIndex  = profilerFrameIndex,
                timestamp   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };

            // 1. Snapshot 数据（从持久 Recorder 读取渲染统计，几乎零开销）
            sample.snapshot = CollectSnapshot(state, profilerFrameIndex);

            // 2. 层级数据（一次 BuildNodeTree + FlattenTree，开销较大）
            //    持续模式下按 HierarchyInterval 间隔采集，降低采样开销
            bool collectHierarchy = state.Cfg.HierarchyInterval <= 1
                                    || state.SampleIndex % state.Cfg.HierarchyInterval == 0;
            if (!collectHierarchy)
                return sample;

            try
            {
                using var frameData = ProfilerDriver.GetHierarchyFrameDataView(
                    profilerFrameIndex,
                    0,
                    HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                    HierarchyFrameDataView.columnTotalTime,
                    false
                );

                if (frameData != null && frameData.valid)
                {
                    int rootId = frameData.GetRootItemID();
                    var childIds = new List<int>();
                    frameData.GetItemChildren(rootId, childIds);

                    var allNodes = new List<ProfilerFrameNode>();
                    double frameTotalMs = 0;
                    foreach (int childId in childIds)
                    {
                        var node = BuildNodeTree(frameData, childId, 1, state.Cfg.HierarchyMaxDepth, 0f);
                        allNodes.Add(node);
                        // 编辑器内部开销不计入帧总时间，避免 FPS 被拉低
                        if (!IsEditorInternal(node.name, node.name))
                            frameTotalMs += node.totalMs;
                    }

                    var flat = new List<ProfilerFlatEntry>();
                    foreach (var node in allNodes)
                        FlattenTree(node, flat);

                    sample.gcAlloc    = ExtractGcAlloc(flat, allNodes, state.Cfg.GcTopN);
                    sample.hotpath    = ExtractHotPath(flat, frameTotalMs, state.Cfg.HotpathTopN);
                    sample.callCounts = ExtractCallCounts(flat, state.Cfg.HotpathTopN);

                    // 用层级帧总时间覆盖 snapshot 的 fps/frameTimeMs
                    if (sample.snapshot != null && frameTotalMs > 0)
                    {
                        sample.snapshot.frameTimeMs = (float)Math.Round(frameTotalMs, 3);
                        sample.snapshot.fps = (float)Math.Round(1000.0 / frameTotalMs, 1);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ProfilerStream] Hierarchy unavailable for frame {profilerFrameIndex}: {e.Message}");
            }

            return sample;
        }

        // 从持久 Recorder 读取渲染统计，fps 默认 -1（由层级数据覆盖）
        private static ProfilerSnapshotResult CollectSnapshot(StreamState state, int profilerFrameIndex)
        {
            var result = new ProfilerSnapshotResult
            {
                frameIndex  = profilerFrameIndex,
                frameTimeMs = -1,
                fps         = -1,
            };

            // 内存
            result.totalAllocatedMemory = Profiler.GetTotalAllocatedMemoryLong();
            result.totalReservedMemory  = Profiler.GetTotalReservedMemoryLong();
            result.monoHeapSize         = Profiler.GetMonoHeapSizeLong();
            result.monoUsedSize         = Profiler.GetMonoUsedSizeLong();

            // 渲染统计从持久 Recorder 读取
            result.drawCalls    = state.RecDrawCalls.LastValue;
            result.setPassCalls = state.RecSetPassCalls.LastValue;
            result.triangles    = state.RecTriangles.LastValue;
            result.vertices     = state.RecVertices.LastValue;

            return result;
        }

        #region Extract helpers

        private static StreamGcAllocData ExtractGcAlloc(List<ProfilerFlatEntry> flat, List<ProfilerFrameNode> rootNodes, int topN)
        {
            long totalGcAlloc = 0;
            foreach (var node in rootNodes)
            {
                // 编辑器内部开销的 GC 分配不计入总量
                if (!IsEditorInternal(node.name, node.name))
                    totalGcAlloc += node.gcAllocBytes;
            }

            var topEntries = flat
                .Where(e => e.gcAllocBytes > 0 && !IsEditorInternal(e.name, e.callPath))
                .OrderByDescending(e => e.gcAllocBytes)
                .Take(topN)
                .Select(e => new StreamFlatEntry { name = e.name, callPath = e.callPath, bytes = e.gcAllocBytes })
                .ToList();

            return new StreamGcAllocData
            {
                totalBytes = totalGcAlloc,
                top        = topEntries.Count > 0 ? topEntries : null,
            };
        }

        private static StreamHotPathData ExtractHotPath(List<ProfilerFlatEntry> flat, double frameTotalMs, int topN)
        {
            var topEntries = flat
                .Where(e => e.selfMs > 0 && !IsEditorInternal(e.name, e.callPath) && !IsSelfTimeNoise(e.name))
                .OrderByDescending(e => e.selfMs)
                .Take(topN)
                .Select(e => new StreamFlatEntry
                {
                    name            = e.name,
                    callPath        = e.callPath,
                    selfTimeMs      = Math.Round(e.selfMs, 3),
                    selfTimePercent = frameTotalMs > 0 ? Math.Round(e.selfMs / frameTotalMs * 100, 2) : 0,
                })
                .ToList();

            return new StreamHotPathData { top = topEntries.Count > 0 ? topEntries : null };
        }

        private static StreamCallCountData ExtractCallCounts(List<ProfilerFlatEntry> flat, int topN)
        {
            var topEntries = flat
                .Where(e => e.calls > 0 && !IsEditorInternal(e.name, e.callPath))
                .OrderByDescending(e => e.calls)
                .Take(topN)
                .Select(e => new StreamFlatEntry { name = e.name, callPath = e.callPath, calls = e.calls })
                .ToList();

            return new StreamCallCountData { top = topEntries.Count > 0 ? topEntries : null };
        }

        #endregion

        #region Incremental Stats + Screenshots

        // [P2 Step 4] 增量 stats 输出间隔（每 N 帧写一次中间 stats）
        private const int IncrementalStatsInterval = 100;

        // [P2 Step 4] 加载围墙阈值（从 baseline.json）
        private static Dictionary<string, double>? LoadBaselineThresholds()
        {
            string path = Path.Combine(Application.dataPath, "..", ".claude", "skills", "perf-check", "baseline.json");
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                var thresholds = doc.RootElement.GetProperty("thresholds");
                var result = new Dictionary<string, double>();
                foreach (var prop in thresholds.EnumerateObject())
                {
                    if (prop.Value.TryGetProperty("value", out var val))
                        result[prop.Name] = val.GetDouble();
                }
                return result;
            }
            catch (Exception e) { Debug.LogWarning($"[ProfilerStream] Failed to parse thresholds: {e.Message}"); return null; }
        }

        // [P2 Step 4] 围墙超标截图
        private static void CheckAndCaptureScreenshot(StreamState state, ProfilerStreamSample sample)
        {
            if (state.Thresholds == null || string.IsNullOrEmpty(state.ScreenshotDir)) return;

            var categories = new List<string>();  // 节流 key（仅类别名）
            var details = new List<string>();     // 文件名（含精确值）

            if (sample.gcAlloc != null
                && state.Thresholds.TryGetValue("gcAllocBytesPerFrame", out var gcLimit)
                && sample.gcAlloc.totalBytes > gcLimit)
            {
                categories.Add("gc");
                details.Add($"gc_{sample.gcAlloc.totalBytes}B");
            }

            if (sample.snapshot != null)
            {
                if (state.Thresholds.TryGetValue("drawCalls", out var dcLimit)
                    && sample.snapshot.drawCalls > dcLimit)
                {
                    categories.Add("drawCalls");
                    details.Add($"drawCalls_{sample.snapshot.drawCalls}");
                }

                if (state.Thresholds.TryGetValue("triangles", out var triLimit)
                    && sample.snapshot.triangles > triLimit)
                {
                    categories.Add("triangles");
                    details.Add($"triangles_{sample.snapshot.triangles}");
                }

                if (state.Thresholds.TryGetValue("vertices", out var vertLimit)
                    && sample.snapshot.vertices > vertLimit)
                {
                    categories.Add("vertices");
                    details.Add($"vertices_{sample.snapshot.vertices}");
                }
            }

            if (sample.hotpath?.top?.Count > 0
                && state.Thresholds.TryGetValue("hotpathMaxSelfTimePercent", out var hpLimit)
                && sample.hotpath.top[0].selfTimePercent > hpLimit)
            {
                categories.Add("hotpath");
                details.Add($"hotpath_{sample.hotpath.top[0].selfTimePercent:F1}pct");
            }

            if (categories.Count == 0) return;

            // 节流：同一类别组合 10 秒内不重复截图（key 不含精确值，避免每帧都不同）
            string reasonKey = string.Join("_", categories);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (state.LastScreenshotReasons.TryGetValue(reasonKey, out var lastTime)
                && now - lastTime < 10000)
                return;
            state.LastScreenshotReasons[reasonKey] = now;

            // 全异步截图：AsyncGPUReadback + ThreadPool，零主线程开销
            string fileName = $"frame_{sample.frameIndex}_{string.Join("_", details)}.png";
            string filePath = Path.Combine(state.ScreenshotDir, fileName);
            state.ScreenshotCount++;
            RequestAsyncScreenshot(filePath);
        }

        // 防止并发 readback 堆积
        private static volatile bool _asyncScreenshotPending;

        // 低分辨率截图 RT（降低 GPU readback + 编码开销）
        private const int CaptureWidth = 480;
        private const int CaptureHeight = 270;
        private static RenderTexture? _captureRT;

        /// <summary>
        /// 低开销截图管线：
        /// 1. delayCall 中同步捕获屏幕（CaptureScreenshotAsTexture ~3ms，10s 节流下可接受）
        /// 2. GPU Blit 降采样到 480x270 小 RT
        /// 3. AsyncGPUReadback 从小 RT 读取像素（异步，不阻塞主线程）
        /// 4. ThreadPool 编码 PNG + 写盘
        /// 主线程总开销 ~3-4ms，仅在节流窗口内触发一次。
        /// </summary>
        private static void RequestAsyncScreenshot(string filePath)
        {
            if (_asyncScreenshotPending) return;
            _asyncScreenshotPending = true;

            // 延迟到下一帧执行，确保当前帧渲染完成、backbuffer 可读
            EditorApplication.delayCall += () => CaptureAndReadbackAsync(filePath);
        }

        private static void CaptureAndReadbackAsync(string filePath)
        {
            try
            {
                // 同步捕获 Game View 屏幕内容（~3ms GPU 同步，有 10s 节流保护）
                var tex = ScreenCapture.CaptureScreenshotAsTexture(1);
                if (tex == null)
                {
                    _asyncScreenshotPending = false;
                    return;
                }

                // 惰性创建低分辨率 RT
                if (_captureRT == null || !_captureRT.IsCreated())
                {
                    if (_captureRT != null) UnityEngine.Object.DestroyImmediate(_captureRT);
                    _captureRT = new RenderTexture(CaptureWidth, CaptureHeight, 0, RenderTextureFormat.ARGB32)
                    {
                        name = "ProfilerStreamCapture",
                    };
                    _captureRT.Create();
                }

                // GPU 降采样：全分辨率 Texture2D → 480x270 RT（瞬时，无 CPU 开销）
                Graphics.Blit(tex, _captureRT);
                UnityEngine.Object.DestroyImmediate(tex);

                // 异步回读小 RT（480x270 RGBA = ~518KB，不阻塞主线程）
                AsyncGPUReadback.Request(_captureRT, 0, TextureFormat.RGBA32, request =>
                {
                    _asyncScreenshotPending = false;
                    if (request.hasError) return;

                    var rawData = request.GetData<byte>().ToArray();
                    string path = filePath;

                    System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try
                        {
                            byte[] png = ImageConversion.EncodeArrayToPNG(
                                rawData,
                                UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,
                                CaptureWidth, CaptureHeight);
                            if (png != null && png.Length > 0)
                                File.WriteAllBytes(path, png);
                        }
                        catch (Exception e) { Debug.LogWarning($"[ProfilerStream] Screenshot encode/write failed: {e.Message}"); }
                    });
                });
            }
            catch
            {
                _asyncScreenshotPending = false;
            }
        }

        // [P2 Step 4] 增量 stats 输出（持续模式下每 IncrementalStatsInterval 帧写一次）
        private static void WriteIncrementalStats(StreamState state)
        {
            var stats = state.Accumulator.ToStats();
            var result = new ProfilerStreamResult
            {
                status          = "sampling",
                totalSamples    = state.SampleIndex,
                outputPath      = state.OutputPath,
                statsPath       = GetStatsPath(),
                sceneName       = SceneManager.GetActiveScene().name,
                durationMs      = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - state.StartTimestamp,
                stats           = stats,
                screenshotCount = state.ScreenshotCount,
                screenshotDir   = state.ScreenshotDir,
            };
            WriteStatsFile(result);
        }

        #endregion

        #region Complete / Cleanup

        private static void CompleteStream(StreamState state)
        {
            long endTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string sceneName = SceneManager.GetActiveScene().name;

            // 聚合统计
            var stats = state.Accumulator.ToStats();

            // 写统计文件（两种模式都写，perf-check 从这里读）
            var statsResult = new ProfilerStreamResult
            {
                status          = "completed",
                totalSamples    = state.SampleIndex,
                outputPath      = state.OutputPath,
                statsPath       = GetStatsPath(),
                sceneName       = sceneName,
                durationMs      = endTimestamp - state.StartTimestamp,
                stats           = stats,
                screenshotCount = state.ScreenshotCount,
                screenshotDir   = state.ScreenshotDir,
            };
            WriteStatsFile(statsResult);

            StopAndCleanup(state);

            Debug.Log($"[ProfilerStream] Completed: {state.SampleIndex} samples in {statsResult.durationMs}ms" +
                      (state.ScreenshotCount > 0 ? $", {state.ScreenshotCount} screenshots" : ""));

            // 定帧模式：resolve Task
            state.Tcs?.TrySetResult(statsResult);
        }

        private static void WriteStatsFile(ProfilerStreamResult result)
        {
            try
            {
                string json = JsonSerializer.Serialize(result, s_statsOptions);
                BridgeFileUtils.WriteAtomically(GetStatsPath(), json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProfilerStream] Failed to write stats file: {e.Message}");
            }
        }

        private static void StopAndCleanup(StreamState state)
        {
            EditorApplication.update -= OnStreamUpdate;
            _activeStream = null;
            _asyncScreenshotPending = false;

            // 释放截图 RT
            if (_captureRT != null)
            {
                UnityEngine.Object.DestroyImmediate(_captureRT);
                _captureRT = null;
            }

            try { state.Writer?.Dispose(); }
            catch (Exception e) { Debug.LogWarning($"[ProfilerStream] Failed to dispose writer: {e.Message}"); }
            state.Writer = null;

            // Dispose 持久 Recorder
            state.RecDrawCalls.Dispose();
            state.RecSetPassCalls.Dispose();
            state.RecTriangles.Dispose();
            state.RecVertices.Dispose();

            if (state.OpenedByTool && EditorWindow.HasOpenInstances<ProfilerWindow>())
            {
                try { EditorWindow.GetWindow<ProfilerWindow>().Close(); }
                catch (Exception e) { Debug.LogWarning($"[ProfilerStream] Failed to close Profiler window: {e.Message}"); }
            }
        }

        #endregion

        // JSONL 用 WhenWritingDefault 减少冗余字段
        private static readonly JsonSerializerOptions s_jsonlOptions = new()
        {
            WriteIndented    = false,
            IncludeFields    = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        };

        // Stats 文件保持完整字段（perf-check 依赖所有字段存在）
        private static readonly JsonSerializerOptions s_statsOptions = new()
        {
            WriteIndented    = false,
            IncludeFields    = true,
        };

        #endregion
    }

    // Domain Reload 恢复守卫
    [InitializeOnLoad]
    static class ProfilerStreamReloadGuard
    {
        static ProfilerStreamReloadGuard()
        {
            EditorApplication.delayCall += CheckInterruptedStream;
        }

        private static void CheckInterruptedStream()
        {
            string bridgeRoot = Path.Combine(
                Directory.GetParent(Application.dataPath)!.FullName, "Temp", "UnityBridge");
            string jsonlPath = Path.Combine(bridgeRoot, "profiler-stream.jsonl");
            string statsPath = Path.Combine(bridgeRoot, "profiler-stream-stats.json");

            // JSONL 存在但 stats 不存在 → 采样被 Domain Reload 中断
            if (File.Exists(jsonlPath) && !File.Exists(statsPath))
            {
                Debug.LogWarning("[ProfilerStream] Detected interrupted stream after Domain Reload.");
                var result = new Tool_Profiler.ProfilerStreamResult
                {
                    status    = "interrupted",
                    sceneName = SceneManager.GetActiveScene().name,
                };
                string json = JsonSerializer.Serialize(result, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    IncludeFields = true,
                });
                try
                {
                    BridgeFileUtils.WriteAtomically(statsPath, json);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ProfilerStream] Failed to write interrupted stats: {e.Message}");
                }
            }
        }
    }
}
