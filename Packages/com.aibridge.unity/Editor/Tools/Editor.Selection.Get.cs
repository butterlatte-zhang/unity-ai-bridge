
#nullable enable
using System.ComponentModel;
using System.Linq;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityAiBridge.Data;
using UnityEditor;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Editor_Selection
    {
        public const string EditorSelectionGetToolId = "editor-selection-get";
        [BridgeTool
        (
            EditorSelectionGetToolId,
            Title = "Editor / Selection / Get"
        )]
        [Description("Get information about the current Selection in the Unity Editor. " +
            "Use '" + EditorSelectionSetToolId + "' tool to set the selection.")]
        public SelectionData Get(
            bool includeGameObjects = false,
            bool includeTransforms = false,
            bool includeInstanceIDs = false,
            bool includeAssetGUIDs = false,
            bool includeActiveObject = true,
            bool includeActiveTransform = true)
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                var response = new SelectionData()
                {
                    ActiveGameObject = Selection.activeGameObject != null
                        ? new GameObjectRef(Selection.activeGameObject)
                        : null,
#if UNITY_6000_3_OR_NEWER
                    ActiveInstanceID = (int)Selection.activeEntityId
#else
                    ActiveInstanceID = Selection.activeInstanceID
#endif
                };

                if (includeGameObjects)
                    response.GameObjects = Selection.gameObjects?.Select(go => new GameObjectRef(go)).ToArray();

                if (includeTransforms)
                    response.Transforms = Selection.transforms?.Select(t => new ComponentRef(t)).ToArray();

                if (includeInstanceIDs)
#if UNITY_6000_3_OR_NEWER
                    response.InstanceIDs = Selection.entityIds.Select(x => (int)x).ToArray();
#else
                    response.InstanceIDs = Selection.instanceIDs;
#endif

                if (includeAssetGUIDs)
                    response.AssetGUIDs = Selection.assetGUIDs;

                if (includeActiveObject)
                    response.ActiveObject = Selection.activeObject != null
                        ? new ObjectRef(Selection.activeObject)
                        : null;

                if (includeActiveTransform)
                    response.ActiveTransform = Selection.activeTransform != null
                        ? new ComponentRef(Selection.activeTransform)
                        : null;

                return response;
            });
        }
    }
}
