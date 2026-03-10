
#nullable enable

namespace UnityAiBridge.Utils
{
    /// <summary>
    /// String utility methods.
    /// </summary>
    public static class StringUtils
    {
        /// <summary>
        /// Get indentation padding for hierarchical display.
        /// </summary>
        public static string GetPadding(int depth)
        {
            if (depth <= 0)
                return string.Empty;
            return new string(' ', depth * 2);
        }

        /// <summary>
        /// Check if a string is null or empty. Equivalent to string.IsNullOrEmpty
        /// but used as a static method for consistency with original codebase.
        /// </summary>
        public static bool IsNullOrEmpty(string? value)
        {
            return string.IsNullOrEmpty(value);
        }

        /// <summary>
        /// Trim a path string by removing leading/trailing whitespace and trailing slashes.
        /// </summary>
        public static string? TrimPath(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            return path!.Trim().TrimEnd('/');
        }

        /// <summary>
        /// Parse a path into parent path and name components.
        /// Returns true if parsing succeeded (path contains a separator).
        /// </summary>
        public static bool Path_ParseParent(string? path, out string? parentPath, out string? name)
        {
            if (string.IsNullOrEmpty(path))
            {
                parentPath = null;
                name = null;
                return false;
            }

            var trimmed = path!.Trim().TrimEnd('/');
            var lastSlash = trimmed.LastIndexOf('/');

            if (lastSlash < 0)
            {
                parentPath = null;
                name = trimmed;
                return false;
            }

            parentPath = trimmed[..lastSlash];
            name = trimmed[(lastSlash + 1)..];
            return true;
        }
    }
}
