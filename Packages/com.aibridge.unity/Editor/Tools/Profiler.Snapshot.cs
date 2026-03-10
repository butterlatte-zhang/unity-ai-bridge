#nullable enable
using System.ComponentModel;
using UnityAiBridge;
using UnityAiBridge.Utils;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Profiler
    {
        public const string ProfilerSnapshotToolId = "profiler-snapshot";

        [BridgeTool
        (
            ProfilerSnapshotToolId,
            Title = "Profiler / Snapshot"
        )]
        [Description("Captures a quick performance snapshot including FPS, memory usage, " +
            "draw calls, triangles, and other key metrics. " +
            "Useful for a quick overview of current performance status.")]
        public ProfilerSnapshotResult GetSnapshot()
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                // 确保 Profiler 已启用
                EnsureProfilerEnabled();

                var result = new ProfilerSnapshotResult();

                // Frame info
                result.frameIndex  = Time.frameCount;
                result.frameTimeMs = Time.unscaledDeltaTime * 1000f;
                result.fps         = Time.unscaledDeltaTime > 0 ? 1f / Time.unscaledDeltaTime : 0f;

                // Memory (always available)
                result.totalAllocatedMemory = Profiler.GetTotalAllocatedMemoryLong();
                result.totalReservedMemory  = Profiler.GetTotalReservedMemoryLong();
                result.monoHeapSize         = Profiler.GetMonoHeapSizeLong();
                result.monoUsedSize         = Profiler.GetMonoUsedSizeLong();

                // Render stats via ProfilerRecorder (one-shot read)
                TryReadRecorder(ProfilerCategory.Render, "Draw Calls Count",    out result.drawCalls);
                TryReadRecorder(ProfilerCategory.Render, "SetPass Calls Count", out result.setPassCalls);
                TryReadRecorder(ProfilerCategory.Render, "Triangles Count",     out result.triangles);
                TryReadRecorder(ProfilerCategory.Render, "Vertices Count",      out result.vertices);

                return result;
            });
        }

        private static void TryReadRecorder(ProfilerCategory category, string name, out long value)
        {
            value = 0;
            try
            {
                using var recorder = ProfilerRecorder.StartNew(category, name, 1);
                value = recorder.CurrentValue;
            }
            catch
            {
                // Recorder not available (e.g., not in Play mode)
            }
        }
    }
}
