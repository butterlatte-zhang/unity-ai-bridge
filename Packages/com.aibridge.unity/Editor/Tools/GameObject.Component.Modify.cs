
#nullable enable
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using UnityAiBridge;
using UnityAiBridge.Serialization;
using UnityAiBridge.Utils;
using UnityAiBridge.Data;
using UnityAiBridge.Extensions;
using UnityAiBridge.Logger;
using UnityEngine;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_GameObject
    {
        public const string GameObjectComponentModifyToolId = "gameobject-component-modify";
        [BridgeTool
        (
            GameObjectComponentModifyToolId,
            Title = "GameObject / Component / Modify"
        )]
        [Description("Modify a specific Component on a GameObject in opened Prefab or in a Scene. " +
            "Allows direct modification of component fields and properties without wrapping in GameObject structure. " +
            "Use '" + GameObjectComponentGetToolId + "' first to inspect the component structure before modifying.")]
        public ModifyComponentResponse ModifyComponent
        (
            GameObjectRef gameObjectRef,
            ComponentRef componentRef,
            [Description("The component data to apply. Should contain '" + nameof(SerializedMember.fields) + "' and/or '" + nameof(SerializedMember.props) + "' with the values to modify.\n" +
                "Only include the fields/properties you want to change.\n" +
                "Any unknown or invalid fields and properties will be reported in the response.")]
            SerializedMember componentDiff
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

                var response = new ModifyComponentResponse
                {
                    Reference = new ComponentRef(targetComponent),
                    Index = targetIndex
                };

                var logs = new Logs();
                bool success;

                // Transform 的 position/rotation/localScale 等属性是 native 属性，
                // 通过反射 PropertyInfo.SetValue 修改不生效，需要直接调用属性 setter。
                if (targetComponent is Transform transform)
                {
                    success = TryApplyTransformDiff(transform, componentDiff, logs);
                }
                else
                {
                    var objToModify = (object)targetComponent;
                    success = BridgePlugin.Reflector.TryPopulate(
                        ref objToModify,
                        data: componentDiff,
                        logs: logs,
                        logger: BridgeLoggerFactory.CreateLogger<Tool_GameObject>());
                }

                if (success)
                {
                    UnityEditor.EditorUtility.SetDirty(go);
                    UnityEditor.EditorUtility.SetDirty(targetComponent);
                    response.Success = true;
                }

                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

                response.Logs = logs
                    .Select(log => log.ToString())
                    .ToArray();

                // Return updated component data
                response.Component = new ComponentDataShallow(targetComponent);

                return response;
            });
        }

        #region Transform 特殊处理

        /// <summary>
        /// Transform 的 position/rotation/localScale 等是 native 属性，
        /// 反射的 PropertyInfo.SetValue 不能可靠地写入。
        /// 这里用直接属性赋值绕过反射限制。
        /// </summary>
        private static bool TryApplyTransformDiff(Transform transform, SerializedMember diff, Logs logs)
        {
            if (diff.props == null || diff.props.Count == 0)
            {
                logs?.Warning("No properties to modify on Transform.");
                return false;
            }

            var jsonOpts = BridgePlugin.Reflector.JsonSerializerOptions;
            bool anyModified = false;

            UnityEditor.Undo.RecordObject(transform, "Bridge: Modify Transform");

            foreach (var prop in diff.props)
            {
                if (string.IsNullOrEmpty(prop.name) || !prop.value.HasValue)
                    continue;

                try
                {
                    var json = prop.value.Value.GetRawText();
                    switch (prop.name)
                    {
                        case "position":
                            transform.position = JsonSerializer.Deserialize<Vector3>(json, jsonOpts);
                            break;
                        case "localPosition":
                            transform.localPosition = JsonSerializer.Deserialize<Vector3>(json, jsonOpts);
                            break;
                        case "rotation":
                            transform.rotation = JsonSerializer.Deserialize<Quaternion>(json, jsonOpts);
                            break;
                        case "localRotation":
                            transform.localRotation = JsonSerializer.Deserialize<Quaternion>(json, jsonOpts);
                            break;
                        case "localScale":
                            transform.localScale = JsonSerializer.Deserialize<Vector3>(json, jsonOpts);
                            break;
                        case "eulerAngles":
                            transform.eulerAngles = JsonSerializer.Deserialize<Vector3>(json, jsonOpts);
                            break;
                        case "localEulerAngles":
                            transform.localEulerAngles = JsonSerializer.Deserialize<Vector3>(json, jsonOpts);
                            break;
                        default:
                            logs?.Warning($"Unknown Transform property: '{prop.name}'. Supported: position, localPosition, rotation, localRotation, localScale, eulerAngles, localEulerAngles.");
                            continue;
                    }
                    logs?.Add($"Modified Transform.{prop.name}");
                    anyModified = true;
                }
                catch (Exception e)
                {
                    logs?.Error($"Failed to set Transform.{prop.name}: {e.Message}");
                }
            }

            return anyModified;
        }

        #endregion

        public class ModifyComponentResponse
        {
            [Description("Whether the modification was successful.")]
            public bool Success { get; set; } = false;

            [Description("Reference to the modified component.")]
            public ComponentRef? Reference { get; set; }

            [Description("Index of the component in the GameObject's component list.")]
            public int Index { get; set; }

            [Description("Updated component information after modification.")]
            public ComponentDataShallow? Component { get; set; }
            [Description("Log of modifications made and any warnings/errors encountered.")]
            public string[]? Logs { get; set; }
        }
    }
}
