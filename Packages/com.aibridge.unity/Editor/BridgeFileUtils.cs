#nullable enable

using System.IO;
using UnityEngine;

namespace UnityAiBridge.Editor
{
    /// <summary>
    /// 共享的文件工具方法。
    /// </summary>
    internal static class BridgeFileUtils
    {
        /// <summary>
        /// 原子写入文件：先写 .tmp 再 rename，避免读到半写文件。
        /// </summary>
        internal static void WriteAtomically(string targetPath, string content)
        {
            var tmpPath = targetPath + ".tmp";
            try
            {
                File.WriteAllText(tmpPath, content);
#if UNITY_EDITOR_WIN
                // Windows: File.Move 不能覆盖，用 File.Replace
                if (File.Exists(targetPath))
                    File.Replace(tmpPath, targetPath, null);
                else
                    File.Move(tmpPath, targetPath);
#else
                // Unix: 先删再 Move（rename(2) 本身支持覆盖，但 Mono 的 File.Move 不支持）
                if (File.Exists(targetPath))
                    File.Delete(targetPath);
                File.Move(tmpPath, targetPath);
#endif
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BridgeFileUtils] Atomic write failed, falling back to direct write: {ex.Message}");
                // 兜底：直接写入
                try { File.WriteAllText(targetPath, content); }
                catch (System.Exception ex2) { Debug.LogWarning($"[BridgeFileUtils] Fallback write also failed: {ex2.Message}"); }
                try { if (File.Exists(tmpPath)) File.Delete(tmpPath); }
                catch (System.Exception ex3) { Debug.LogWarning($"[BridgeFileUtils] Failed to cleanup tmp file: {ex3.Message}"); }
            }
        }
    }
}
