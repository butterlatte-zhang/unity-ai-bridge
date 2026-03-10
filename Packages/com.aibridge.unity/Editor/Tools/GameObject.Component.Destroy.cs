
#nullable enable
using System;
using System.ComponentModel;
using System.Linq;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityAiBridge.Data;
using UnityAiBridge.Extensions;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_GameObject
    {
        public const string GameObjectComponentDestroyToolId = "gameobject-component-destroy";
        [BridgeTool
        (
            GameObjectComponentDestroyToolId,
            Title = "GameObject / Component / Destroy"
        )]
        [Description("Destroy one or many components from target GameObject. Can't destroy missed components. " +
            "Use '" + GameObjectFindToolId + "' tool to find the target GameObject and '" + GameObjectComponentGetToolId + "' to get component details first.")]
        public DestroyComponentsResponse DestroyComponents
        (
            GameObjectRef gameObjectRef,
            ComponentRefList destroyComponentRefs
        )
        {
            if (gameObjectRef == null)
                throw new ArgumentNullException(nameof(gameObjectRef));

            if (!gameObjectRef.IsValid(out var gameObjectValidationError))
                throw new ArgumentException(gameObjectValidationError, nameof(gameObjectRef));

            if (destroyComponentRefs == null)
                throw new ArgumentNullException(nameof(destroyComponentRefs));

            if (destroyComponentRefs.Count == 0)
                throw new ArgumentException("No components provided to destroy.", nameof(destroyComponentRefs));

            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                var go = gameObjectRef.FindGameObject(out var error);
                if (error != null)
                    throw new Exception(error);

                if (go == null)
                    throw new Exception($"GameObject by {nameof(gameObjectRef)} not found.");

                var destroyCounter = 0;

                var allComponents = go.GetComponents<UnityEngine.Component>();

                var response = new DestroyComponentsResponse();

                foreach (var component in allComponents)
                {
                    if (component == null)
                        continue; // Skip null/missing script components

                    if (destroyComponentRefs.Any(cr => cr.Matches(component)))
                    {
                        var destroyedComponentRef = new ComponentRef(component);
                        UnityEngine.Object.DestroyImmediate(component);
                        destroyCounter++;
                        response.DestroyedComponents ??= new ComponentRefList();
                        response.DestroyedComponents.Add(destroyedComponentRef);
                    }
                }

                if (destroyCounter == 0)
                    throw new Exception(Error.NotFoundComponents(destroyComponentRefs, allComponents));

                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

                return response;
            });
        }

        public class DestroyComponentsResponse
        {
            [Description("List of destroyed components.")]
            public ComponentRefList? DestroyedComponents { get; set; }
        }
    }
}
