
#nullable enable
using UnityAiBridge.Utils;

namespace UnityAiBridge.Extensions
{
    public static class ExtensionsLogLevel
    {
        public static string ToDisplayString(this LogLevel logLevel) => logLevel switch
        {
            LogLevel.Trace => "Trace",
            LogLevel.Debug => "Debug",
            LogLevel.Info => "Information",
            LogLevel.Warning => "Warning",
            LogLevel.Error => "Error",
            LogLevel.Exception => "Critical",
            LogLevel.None => "None",
            _ => "Unknown"
        };
    }
}
