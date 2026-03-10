
#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityAiBridge;
using UnityAiBridge.Utils;

namespace UnityAiBridge.Editor.Tools
{
    [BridgeToolType]
    public partial class Tool_Scene
    {
        public static IEnumerable<UnityEngine.SceneManagement.Scene> OpenedScenes => SceneUtils.GetAllOpenedScenes();
        public static string OpenedScenesText
            => $"Opened Scenes:\n{string.Join("\n", SceneUtils.GetAllOpenedScenes().Select(scene => scene.name))}";

        public static class Error
        {
            static string ScenesPrinted => string.Join("\n", SceneUtils.GetAllOpenedScenes().Select(scene => scene.name));

            public static string SceneNameIsEmpty()
                => $"[Error] Scene name is empty. Available scenes:\n{ScenesPrinted}";
            public static string NotFoundSceneWithName(string? name)
                => $"[Error] Scene '{name ?? "null"}' not found. Available scenes:\n{ScenesPrinted}";
            public static string ScenePathIsEmpty()
                => "[Error] Scene path is empty. Please provide a valid path. Sample: \"Assets/Scenes/MyScene.unity\".";
            public static string FilePathMustEndsWithUnity()
                => "[Error] File path must end with '.unity'. Please provide a valid path. Sample: \"Assets/Scenes/MyScene.unity\".";
            public static string InvalidLoadSceneMode(int loadSceneMode)
                => $"[Error] Invalid load scene mode '{loadSceneMode}'. Valid values are 0 (Single) and 1 (Additive).";
        }
    }
}
