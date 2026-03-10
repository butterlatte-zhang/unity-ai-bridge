#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UnityAiBridge.Utils
{
    /// <summary>
    /// Extension methods for Type.
    /// </summary>
    public static class TypeExtensions
    {
        /// <summary>
        /// Get a human-readable type ID string.
        /// Includes namespace for non-generic types and handles generics, arrays, nullable, and nested types.
        /// </summary>
        public static string GetTypeId(this Type type)
        {
            if (type == null)
                return "null";

            if (type.IsGenericParameter)
                return type.Name;

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
                type = underlying;

            if (type == typeof(string))
                return type.FullName ?? "null";

            // Nested types
            if (type.IsNested)
            {
                var declaring = type.DeclaringType;
                if (declaring != null)
                {
                    if (declaring.IsGenericTypeDefinition && type.IsGenericType && !type.IsGenericTypeDefinition)
                    {
                        var genericArgs = type.GetGenericArguments();
                        var declaringArgs = declaring.GetGenericArguments();
                        if (genericArgs.Length >= declaringArgs.Length)
                        {
                            var typeArgs = genericArgs.Take(declaringArgs.Length).ToArray();
                            declaring = declaring.MakeGenericType(typeArgs);
                        }
                    }

                    var declaringId = GetTypeId(declaring);
                    var shortName = type.Name;
                    var backtick = shortName.IndexOf('`');
                    if (backtick > 0)
                        shortName = shortName[..backtick];

                    if (type.IsGenericType)
                    {
                        var genericArgs = type.GetGenericArguments();
                        int skip = declaring.IsGenericType ? declaring.GetGenericArguments().Length : 0;
                        if (genericArgs.Length > skip)
                        {
                            var argNames = genericArgs.Skip(skip).Select(GetTypeId);
                            return declaringId + "+" + shortName + "<" + string.Join(",", argNames) + ">";
                        }
                    }

                    return declaringId + "+" + shortName;
                }
            }

            if (type.IsGenericType)
            {
                var fullName = type.GetGenericTypeDefinition().FullName ?? type.Name;
                var backtick = fullName.IndexOf('`');
                if (backtick > 0)
                    fullName = fullName[..backtick];
                var argNames = type.GetGenericArguments().Select(GetTypeId);
                return fullName + "<" + string.Join(",", argNames) + ">";
            }

            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                if (elementType == null)
                    return type.FullName ?? type.Name;
                int rank = type.GetArrayRank();
                if (rank == 1)
                    return GetTypeId(elementType) + "[]";
                return GetTypeId(elementType) + "[" + new string(',', rank - 1) + "]";
            }

            return type.FullName ?? "null";
        }

        /// <summary>
        /// Get the short name of a type without namespace.
        /// </summary>
        public static string GetTypeShortName(this Type? type)
        {
            if (type == null)
                return "null";

            if (type.IsGenericParameter)
                return type.Name;

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
                return GetTypeShortName(underlying) + "?";

            if (type.IsGenericType)
            {
                var shortName = type.Name;
                var backtick = shortName.IndexOf('`');
                if (backtick > 0)
                    shortName = shortName[..backtick];
                var argNames = type.GetGenericArguments().Select(GetTypeShortName);
                return shortName + "<" + string.Join(", ", argNames) + ">";
            }

            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                int rank = type.GetArrayRank();
                if (rank == 1)
                    return GetTypeShortName(elementType) + "[]";
                return GetTypeShortName(elementType) + "[" + new string(',', rank - 1).Replace(",", ", ") + "]";
            }

            return !string.IsNullOrEmpty(type.Name) ? type.Name : "null";
        }

        /// <summary>
        /// Check if a type matches a type name string.
        /// Compares against the type's GetTypeId() result.
        /// </summary>
        public static bool IsMatch(this Type? type, string? typeName)
        {
            if (type == null || string.IsNullOrEmpty(typeName))
                return false;

            return type.GetTypeId() == typeName;
        }
    }

    /// <summary>
    /// Assembly scanning utilities.
    /// </summary>
    public static class AssemblyUtils
    {
        private static Assembly[]? _allAssemblies;

        public static Assembly[] AllAssemblies
        {
            get
            {
                _allAssemblies ??= AppDomain.CurrentDomain.GetAssemblies();
                return _allAssemblies;
            }
        }

        public static void ClearCache()
        {
            _allAssemblies = null;
        }
    }

}
