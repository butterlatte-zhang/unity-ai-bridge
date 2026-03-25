#nullable enable
using System;
using System.ComponentModel;
using System.IO;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityEditor;
using UnityEngine;

namespace UnityAiBridge.Editor.Tools
{
    public static partial class Tool_Screenshot
    {
        public const string ScreenshotCaptureToolId = "screenshot-capture";

        [BridgeTool
        (
            ScreenshotCaptureToolId,
            Title = "Screenshot / Capture"
        )]
        [Description("Capture a screenshot of the Game view (Play Mode) or Scene view (Edit Mode) " +
            "and save it as a PNG file. Returns the file path so Claude can use the Read tool " +
            "to view the image. Useful for visually verifying game state, UI layout, or debugging rendering issues.")]
        public static ScreenshotResult Capture(
            [Description("Optional tag prefix for the filename (e.g. 'before-fix', 'ui-check'). " +
                "If omitted, the filename is 'screenshot_<timestamp>.png'.")]
            string tag = "",
            [Description("Output width in pixels (default: 960).")]
            int width = 960,
            [Description("Output height in pixels (default: 540).")]
            int height = 540,
            [Description("Super-sampling multiplier for higher quality (1-4, default: 1). " +
                "Only applies to Play Mode captures.")]
            int superSize = 1)
        {
            bool isPlayMode = EditorApplication.isPlaying;

            return MainThread.Instance.Run(() =>
            {
                Directory.CreateDirectory(ScreenshotDir);

                string filename = string.IsNullOrEmpty(tag)
                    ? $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"
                    : $"{tag}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string fullPath = Path.Combine(ScreenshotDir, filename);

                byte[] pngData;

                if (isPlayMode)
                {
                    pngData = CaptureGameView(width, height, superSize);
                }
                else
                {
                    pngData = CaptureSceneView(width, height);
                }

                File.WriteAllBytes(fullPath, pngData);

                var fileInfo = new FileInfo(fullPath);
                return new ScreenshotResult
                {
                    path = fullPath,
                    width = width,
                    height = height,
                    fileSize = fileInfo.Length,
                    isPlayMode = isPlayMode
                };
            });
        }

        /// <summary>
        /// Capture the Game view during Play Mode using ScreenCapture API.
        /// </summary>
        private static byte[] CaptureGameView(int width, int height, int superSize)
        {
            superSize = Mathf.Clamp(superSize, 1, 4);

            var screenTex = ScreenCapture.CaptureScreenshotAsTexture(superSize);
            if (screenTex == null)
                throw new InvalidOperationException(Error.CaptureReturnedNull());

            Texture2D? finalTex = null;
            RenderTexture? rt = null;
            try
            {
                if (screenTex.width != width || screenTex.height != height)
                {
                    // Resize via RenderTexture blit.
                    rt = RenderTexture.GetTemporary(width, height, 0);
                    Graphics.Blit(screenTex, rt);

                    var prevActive = RenderTexture.active;
                    RenderTexture.active = rt;
                    finalTex = new Texture2D(width, height, TextureFormat.RGB24, false);
                    finalTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    finalTex.Apply();
                    RenderTexture.active = prevActive;
                }
                else
                {
                    finalTex = screenTex;
                    screenTex = null; // ownership transferred
                }

                byte[]? pngData = finalTex.EncodeToPNG();
                if (pngData == null || pngData.Length == 0)
                    throw new InvalidOperationException(Error.PngEncodingFailed());

                return pngData;
            }
            finally
            {
                if (rt != null)
                    RenderTexture.ReleaseTemporary(rt);
                if (finalTex != null)
                    UnityEngine.Object.Destroy(finalTex);
                if (screenTex != null)
                    UnityEngine.Object.Destroy(screenTex);
            }
        }

        /// <summary>
        /// Capture the Scene view in Edit Mode by rendering the SceneView camera.
        /// </summary>
        private static byte[] CaptureSceneView(int width, int height)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                throw new InvalidOperationException(Error.NoSceneView());

            var camera = sceneView.camera;
            if (camera == null)
                throw new InvalidOperationException(Error.SceneViewRenderFailed());

            RenderTexture? rt = null;
            Texture2D? tex = null;
            try
            {
                rt = RenderTexture.GetTemporary(width, height, 24);
                camera.targetTexture = rt;
                camera.Render();

                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;

                camera.targetTexture = null;

                byte[]? pngData = tex.EncodeToPNG();
                if (pngData == null || pngData.Length == 0)
                    throw new InvalidOperationException(Error.PngEncodingFailed());

                return pngData;
            }
            finally
            {
                if (rt != null)
                {
                    camera.targetTexture = null;
                    RenderTexture.ReleaseTemporary(rt);
                }
                if (tex != null)
                    UnityEngine.Object.Destroy(tex);
            }
        }
    }
}
