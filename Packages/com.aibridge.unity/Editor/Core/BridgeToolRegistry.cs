#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using UnityEngine;

namespace UnityAiBridge.Editor
{
    /// <summary>
    /// Registry of all Bridge tools. Scans assemblies for [BridgeToolType] classes
    /// and [BridgeTool] methods.
    /// </summary>
    public class BridgeToolRegistry
    {
        private readonly Dictionary<string, ToolEntry> _tools = new();
        private readonly Dictionary<Type, object> _toolInstances = new();

        public BridgeToolRegistry()
        {
            ScanAssemblies();
        }

        #region Public API

        public IEnumerable<ToolEntry> GetAllTools() => _tools.Values;

        public ToolEntry? GetTool(string name)
        {
            _tools.TryGetValue(name, out var entry);
            return entry;
        }

        public int ToolCount => _tools.Count;

        #endregion

        #region Assembly Scanning

        private void ScanAssemblies()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                var assemblyName = assembly.GetName().Name ?? "";

                // Skip known irrelevant assemblies
                if (assemblyName.StartsWith("mscorlib") ||
                    assemblyName.StartsWith("System") ||
                    assemblyName.StartsWith("Unity.") ||
                    assemblyName.StartsWith("UnityEngine") ||
                    assemblyName.StartsWith("UnityEditor") ||
                    assemblyName.StartsWith("Microsoft") ||
                    assemblyName.StartsWith("Mono") ||
                    assemblyName.StartsWith("netstandard") ||
                    assemblyName.StartsWith("nunit"))
                    continue;

                try
                {
                    ScanAssembly(assembly);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[BridgeToolRegistry] Failed to scan assembly {assemblyName}: {e.Message}");
                }
            }

            stopwatch.Stop();
            Debug.Log($"[BridgeToolRegistry] Discovered {_tools.Count} tools in {stopwatch.ElapsedMilliseconds}ms");
        }

        private void ScanAssembly(Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray()!;
            }

            foreach (var type in types)
            {
                if (type.GetCustomAttribute<BridgeToolTypeAttribute>() == null)
                    continue;

                ScanToolType(type);
            }
        }

        private void ScanToolType(Type type)
        {
            // Scan both instance and static methods to support static tool classes (e.g. Tool_Script, Tool_Tests)
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                var toolAttr = method.GetCustomAttribute<BridgeToolAttribute>();
                if (toolAttr == null)
                    continue;

                var descAttr = method.GetCustomAttribute<DescriptionAttribute>();
                var parameters = BuildParameterList(method);
                var inputSchema = BuildInputSchema(method);

                var entry = new ToolEntry
                {
                    Name = toolAttr.Name,
                    Title = toolAttr.Title ?? "",
                    Description = descAttr?.Description ?? "",
                    Method = method,
                    DeclaringType = type,
                    IsStatic = method.IsStatic,
                    Parameters = parameters,
                    InputSchema = inputSchema
                };

                if (_tools.ContainsKey(entry.Name))
                {
                    Debug.LogWarning($"[BridgeToolRegistry] Duplicate tool name: {entry.Name}");
                    continue;
                }

                _tools[entry.Name] = entry;
            }
        }

        #endregion

        #region Tool Instance Management

        internal object GetOrCreateInstance(Type type)
        {
            if (!_toolInstances.TryGetValue(type, out var instance))
            {
                instance = Activator.CreateInstance(type)!;
                _toolInstances[type] = instance;
            }
            return instance;
        }

        #endregion

        #region Schema Generation

        private static List<ToolParameter> BuildParameterList(MethodInfo method)
        {
            return method.GetParameters().Select(p =>
            {
                var desc = p.GetCustomAttribute<DescriptionAttribute>();
                return new ToolParameter
                {
                    Name = p.Name ?? "",
                    Type = GetJsonTypeName(p.ParameterType),
                    Description = desc?.Description ?? "",
                    HasDefault = p.HasDefaultValue,
                    DefaultValue = p.HasDefaultValue ? p.DefaultValue : null,
                    ParameterType = p.ParameterType,
                    IsRequired = !p.HasDefaultValue
                };
            }).ToList();
        }

        private static JsonNode? BuildInputSchema(MethodInfo method)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 0)
                return null;

            var properties = new JsonObject();
            var required = new JsonArray();

            foreach (var param in parameters)
            {
                var desc = param.GetCustomAttribute<DescriptionAttribute>();
                var propObj = new JsonObject
                {
                    ["type"] = GetJsonTypeName(param.ParameterType)
                };

                if (desc != null)
                    propObj["description"] = desc.Description;

                if (param.HasDefaultValue && param.DefaultValue != null)
                {
                    try
                    {
                        var defaultJson = JsonSerializer.Serialize(param.DefaultValue);
                        propObj["default"] = JsonNode.Parse(defaultJson);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[BridgeToolRegistry] Failed to serialize default value for param '{param.Name}': {e.Message}");
                    }
                }

                if (param.ParameterType.IsEnum)
                {
                    var enumValues = new JsonArray();
                    foreach (var name in Enum.GetNames(param.ParameterType))
                        enumValues.Add(name);
                    propObj["enum"] = enumValues;
                }

                properties[param.Name!] = propObj;

                if (!param.HasDefaultValue)
                    required.Add(param.Name!);
            }

            var schema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = properties
            };

            if (required.Count > 0)
                schema["required"] = required;

            return schema;
        }

        private static string GetJsonTypeName(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying == typeof(string)) return "string";
            if (underlying == typeof(bool)) return "boolean";
            if (underlying == typeof(int) || underlying == typeof(long) ||
                underlying == typeof(short) || underlying == typeof(byte))
                return "integer";
            if (underlying == typeof(float) || underlying == typeof(double) || underlying == typeof(decimal))
                return "number";
            if (underlying.IsArray || (underlying.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(underlying.GetGenericTypeDefinition())))
                return "array";
            if (underlying.IsEnum)
                return "string";

            return "object";
        }

        #endregion
    }

    #region Data Models

    public class ToolEntry
    {
        public string Name { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public MethodInfo Method { get; set; } = null!;
        public Type DeclaringType { get; set; } = null!;
        public bool IsStatic { get; set; }
        public List<ToolParameter> Parameters { get; set; } = new();
        public JsonNode? InputSchema { get; set; }
    }

    public class ToolParameter
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Description { get; set; } = "";
        public bool HasDefault { get; set; }
        public object? DefaultValue { get; set; }
        public Type ParameterType { get; set; } = typeof(object);
        public bool IsRequired { get; set; }
    }

    #endregion
}
