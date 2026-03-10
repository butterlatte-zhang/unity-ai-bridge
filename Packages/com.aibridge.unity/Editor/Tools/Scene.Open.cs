
#nullable enable
using System.ComponentModel;
using System.Linq;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityAiBridge.Editor.Utils;
using UnityAiBridge.Data;
using UnityAiBridge.Extensions;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Scene
    {
        public const string SceneOpenToolId = "scene-open";
        [BridgeTool
        (
            SceneOpenToolId,
            Title = "Scene / Open"
        )]
        [Description("Open scene from the project asset file. " +
            "Use '" + Tool_Assets.AssetsFindToolId + "' tool to find the scene asset first.")]
        public SceneDataShallow[] Open
        (
            AssetObjectRef sceneRef,
            [Description("Open scene mode. " +
                "Single: closes the current scenes and opens a new one. " +
                "Additive: keeps the current scene and opens additional one.")]
            UnityEditor.SceneManagement.OpenSceneMode loadSceneMode = UnityEditor.SceneManagement.OpenSceneMode.Single
        )
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                var sceneAsset = sceneRef.FindAssetObject<UnityEditor.SceneAsset>()
                    ?? throw new System.ArgumentException($"Requested scene is not valid or not found.");

                var scenePath = sceneAsset.GetAssetPath();

                var sceneOpened = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, loadSceneMode);

                if (!sceneOpened.IsValid())
                    throw new System.Exception($"Failed to load scene at '{scenePath}'.\n{OpenedScenesText}");

                EditorUtils.RepaintAllEditorWindows();

                return OpenedScenes
                    .Select(scene => scene.ToSceneDataShallow())
                    .ToArray();
            });
        }
    }
}
