using System.Text.RegularExpressions;

namespace UnityAiBridge.Utils
{
    public static partial class ErrorUtils
    {
        /// <summary>
        /// Extracts a process ID from file lock error messages.
        /// </summary>
        /// <param name="error">The error message to parse.</param>
        /// <param name="processId">The extracted process ID, or -1 if not found.</param>
        /// <returns>True if a process ID was successfully extracted, false otherwise.</returns>
        public static bool ExtractProcessId(string error, out int processId)
        {
            processId = -1;

            if (string.IsNullOrWhiteSpace(error))
                return false;

            try
            {
                // Define a regex pattern to match the process ID in file lock messages
                var pattern = @"The file is locked by: ""[^""]+ \((\d+)\)""";
                var match = Regex.Match(error, pattern);

                return match.Success && int.TryParse(match.Groups[1].Value, out processId);
            }
            catch (RegexMatchTimeoutException)
            {
                // Handle regex timeout gracefully
                return false;
            }
        }
    }
}