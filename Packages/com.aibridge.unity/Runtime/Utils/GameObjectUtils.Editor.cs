
#nullable enable
#if UNITY_EDITOR
using UnityAiBridge.Extensions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityAiBridge.Utils
{
    public static partial class GameObjectUtils
    {
        /// <summary>
        /// Find Root GameObject in opened Prefab. Of array of GameObjects in a scene.
        /// </summary>
        /// <param name="scene">Scene for the search, if null the current active scene would be used</param>
        /// <returns>Array of root GameObjects</returns>
        public static GameObject[] FindRootGameObjects(Scene? scene = null)
        {
            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
                return prefabStage.prefabContentsRoot.MakeArray();

            if (scene == null)
            {
                var rootGos = UnityEditor.SceneManagement.EditorSceneManager
                    .GetActiveScene()
                    .GetRootGameObjects();

                return rootGos;
            }
            else
            {
                return scene.Value.GetRootGameObjects();
            }
        }
        public static GameObject? FindByInstanceID(int instanceID)
        {
            if (instanceID == 0)
                return null;

#if UNITY_6000_3_OR_NEWER
            var obj = UnityEditor.EditorUtility.EntityIdToObject((UnityEngine.EntityId)instanceID);
#else
            var obj = UnityEditor.EditorUtility.InstanceIDToObject(instanceID);
#endif
            if (obj is not GameObject go)
                return null;

            return go;
        }
    }
}
#endif
