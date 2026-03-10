
#nullable enable
using System;
using UnityAiBridge;

namespace UnityAiBridge.Editor.Tools
{
    [BridgeToolType]
    public partial class Tool_Package
    {
        public static class Error
        {
            public static string PackageNameIsEmpty()
                => "[Error] Package name is empty. Please provide a valid package name. Sample: 'com.unity.textmeshpro'.";

            public static string PackageIdentifierIsEmpty()
                => "[Error] Package identifier is empty. Please provide a valid package identifier. Sample: 'com.unity.textmeshpro' or 'com.unity.textmeshpro@3.0.6'.";

            public static string PackageNotFound(string packageName)
                => $"[Error] Package '{packageName}' not found in the project.";

            public static string PackageOperationFailed(string operation, string packageName, string error)
                => $"[Error] Failed to {operation} package '{packageName}': {error}";

            public static string PackageSearchFailed(string query, string error)
                => $"[Error] Failed to search for packages with query '{query}': {error}";

            public static string PackageListFailed(string error)
                => $"[Error] Failed to list packages: {error}";
        }

        /// <summary>
        /// Returns search priority (lower = better match). Returns 0 if no match.
        /// Priority order: 1=name exact, 2=displayName exact, 3=name substring, 4=displayName substring, 5=description substring
        /// </summary>
        protected static int GetSearchPriority(string? name, string? displayName, string? description, string query)
        {
            if (name?.Equals(query, StringComparison.OrdinalIgnoreCase) == true)
                return 1;
            if (displayName?.Equals(query, StringComparison.OrdinalIgnoreCase) == true)
                return 2;
            if (name?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                return 3;
            if (displayName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                return 4;
            if (description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                return 5;
            return 0; // No match
        }
    }
}
