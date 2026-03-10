
#nullable enable
using UnityAiBridge;

namespace UnityAiBridge.Editor.Tools
{
    [BridgeToolType]
    public partial class Tool_Console
    {
        public static class Error
        {
            public static string InvalidMaxEntries(int entriesCount)
                => $"[Error] Invalid maxEntries value '{entriesCount}'. Must be greater than 0.";

            public static string InvalidLogTypeFilter(string logType)
                => $"[Error] Invalid logType filter '{logType}'. Valid values: All, Error, Assert, Warning, Log, Exception.";
        }
    }
}
