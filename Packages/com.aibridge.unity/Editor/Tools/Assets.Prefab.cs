
#nullable enable
using UnityAiBridge;
using UnityEditor;

namespace UnityAiBridge.Editor.Tools
{
    [BridgeToolType]
    public partial class Tool_Assets_Prefab
    {
        public static class Error
        {
            static string PrefabsPrinted => string.Join("\n", AssetDatabase.FindAssets("t:Prefab"));

            public static string PrefabPathIsEmpty()
                => "[Error] Prefab path is empty. Available prefabs:\n" + PrefabsPrinted;

            public static string NotFoundPrefabAtPath(string path)
                => $"[Error] Prefab '{path}' not found. Available prefabs:\n" + PrefabsPrinted;

            public static string PrefabPathIsInvalid(string path)
                => $"[Error] Prefab path '{path}' is invalid.";

            public static string PrefabStageIsNotOpened()
                => "[Error] Prefab stage is not opened. Use 'assets-prefab-open' to open it.";

            public static string PrefabStageIsAlreadyOpened()
                => "[Error] Prefab stage is already opened. Use 'assets-prefab-close' to close it.";
        }
    }
}
