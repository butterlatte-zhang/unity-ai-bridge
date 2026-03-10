
#nullable enable
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityAiBridge.Data;
using UnityAiBridge.Logger;
using UnityEditor;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Scene
    {
        public const string SceneUnloadToolId = "scene-unload";
        [BridgeTool
        (
            SceneUnloadToolId,
            Title = "Scene / Unload"
        )]
        [Description("Unload scene from the Opened scenes in Unity Editor. " +
            "Use '" + SceneListOpenedToolId + "' tool to get the list of all opened scenes.")]
        public Task<UnloadSceneResult> Unload
        (
            [Description("Name of the loaded scene.")]
            string name
        )
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(async () =>
            {
                var logger = BridgeLoggerFactory.CreateLogger<Tool_Scene>();

                if (string.IsNullOrEmpty(name))
                    throw new ArgumentException(Error.SceneNameIsEmpty(), nameof(name));

                var scene = SceneUtils.GetAllOpenedScenes()
                    .FirstOrDefault(scene => scene.name == name);

                if (!scene.IsValid())
                    throw new ArgumentException(Error.NotFoundSceneWithName(name), nameof(name));

                var scenePath = scene.path;
                logger.LogInformation($"Unloading scene '{name}' at path '{scenePath}'");

                var asyncOperation = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);

                while (!asyncOperation.isDone)
                    await Task.Yield();

                logger.LogInformation($"Successfully unloaded scene '{name}'");

                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);

                return new UnloadSceneResult
                {
                    Name = name,
                    AssetObjectRef = sceneAsset == null
                        ? null
                        : new AssetObjectRef(sceneAsset)
                };
            });
        }

        public class UnloadSceneResult
        {
            [Description("Name of the unloaded scene.")]
            public string? Name { get; set; }
            [Description("Reference to the unloaded scene asset.")]
            public AssetObjectRef? AssetObjectRef { get; set; } = null!;
        }
    }
}
