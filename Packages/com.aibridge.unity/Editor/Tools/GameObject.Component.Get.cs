
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
        public const string GameObjectComponentGetToolId = "gameobject-component-get";
        [BridgeTool
        (
            GameObjectComponentGetToolId,
            Title = "GameObject / Component / Get"
        )]
        [Description("Get detailed information about a specific Component on a GameObject. " +
        "Returns component type, enabled state, and optionally serialized fields and properties. " +
        "Use this to inspect component data before modifying it. " +
        "Use '" + GameObjectFindToolId + "' tool to get the list of all components on the GameObject.")]
        public GetComponentResponse GetComponent
        (
            GameObjectRef gameObjectRef,
            ComponentRef componentRef,
            [Description("Include serialized fields of the component.")]
            bool includeFields = true,
            [Description("Include serialized properties of the component.")]
            bool includeProperties = true,
            [Description("Performs deep serialization including all nested objects. Otherwise, only serializes top-level members.")]
            bool deepSerialization = false
        )
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                if (!gameObjectRef.IsValid(out var gameObjectValidationError))
                    throw new ArgumentException(gameObjectValidationError, nameof(gameObjectRef));

                if (!componentRef.IsValid(out var componentValidationError))
                    throw new ArgumentException(componentValidationError, nameof(componentRef));

                var go = gameObjectRef.FindGameObject(out var error);
                if (error != null)
                    throw new Exception(error);

                if (go == null)
                    throw new Exception("GameObject not found.");

                var allComponents = go.GetComponents<UnityEngine.Component>();
                UnityEngine.Component? targetComponent = null;
                int targetIndex = -1;

                for (int i = 0; i < allComponents.Length; i++)
                {
                    if (componentRef.Matches(allComponents[i], i))
                    {
                        targetComponent = allComponents[i];
                        targetIndex = i;
                        break;
                    }
                }

                if (targetComponent == null)
                    throw new Exception(Error.NotFoundComponent(componentRef.InstanceID, allComponents));

                var response = new GetComponentResponse
                {
                    Reference = new ComponentRef(targetComponent),
                    Index = targetIndex,
                    Component = new ComponentDataShallow(targetComponent)
                };

                var reflector = BridgePlugin.Reflector;
                var logger = BridgeLoggerFactory.CreateLogger<Tool_GameObject>();

                if (includeFields || includeProperties)
                {
                    var serialized = reflector.Serialize(
                        obj: targetComponent,
                        name: targetComponent.GetType().GetTypeId(),
                        recursive: deepSerialization,
                        logger: logger
                    );

                    if (includeFields && serialized?.fields != null)
                    {
                        response.Fields = serialized.fields
                            .Where(f => f != null)
                            .ToList();
                    }

                    if (includeProperties && serialized?.props != null)
                    {
                        response.Properties = serialized.props
                            .Where(p => p != null)
                            .ToList();
                    }
                }

                return response;
            });
        }

        public class GetComponentResponse
        {
            [Description("Reference to the component for future operations.")]
            public ComponentRef? Reference { get; set; }

            [Description("Index of the component in the GameObject's component list.")]
            public int Index { get; set; }

            [Description("Basic component information (type, enabled state).")]
            public ComponentDataShallow? Component { get; set; }

            [Description("Serialized fields of the component.")]
            public List<SerializedMember>? Fields { get; set; }

            [Description("Serialized properties of the component.")]
            public List<SerializedMember>? Properties { get; set; }
        }
    }
}
