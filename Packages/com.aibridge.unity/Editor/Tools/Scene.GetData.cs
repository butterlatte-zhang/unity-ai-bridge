
#nullable enable
using System;
using System.ComponentModel;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityAiBridge.Data;
using UnityAiBridge.Logger;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Scene
    {
        public const string SceneGetDataToolId = "scene-get-data";
        [BridgeTool
        (
            SceneGetDataToolId,
            Title = "Scene / Get Data"
        )]
        [Description("This tool retrieves the list of root GameObjects in the specified scene. " +
            "Use '" + SceneListOpenedToolId + "' tool to get the list of all opened scenes.")]
        public SceneData GetData
        (
            [Description("Name of the opened scene. If empty or null, the active scene will be used.")]
            string? openedSceneName = null,
            [Description("If true, includes root GameObjects in the scene data.")]
            bool includeRootGameObjects = false,
            [Description("Determines the depth of the hierarchy to include.")]
            int includeChildrenDepth = 3,
            [Description("If true, includes bounding box information for GameObjects.")]
            bool includeBounds = false,
            [Description("If true, includes component data for GameObjects.")]
            bool includeData = false
        )
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                var scene = string.IsNullOrEmpty(openedSceneName)
                    ? UnityEngine.SceneManagement.SceneManager.GetActiveScene()
                    : UnityEngine.SceneManagement.SceneManager.GetSceneByName(openedSceneName);

                if (!scene.IsValid())
                    throw new ArgumentException(Error.NotFoundSceneWithName(openedSceneName));

                return new SceneData(
                    scene: scene,
                    reflector: BridgePlugin.Reflector,
                    includeRootGameObjects: includeRootGameObjects,
                    includeChildrenDepth: includeChildrenDepth,
                    includeBounds: includeBounds,
                    includeData: includeData,
                    logger: BridgeLoggerFactory.CreateLogger<Tool_Scene>()
                );
            });
        }
    }
}
