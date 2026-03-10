
#nullable enable
using System;
using System.ComponentModel;
using UnityAiBridge;
using UnityAiBridge.Serialization;
using UnityAiBridge.Utils;
using UnityAiBridge.Data;
using UnityAiBridge.Extensions;
using UnityAiBridge.Logger;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_GameObject
    {
        public const string GameObjectModifyToolId = "gameobject-modify";
        [BridgeTool
        (
            GameObjectModifyToolId,
            Title = "GameObject / Modify"
        )]
        [Description("Modify GameObject fields and properties in opened Prefab or in a Scene. " +
            "You can modify multiple GameObjects at once. Just provide the same number of GameObject references and SerializedMember objects.")]
        public Logs? Modify
        (
            GameObjectRefList gameObjectRefs,
            [Description("Each item in the array represents a GameObject modification of the 'gameObjectRefs' at the same index. " +
                "Usually a GameObject is a container for components. Each component may have fields and properties for modification. " +
                "If you need to modify components of a GameObject, please use '" + GameObjectComponentModifyToolId + "' tool. " +
                "Ignore values that should not be modified. " +
                "Any unknown or wrong located fields and properties will be ignored. " +
                "Check the result of this command to see what was changed. The ignored fields and properties will be listed.")]
            SerializedMemberList gameObjectDiffs
        )
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                if (gameObjectRefs.Count == 0)
                    throw new Exception("No GameObject references provided. Please provide at least one GameObject reference.");

                if (gameObjectDiffs.Count != gameObjectRefs.Count)
                    throw new Exception($"The number of {nameof(gameObjectDiffs)} and {nameof(gameObjectRefs)} should be the same. " +
                        $"{nameof(gameObjectDiffs)}: {gameObjectDiffs.Count}, {nameof(gameObjectRefs)}: {gameObjectRefs.Count}");

                var logs = new Logs();

                for (int i = 0; i < gameObjectRefs.Count; i++)
                {
                    var go = gameObjectRefs[i].FindGameObject(out var error);
                    if (error != null)
                    {
                        logs.Error(error);
                        continue;
                    }
                    if (go == null)
                    {
                        logs.Error($"GameObject by {nameof(gameObjectRefs)}[{i}] not found.");
                        continue;
                    }

                    var objToModify = (object)go;

                    var modified = BridgePlugin.Reflector.TryPopulate(
                        ref objToModify,
                        data: gameObjectDiffs[i],
                        logs: logs,
                        logger: BridgeLoggerFactory.CreateLogger<Tool_GameObject>());

                    if (modified)
                        UnityEditor.EditorUtility.SetDirty(go);
                }

                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

                if (logs.Count == 0)
                    logs.Warning("No modifications were made.");

                return logs;
            });
        }
    }
}
