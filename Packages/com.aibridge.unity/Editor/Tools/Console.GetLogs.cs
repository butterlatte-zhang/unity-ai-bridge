
#nullable enable
using System;
using System.ComponentModel;
using UnityAiBridge;
using UnityEngine;
using UnityAiBridge.Logger;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Console
    {
        public const string ConsoleGetLogsToolId = "console-get-logs";
        [BridgeTool
        (
            ConsoleGetLogsToolId,
            Title = "Console / Get Logs"
        )]
        [Description("Retrieves Unity Editor logs. " +
            "Useful for debugging and monitoring Unity Editor activity.")]
        public LogEntry[] GetLogs
        (
            [Description("Maximum number of log entries to return. Minimum: 1. Default: 100")]
            int maxEntries = 100,
            [Description("Filter by log type. 'null' means All.")]
            LogType? logTypeFilter = null,
            [Description("Include stack traces in the output. Default: false")]
            bool includeStackTrace = false,
            [Description("Return logs from the last N minutes. If 0, returns all available logs. Default: 0")]
            int lastMinutes = 0
        )
        {
            // Validate parameters
            if (maxEntries < 1)
                throw new ArgumentException(Error.InvalidMaxEntries(maxEntries));

            if (!BridgeCompat.HasInstance)
                throw new InvalidOperationException("[Error] BridgeCompat is not initialized.");

            var logCollector = BridgeCompat.Instance.LogCollector;
            if (logCollector == null)
                throw new InvalidOperationException("[Error] LogCollector is not initialized.");

            // Get all log entries as array to avoid concurrent modification
            var logs = logCollector.Query(
                maxEntries: maxEntries,
                logTypeFilter: logTypeFilter,
                includeStackTrace: includeStackTrace,
                lastMinutes: lastMinutes
            );

            return logs;
        }
    }
}