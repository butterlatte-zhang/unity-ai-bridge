
#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityAiBridge.Serialization;
using UnityAiBridge.Logger;

namespace UnityAiBridge.Data
{
    public class SceneData : SceneDataShallow
    {
        public List<GameObjectData>? RootGameObjects { get; set; } = null;

        public SceneData() { }
        public SceneData(
            UnityEngine.SceneManagement.Scene scene,
            BridgeReflector reflector,
            bool includeRootGameObjects = false,
            int includeChildrenDepth = 0,
            bool includeBounds = false,
            bool includeData = false,
            IBridgeLogger? logger = null)
            : base(scene)
        {
            if (includeRootGameObjects)
            {
                this.RootGameObjects = scene.GetRootGameObjects()
                    .Select(go => go.ToGameObjectData(
                        reflector: reflector,
                        includeData: includeData,
                        includeComponents: false,
                        includeBounds: includeBounds,
                        includeHierarchy: includeChildrenDepth > 0,
                        hierarchyDepth: includeChildrenDepth,
                        logger: logger
                    ))
                    .ToList();
            }
        }
    }

    public static class SceneDataExtensions
    {
        public static SceneData ToSceneData(
            this UnityEngine.SceneManagement.Scene scene,
            BridgeReflector reflector,
            bool includeRootGameObjects = false,
            IBridgeLogger? logger = null)
        {
            return new SceneData(
                scene: scene,
                reflector: reflector,
                includeRootGameObjects: includeRootGameObjects,
                logger: logger);
        }
    }
}