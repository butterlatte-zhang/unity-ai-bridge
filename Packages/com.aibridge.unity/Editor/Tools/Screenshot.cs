#nullable enable
using System.ComponentModel;
using System.IO;
using UnityAiBridge;
using UnityEngine;

namespace UnityAiBridge.Editor.Tools
{
    [BridgeToolType]
    public static partial class Tool_Screenshot
    {
        internal static string ScreenshotDir =>
            Path.Combine(Application.temporaryCachePath, "UnityBridge", "screenshots");

        #region Data Models

        public class ScreenshotResult
        {
            [Description("Absolute path to the saved PNG file. Use the Read tool to view the image.")]
            public string path = string.Empty;

            [Description("Image width in pixels.")]
            public int width;

            [Description("Image height in pixels.")]
            public int height;

            [Description("File size in bytes.")]
            public long fileSize;

            [Description("True if captured during Play Mode (Game view), false if Edit Mode (Scene view).")]
            public bool isPlayMode;
        }

        #endregion

        public static class Error
        {
            public static string NoSceneView()
                => "[Error] No active Scene view found. Open a Scene view (Window > General > Scene) before capturing in Edit Mode.";

            public static string CaptureReturnedNull()
                => "[Error] ScreenCapture.CaptureScreenshotAsTexture returned null.";

            public static string PngEncodingFailed()
                => "[Error] PNG encoding produced empty output.";

            public static string SceneViewRenderFailed()
                => "[Error] Failed to render Scene view camera.";
        }
    }
}
