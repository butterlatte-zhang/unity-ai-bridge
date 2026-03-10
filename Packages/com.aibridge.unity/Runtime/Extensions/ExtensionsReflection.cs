#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityAiBridge.Data;
using UnityAiBridge.Serialization;
using UnityAiBridge.Utils;

namespace UnityAiBridge.Extensions
{
    /// <summary>
    /// Extension methods for MethodInfo filtering and parameter matching.
    /// </summary>
    public static class ExtensionsMethodInfo
    {
        /// <summary>
        /// Filter methods by matching their parameters against a SerializedMemberList.
        /// Returns the first method whose parameters match the provided list.
        /// </summary>
        public static MethodInfo? FilterByParameters(this IEnumerable<MethodInfo> methods, SerializedMemberList? parameters = null)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return methods.FirstOrDefault(m => m.GetParameters().Length == 0);
            }

            return methods.FirstOrDefault(method =>
            {
                var methodParams = method.GetParameters();
                for (int i = 0; i < methodParams.Length; i++)
                {
                    var paramInfo = methodParams[i];
                    if (i >= parameters.Count)
                    {
                        if (paramInfo.IsOptional)
                            break;
                        return false;
                    }

                    var member = parameters[i];
                    if (paramInfo.Name != member.name ||
                        paramInfo.ParameterType != TypeUtils.GetType(member.typeName))
                    {
                        return false;
                    }
                }
                return true;
            });
        }
    }

    /// <summary>
    /// Extension methods for SerializedMemberList that handle null references.
    /// </summary>
    public static class ExtensionsSerializedMemberList
    {
        /// <summary>
        /// Validate that all type names in the list resolve to actual types.
        /// Handles null list by returning true (no parameters to validate).
        /// </summary>
        public static bool IsValidTypeNames(this SerializedMemberList? parameters, string fieldName, out string? error)
        {
            if (parameters == null || parameters.Count == 0)
            {
                error = null;
                return true;
            }

            bool result = true;
            var sb = new StringBuilder();

            for (int i = 0; i < parameters.Count; i++)
            {
                var member = parameters[i];
                if (string.IsNullOrEmpty(member.typeName))
                {
                    sb.AppendLine($"[Error] {fieldName}[{i}].typeName is empty. Please specify the 'name' properly.");
                    result = false;
                }
                else if (TypeUtils.GetType(member.typeName) == null)
                {
                    sb.AppendLine($"[Error] {fieldName}[{i}].typeName type '{member.typeName}' not found. Please specify the 'name' properly.");
                    result = false;
                }
            }

            error = sb.ToString();
            if (string.IsNullOrEmpty(error))
                error = null;

            return result;
        }
    }
}
