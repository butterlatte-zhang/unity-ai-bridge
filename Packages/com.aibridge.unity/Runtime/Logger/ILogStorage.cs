
#nullable enable
using System;
using System.Threading.Tasks;

namespace UnityAiBridge.Logger
{
    public interface ILogStorage : IDisposable
    {
        Task AppendAsync(params LogEntry[] entries);
        void Append(params LogEntry[] entries);

        Task FlushAsync();
        void Flush();

        Task<LogEntry[]> QueryAsync(
            int maxEntries = 100,
            UnityEngine.LogType? logTypeFilter = null,
            bool includeStackTrace = false,
            int lastMinutes = 0);
        LogEntry[] Query(
            int maxEntries = 100,
            UnityEngine.LogType? logTypeFilter = null,
            bool includeStackTrace = false,
            int lastMinutes = 0);

        void Clear();
    }
}