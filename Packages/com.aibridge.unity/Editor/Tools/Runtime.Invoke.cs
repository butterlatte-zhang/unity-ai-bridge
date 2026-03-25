#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityEditor;

namespace UnityAiBridge.Editor.Tools
{
    public static partial class Tool_Runtime
    {
        public const string RuntimeInvokeToolId = "runtime-invoke";

        #region Data Models

        public class RuntimeInvokeResult
        {
            [Description("The full type name of the class.")]
            public string typeName = string.Empty;

            [Description("The method that was invoked.")]
            public string methodName = string.Empty;

            [Description("The return value of the method (null for void methods).")]
            public object? returnValue;

            [Description("True if the method was invoked successfully.")]
            public bool success;
        }

        #endregion

        [BridgeTool
        (
            RuntimeInvokeToolId,
            Title = "Runtime / Invoke"
        )]
        [Description("Invoke a public static method on any class in Play Mode. " +
            "Useful for triggering game actions, changing state, calling test helpers, " +
            "or executing debug commands. The method must be public and static. " +
            "For instance methods on MonoBehaviours, use 'reflection-method-call' instead.")]
        public static RuntimeInvokeResult Invoke(
            [Description("Full type name (e.g. 'MyNamespace.GameManager') or simple name (e.g. 'GameManager'). " +
                "Searches all loaded assemblies.")]
            string typeName,
            [Description("Name of the public static method to invoke.")]
            string methodName,
            [Description("Arguments as JSON array (e.g. '[\"hello\", 42, true]'). " +
                "Leave empty or '[]' for no-arg methods. " +
                "Supported types: string, int, float, bool, null.")]
            string arguments = "[]")
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException(Error.NotInPlayMode(RuntimeInvokeToolId));

            return MainThread.Instance.Run(() =>
            {
                var type = FindType(typeName);
                if (type == null)
                    throw new ArgumentException(Error.TypeNotFound(typeName));

                // Parse arguments
                object?[] args;
                try
                {
                    args = ParseArguments(arguments);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"[Error] Failed to parse arguments: {ex.Message}");
                }

                // Find matching static method
                var candidates = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (candidates.Length == 0)
                    throw new MissingMethodException(Error.MethodNotFound(typeName, methodName));

                // Pick best overload
                var method = FindBestOverload(candidates, args);
                if (method == null)
                    throw new MissingMethodException(
                        $"[Error] No overload of '{type.Name}.{methodName}' is compatible with " +
                        $"{args.Length} argument(s). Available overloads:\n" +
                        string.Join("\n", candidates.Select(m =>
                            $"  {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})")));

                // Build final argument array with default padding
                var parameters = method.GetParameters();
                var finalArgs = new object?[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i < args.Length)
                        finalArgs[i] = ConvertArgument(args[i], parameters[i].ParameterType);
                    else
                        finalArgs[i] = parameters[i].DefaultValue;
                }

                // Invoke
                try
                {
                    var returnVal = method.Invoke(null, finalArgs);
                    return new RuntimeInvokeResult
                    {
                        typeName = type.FullName ?? type.Name,
                        methodName = method.Name,
                        returnValue = SerializeValue(returnVal),
                        success = true
                    };
                }
                catch (TargetInvocationException ex)
                {
                    var inner = ex.InnerException ?? ex;
                    return new RuntimeInvokeResult
                    {
                        typeName = type.FullName ?? type.Name,
                        methodName = method.Name,
                        returnValue = $"Exception: {inner.GetType().Name}: {inner.Message}",
                        success = false
                    };
                }
            });
        }

        #region Argument Parsing

        private static object?[] ParseArguments(string jsonArray)
        {
            if (string.IsNullOrWhiteSpace(jsonArray) || jsonArray == "[]")
                return Array.Empty<object?>();

            using var doc = JsonDocument.Parse(jsonArray);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("Arguments must be a JSON array.");

            var result = new List<object?>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                result.Add(element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Number => element.TryGetInt64(out long l) ? (object)l : element.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => element.GetRawText()
                });
            }
            return result.ToArray();
        }

        private static MethodInfo? FindBestOverload(MethodInfo[] candidates, object?[] args)
        {
            // Exact match first
            foreach (var candidate in candidates)
            {
                var parameters = candidate.GetParameters();
                if (parameters.Length == args.Length)
                    return candidate;
            }

            // Match with defaults
            foreach (var candidate in candidates)
            {
                var parameters = candidate.GetParameters();
                if (parameters.Length < args.Length) continue;

                bool allTrailingHaveDefaults = true;
                for (int i = args.Length; i < parameters.Length; i++)
                {
                    if (!parameters[i].HasDefaultValue)
                    {
                        allTrailingHaveDefaults = false;
                        break;
                    }
                }

                if (allTrailingHaveDefaults)
                    return candidate;
            }

            return null;
        }

        private static object? ConvertArgument(object? value, Type targetType)
        {
            if (value == null) return null;

            var valueType = value.GetType();

            // Already correct type
            if (targetType.IsAssignableFrom(valueType))
                return value;

            // Numeric conversions
            if (targetType == typeof(int) && value is long l) return (int)l;
            if (targetType == typeof(float) && value is long l2) return (float)l2;
            if (targetType == typeof(float) && value is double d) return (float)d;
            if (targetType == typeof(double) && value is long l3) return (double)l3;
            if (targetType == typeof(long) && value is int i) return (long)i;
            if (targetType == typeof(byte) && value is long l4) return (byte)l4;
            if (targetType == typeof(short) && value is long l5) return (short)l5;

            // String to enum
            if (targetType.IsEnum && value is string s)
                return Enum.Parse(targetType, s, ignoreCase: true);

            // Fallback: Convert.ChangeType
            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return value;
            }
        }

        #endregion
    }
}
