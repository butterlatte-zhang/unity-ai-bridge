#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityEditor;
using UnityEngine;

namespace UnityAiBridge.Editor.Tools
{
    public static partial class Tool_Runtime
    {
        public const string RuntimeQueryToolId = "runtime-query";

        #region Data Models

        public class RuntimeQueryResult
        {
            [Description("The type name that was queried.")]
            public string typeName = string.Empty;

            [Description("Number of instances found in the scene.")]
            public int instanceCount;

            [Description("List of instances with their field/property values.")]
            public List<InstanceData> instances = new();
        }

        public class InstanceData
        {
            [Description("Name of the GameObject this component is attached to.")]
            public string gameObject = string.Empty;

            [Description("Dictionary of field/property names to their current values.")]
            public Dictionary<string, object?> fields = new();
        }

        #endregion

        [BridgeTool
        (
            RuntimeQueryToolId,
            Title = "Runtime / Query"
        )]
        [Description("Query runtime game state in Play Mode. " +
            "Find MonoBehaviours by type name and read their public fields and properties. " +
            "Useful for verifying game logic, checking object states, reading scores, " +
            "debugging component values, etc. " +
            "Works with any MonoBehaviour in the scene — no project-specific setup required.")]
        public static RuntimeQueryResult Query(
            [Description("Full or partial type name of the MonoBehaviour to find " +
                "(e.g. 'PlayerController', 'GameManager', 'MyNamespace.EnemyAI').")]
            string typeName,
            [Description("Specific field or property names to read (comma-separated). " +
                "If empty, reads all public instance fields and properties.")]
            string fields = "",
            [Description("If true, find all instances. If false, find first instance only.")]
            bool findAll = false,
            [Description("Maximum number of instances to return when findAll=true (default: 10).")]
            int maxResults = 10)
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException(Error.NotInPlayMode(RuntimeQueryToolId));

            return MainThread.Instance.Run(() =>
            {
                var type = FindType(typeName);
                if (type == null)
                    throw new ArgumentException(Error.TypeNotFound(typeName));

                // Ensure it's a Component type (MonoBehaviour or any Component subclass)
                if (!typeof(UnityEngine.Component).IsAssignableFrom(type))
                    throw new ArgumentException(
                        $"[Error] Type '{typeName}' is not a Component/MonoBehaviour. " +
                        "runtime-query only works with scene components. " +
                        "Use 'reflection-method-call' for static types.");

                // Find instances
                var allInstances = UnityEngine.Object.FindObjectsByType(
                    type, FindObjectsSortMode.None);

                if (allInstances.Length == 0)
                    throw new InvalidOperationException(Error.NoInstancesFound(typeName));

                var instances = findAll
                    ? allInstances.Take(maxResults).ToArray()
                    : new[] { allInstances[0] };

                // Parse requested fields
                var requestedFields = string.IsNullOrWhiteSpace(fields)
                    ? null
                    : fields.Split(',').Select(f => f.Trim()).Where(f => f.Length > 0).ToHashSet();

                // Build result
                var result = new RuntimeQueryResult
                {
                    typeName = type.FullName ?? type.Name,
                    instanceCount = allInstances.Length
                };

                foreach (var instance in instances)
                {
                    var component = instance as UnityEngine.Component;
                    if (component == null) continue;

                    var instanceData = new InstanceData
                    {
                        gameObject = component.gameObject.name
                    };

                    // Read public instance fields
                    var fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var fi in fieldInfos)
                    {
                        if (requestedFields != null && !requestedFields.Contains(fi.Name))
                            continue;

                        try
                        {
                            instanceData.fields[fi.Name] = SerializeValue(fi.GetValue(instance));
                        }
                        catch (Exception ex)
                        {
                            instanceData.fields[fi.Name] = $"<error: {ex.Message}>";
                        }
                    }

                    // Read public instance properties (with getters)
                    var propInfos = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var pi in propInfos)
                    {
                        if (!pi.CanRead) continue;
                        if (pi.GetIndexParameters().Length > 0) continue; // skip indexers
                        if (requestedFields != null && !requestedFields.Contains(pi.Name))
                            continue;

                        // Skip common Unity base properties that are noisy
                        if (requestedFields == null && IsNoiseProperty(pi.Name))
                            continue;

                        try
                        {
                            instanceData.fields[pi.Name] = SerializeValue(pi.GetValue(instance));
                        }
                        catch (Exception ex)
                        {
                            instanceData.fields[pi.Name] = $"<error: {ex.Message}>";
                        }
                    }

                    result.instances.Add(instanceData);
                }

                return result;
            });
        }

        /// <summary>
        /// Filter out noisy base-class properties that are rarely useful in queries.
        /// </summary>
        private static bool IsNoiseProperty(string name)
        {
            return name is
                "transform" or "gameObject" or "tag" or "name" or
                "hideFlags" or "runInEditMode" or "useGUILayout" or
                "destroyCancellationToken" or "isActiveAndEnabled" or "enabled";
        }
    }
}
