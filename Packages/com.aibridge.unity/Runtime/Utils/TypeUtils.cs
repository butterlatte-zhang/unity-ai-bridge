
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UnityAiBridge.Utils
{
    /// <summary>
    /// Type resolution utilities.
    /// </summary>
    public static class TypeUtils
    {
        private static Type[]? _allTypes;

        /// <summary>
        /// All types from all loaded assemblies (cached).
        /// </summary>
        public static IEnumerable<Type> AllTypes
        {
            get
            {
                if (_allTypes == null)
                {
                    var types = new List<Type>();
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            if (assembly.IsDynamic)
                                continue;
                            types.AddRange(assembly.GetTypes());
                        }
                        catch (ReflectionTypeLoadException e)
                        {
                            types.AddRange(e.Types.Where(t => t != null)!);
                        }
                        catch
                        {
                            // Skip assemblies that can't be loaded
                        }
                    }
                    _allTypes = types.ToArray();
                }
                return _allTypes;
            }
        }

        /// <summary>
        /// Resolve a Type by name. Tries Type.GetType first, then scans all loaded assemblies.
        /// </summary>
        public static Type? GetType(string? typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            // Try direct resolution first
            var type = Type.GetType(typeName);
            if (type != null)
                return type;

            // Scan all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.IsDynamic)
                        continue;

                    type = assembly.GetType(typeName);
                    if (type != null)
                        return type;
                }
                catch
                {
                    // Skip problematic assemblies
                }
            }

            // Try matching by name only (without namespace)
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.IsDynamic)
                        continue;

                    foreach (var t in assembly.GetTypes())
                    {
                        if (t.Name == typeName || t.FullName == typeName)
                            return t;
                    }
                }
                catch (ReflectionTypeLoadException e)
                {
                    foreach (var t in e.Types)
                    {
                        if (t != null && (t.Name == typeName || t.FullName == typeName))
                            return t;
                    }
                }
                catch
                {
                    // Skip
                }
            }

            return null;
        }

        /// <summary>
        /// Clear the cached types. Call when assemblies are reloaded.
        /// </summary>
        public static void ClearCache()
        {
            _allTypes = null;
        }
    }
}
