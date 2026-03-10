
#nullable enable
using System;
using System.Collections.Generic;

namespace UnityAiBridge.Utils
{
    /// <summary>
    /// Command line argument parsing utilities.
    /// </summary>
    public static class ArgsUtils
    {
        /// <summary>
        /// Parse command line arguments into a dictionary.
        /// Supports formats: -KEY VALUE, --KEY VALUE, -KEY=VALUE, --KEY=VALUE
        /// </summary>
        public static Dictionary<string, string?> ParseCommandLineArguments()
        {
            var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (!arg.StartsWith("-"))
                    continue;

                // Strip leading dashes
                var key = arg.TrimStart('-');

                // Handle KEY=VALUE format
                var equalsIndex = key.IndexOf('=');
                if (equalsIndex >= 0)
                {
                    var value = key[(equalsIndex + 1)..];
                    key = key[..equalsIndex];
                    result[key] = value;
                }
                else
                {
                    // Check if next arg is a value (doesn't start with -)
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        result[key] = args[i + 1];
                        i++; // Skip value
                    }
                    else
                    {
                        result[key] = null;
                    }
                }
            }

            return result;
        }
    }
}
