#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityAiBridge;

namespace UnityAiBridge.Editor.Tools
{
    [BridgeToolType]
    public static partial class Tool_Runtime
    {
        public static class Error
        {
            public static string NotInPlayMode(string toolName)
                => $"[Error] '{toolName}' requires Play Mode. " +
                   "Enter Play Mode first via editor-application-set-state (isPlaying=true).";

            public static string TypeNotFound(string typeName)
                => $"[Error] Could not find type '{typeName}' in any loaded assembly. " +
                   "Check the type name spelling. Use partial names (e.g. 'PlayerController') or " +
                   "fully-qualified names (e.g. 'MyGame.PlayerController').";

            public static string MethodNotFound(string typeName, string methodName)
                => $"[Error] No public static method '{methodName}' found on type '{typeName}'.";

            public static string NoInstancesFound(string typeName)
                => $"[Error] No instances of '{typeName}' found in the scene. " +
                   "Ensure the component is attached to an active GameObject.";
        }

        #region Reflection Helpers

        /// <summary>
        /// Find a Type by full or partial name across all loaded assemblies.
        /// Partial names match the simple type name (class name without namespace).
        /// </summary>
        internal static Type? FindType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            // Try exact full name first
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = assembly.GetType(typeName, throwOnError: false);
                    if (t != null) return t;
                }
                catch { /* skip problematic assemblies */ }
            }

            // Try partial match on simple type name
            Type? bestMatch = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in assembly.GetExportedTypes())
                    {
                        if (t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
                        {
                            // Prefer exact case match
                            if (t.Name == typeName)
                                return t;
                            bestMatch ??= t;
                        }
                    }
                }
                catch { /* skip assemblies that throw on GetExportedTypes */ }
            }

            return bestMatch;
        }

        /// <summary>
        /// Serialize a value to a JSON-friendly object, handling Unity types specially.
        /// </summary>
        internal static object? SerializeValue(object? value, int depth = 0)
        {
            if (value == null) return null;
            if (depth > 3) return value.ToString();

            var type = value.GetType();

            // Primitives and strings
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
                return value;

            // Enums
            if (type.IsEnum)
                return value.ToString();

            // Unity Vector types
            if (value is UnityEngine.Vector2 v2)
                return new { x = v2.x, y = v2.y };
            if (value is UnityEngine.Vector3 v3)
                return new { x = v3.x, y = v3.y, z = v3.z };
            if (value is UnityEngine.Vector4 v4)
                return new { x = v4.x, y = v4.y, z = v4.z, w = v4.w };
            if (value is UnityEngine.Quaternion q)
                return new { x = q.x, y = q.y, z = q.z, w = q.w };
            if (value is UnityEngine.Color c)
                return new { r = c.r, g = c.g, b = c.b, a = c.a };
            if (value is UnityEngine.Bounds b)
                return new { center = SerializeValue(b.center, depth + 1), size = SerializeValue(b.size, depth + 1) };

            // Unity Object — just return name + type to avoid deep serialization
            if (value is UnityEngine.Object unityObj)
                return unityObj != null ? $"{unityObj.name} ({unityObj.GetType().Name})" : null;

            // Collections
            if (value is System.Collections.IList list)
            {
                var result = new List<object?>();
                int limit = Math.Min(list.Count, 20);
                for (int i = 0; i < limit; i++)
                    result.Add(SerializeValue(list[i], depth + 1));
                if (list.Count > 20)
                    result.Add($"... ({list.Count - 20} more)");
                return result;
            }

            // Dictionaries
            if (value is System.Collections.IDictionary dict)
            {
                var result = new Dictionary<string, object?>();
                int count = 0;
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    if (count++ >= 20) break;
                    result[entry.Key.ToString() ?? "null"] = SerializeValue(entry.Value, depth + 1);
                }
                return result;
            }

            // Fallback: toString
            return value.ToString();
        }

        #endregion
    }
}
