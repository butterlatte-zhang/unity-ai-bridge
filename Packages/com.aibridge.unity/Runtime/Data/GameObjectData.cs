
#nullable enable
using System.ComponentModel;
using System.Linq;
using UnityAiBridge.Serialization;
using UnityAiBridge.Utils;
using UnityEngine;
using UnityAiBridge.Logger;

namespace UnityAiBridge.Data
{
    public class GameObjectData
    {
        public GameObjectRef? Reference { get; set; }

        [Description("GameObject editable data (tag, layer, etc).")]
        public SerializedMember? Data { get; set; } = null;

        [Description("Bounds of the GameObject.")]
        public Bounds? Bounds { get; set; }

        [Description("Hierarchy metadata of the GameObject.")]
        public GameObjectMetadata? Hierarchy { get; set; } = null;

        [Description("Attached components shallow data of the GameObject (Read-only, use Component modification tool for modification).")]
        public ComponentDataShallow[]? Components { get; set; } = null;

        public GameObjectData() { }
        public GameObjectData(
            BridgeReflector reflector,
            GameObject go,
            bool includeData = false,
            bool includeComponents = false,
            bool includeBounds = false,
            bool includeHierarchy = false,
            int hierarchyDepth = 0,
            IBridgeLogger? logger = null)
        {
            Reference = new GameObjectRef(go);

            if (includeData)
            {
                Data = reflector.Serialize(
                    obj: go,
                    fallbackType: typeof(GameObject),
                    name: go.name,
                    recursive: true,
                    logger: logger);
            }

            if (includeComponents)
            {
                Components = go.GetComponents<UnityEngine.Component>()
                    .Select(c => new ComponentDataShallow(c))
                    .ToArray();
            }

            if (includeBounds)
                Bounds = go.CalculateBounds();

            if (includeHierarchy)
                Hierarchy = go.ToMetadata(hierarchyDepth);
        }
    }

    public static class GameObjectDataExtensions
    {
        public static GameObjectData ToGameObjectData(
            this GameObject go,
            BridgeReflector reflector,
            bool includeData = false,
            bool includeComponents = false,
            bool includeBounds = false,
            bool includeHierarchy = false,
            int hierarchyDepth = 0,
            IBridgeLogger? logger = null)
        {
            return new GameObjectData(
                reflector: reflector,
                go: go,
                includeData: includeData,
                includeComponents: includeComponents,
                includeBounds: includeBounds,
                includeHierarchy: includeHierarchy,
                hierarchyDepth: hierarchyDepth,
                logger: logger
            );
        }
    }
}