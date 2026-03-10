
#nullable enable
using System.ComponentModel;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityAiBridge.Data;
using UnityAiBridge.Extensions;
using UnityAiBridge.Logger;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_GameObject
    {
        public const string GameObjectFindToolId = "gameobject-find";
        [BridgeTool
        (
            GameObjectFindToolId,
            Title = "GameObject / Find"
        )]
        [Description("Finds specific GameObject by provided information in opened Prefab or in a Scene. " +
            "First it looks for the opened Prefab, if any Prefab is opened it looks only there ignoring a scene. " +
            "If no opened Prefab it looks into current active scene. " +
            "Returns GameObject information and its children. " +
            "Also, it returns Components preview just for the target GameObject.")]
        public GameObjectData? Find
        (
            GameObjectRef gameObjectRef,
            [Description("Include editable GameObject data (tag, layer, etc).")]
            bool includeData = false,
            [Description("Include attached components references.")]
            bool includeComponents = false,
            [Description("Include 3D bounds of the GameObject.")]
            bool includeBounds = false,
            [Description("Include hierarchy metadata.")]
            bool includeHierarchy = false,
            [Description("Determines the depth of the hierarchy to include. 0 - means only the target GameObject. 1 - means to include one layer below.")]
            int hierarchyDepth = 0
        )
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                var go = gameObjectRef.FindGameObject(out var error);
                if (error != null)
                    throw new System.Exception(error);

                if (go == null)
                    return null;

                return go.ToGameObjectData(
                    reflector: BridgePlugin.Reflector,
                    includeData: includeData,
                    includeComponents: includeComponents,
                    includeBounds: includeBounds,
                    includeHierarchy: includeHierarchy,
                    hierarchyDepth: hierarchyDepth,
                    logger: BridgeLoggerFactory.CreateLogger<Tool_GameObject>()
                );
            });
        }
    }
}
