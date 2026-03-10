
#nullable enable
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace UnityAiBridge.Utils
{
    public static partial class SceneUtils
    {
        public static Scene GetActiveScene() => SceneManager.GetActiveScene();

        public static IEnumerable<Scene> GetAllOpenedScenes()
        {
            var sceneCount = SceneManager.sceneCount;
            for (var i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                    yield return scene;
            }
        }
    }
}
