
#nullable enable
using System;
using System.ComponentModel;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityAiBridge.Editor.Utils;
using UnityAiBridge.Data;
using UnityAiBridge.Extensions;
using UnityAiBridge.Logger;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_GameObject
    {
        public const string GameObjectDestroyToolId = "gameobject-destroy";
        [BridgeTool
        (
            GameObjectDestroyToolId,
            Title = "GameObject / Destroy"
        )]
        [Description("Destroy GameObject and all nested GameObjects recursively in opened Prefab or in a Scene. " +
            "Use '" + GameObjectFindToolId + "' tool to find the target GameObject first.")]
        public DestroyGameObjectResult Destroy(GameObjectRef gameObjectRef)
        {
            if (gameObjectRef == null)
                throw new ArgumentNullException(nameof(gameObjectRef), "No GameObject reference provided.");

            if (!gameObjectRef.IsValid(out var gameObjectValidationError))
                throw new ArgumentException(gameObjectValidationError, nameof(gameObjectRef));

            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                var logger = BridgeLoggerFactory.CreateLogger<Tool_GameObject>();

                var go = gameObjectRef.FindGameObject(out var error);
                if (error != null)
                    throw new Exception(error);

                var destroyedName = go!.name;
                var destroyedPath = go.GetPath();
                var destroyedInstanceId = go.GetInstanceID();

                logger.LogInformation($"Destroying GameObject '{destroyedName}' (InstanceID: {destroyedInstanceId}) at path '{destroyedPath}'");

                UnityEngine.Object.DestroyImmediate(go);

                logger.LogInformation("Successfully destroyed GameObject '{destroyedName}' (InstanceID: {destroyedInstanceId})");

                EditorUtils.RepaintAllEditorWindows();

                return new DestroyGameObjectResult
                {
                    DestroyedName = destroyedName,
                    DestroyedPath = destroyedPath,
                    DestroyedInstanceId = destroyedInstanceId
                };
            });
        }

        public class DestroyGameObjectResult
        {
            [Description("Name of the destroyed GameObject.")]
            public string? DestroyedName { get; set; }

            [Description("Hierarchy path of the destroyed GameObject.")]
            public string? DestroyedPath { get; set; }

            [Description("Instance ID of the destroyed GameObject.")]
            public int DestroyedInstanceId { get; set; }
        }
    }
}
