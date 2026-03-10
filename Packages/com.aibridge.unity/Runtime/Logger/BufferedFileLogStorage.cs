
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using UnityAiBridge.Logger;
using UnityEngine;

namespace UnityAiBridge.Logger
{
    public class BufferedFileLogStorage : FileLogStorage
    {
        protected readonly int _flushEntriesThreshold;
        protected readonly LogEntry[] _logEntriesBuffer;
        protected int _logEntriesBufferLength;

        public BufferedFileLogStorage(
            IBridgeLogger? logger = null,
            int flushEntriesThreshold = 100,
            string? cacheFilePath = null,
            string? cacheFileName = null,
            int fileBufferSize = 4096,
            int maxFileSizeMB = DefaultMaxFileSizeMB,
            JsonSerializerOptions? jsonOptions = null)
            : base(logger, cacheFilePath, cacheFileName, fileBufferSize, maxFileSizeMB, jsonOptions)
        {
            if (flushEntriesThreshold <= 0)
                throw new ArgumentOutOfRangeException(nameof(flushEntriesThreshold), "Flush entries threshold must be greater than zero.");

            _flushEntriesThreshold = flushEntriesThreshold;
            _logEntriesBuffer = new LogEntry[flushEntriesThreshold];
            _logEntriesBufferLength = 0;
        }

        public override void Flush()
        {
            if (_isDisposed.Value)
            {
                _logger.LogWarning($"{nameof(Flush)} called but already disposed, ignored.");
                return;
            }
            lock (_fileMutex)
            {
                // Flush buffered entries to file
                if (_logEntriesBufferLength > 0)
                {
                    var entriesToFlush = new LogEntry[_logEntriesBufferLength];
                    Array.Copy(_logEntriesBuffer, entriesToFlush, _logEntriesBufferLength);
                    base.AppendInternal(entriesToFlush);
                    _logEntriesBufferLength = 0;
                }
                fileWriteStream?.Flush();
            }
        }
        public override Task FlushAsync()
        {
            if (_isDisposed.Value)
            {
                _logger.LogWarning($"{nameof(FlushAsync)} called but already disposed, ignored.");
                return Task.CompletedTask;
            }
            lock (_fileMutex)
            {
                // Flush buffered entries to file
                if (_logEntriesBufferLength > 0)
                {
                    var entriesToFlush = new LogEntry[_logEntriesBufferLength];
                    Array.Copy(_logEntriesBuffer, entriesToFlush, _logEntriesBufferLength);
                    base.AppendInternal(entriesToFlush);
                    _logEntriesBufferLength = 0;
                }
                fileWriteStream?.Flush();
            }
            return Task.CompletedTask;
        }

        protected override void AppendInternal(params LogEntry[] entries)
        {
            if (_isDisposed.Value)
            {
                _logger.LogWarning($"{nameof(AppendInternal)} called but already disposed, ignored.");
                return;
            }
            if (_logEntriesBufferLength >= _flushEntriesThreshold)
            {
                base.AppendInternal(_logEntriesBuffer);
                _logEntriesBufferLength = 0;
            }
            foreach (var entry in entries)
            {
                _logEntriesBuffer[_logEntriesBufferLength] = entry;
                _logEntriesBufferLength++;

                if (_logEntriesBufferLength >= _flushEntriesThreshold)
                {
                    base.AppendInternal(_logEntriesBuffer);
                    _logEntriesBufferLength = 0;
                }
            }
        }

        /// <summary>
        /// Closes and disposes the current file stream if open. Clears the log cache file.
        /// </summary>
        public override void Clear()
        {
            if (_isDisposed.Value)
            {
                _logger.LogWarning($"{nameof(Clear)} called but already disposed, ignored.");
                return;
            }
            lock (_fileMutex)
            {
                fileWriteStream?.Close();
                fileWriteStream?.Dispose();
                fileWriteStream = null;
                _logEntriesBufferLength = 0;

                if (File.Exists(filePath))
                    File.Delete(filePath);

                if (File.Exists(filePath))
                    _logger.LogError($"Failed to delete cache file: {filePath}");
            }
        }

        public override LogEntry[] Query(
            int maxEntries = 100,
            LogType? logTypeFilter = null,
            bool includeStackTrace = false,
            int lastMinutes = 0)
        {
            if (_isDisposed.Value)
            {
                _logger.LogWarning($"{nameof(Query)} called but already disposed, ignored.");
                return Array.Empty<LogEntry>();
            }
            lock (_fileMutex)
            {
                return QueryInternal(maxEntries, logTypeFilter, includeStackTrace, lastMinutes);
            }
        }

        protected override LogEntry[] QueryInternal(
            int maxEntries = 100,
            LogType? logTypeFilter = null,
            bool includeStackTrace = false,
            int lastMinutes = 0)
        {
            var result = new List<LogEntry>();
            var cutoffTime = lastMinutes > 0
                ? System.DateTime.Now.AddMinutes(-lastMinutes)
                : System.DateTime.MinValue;

            // 1. Get from buffer (Newest are at the end of buffer)
            for (int i = _logEntriesBufferLength - 1; i >= 0; i--)
            {
                var entry = _logEntriesBuffer[i];
                if (logTypeFilter.HasValue && entry.LogType != logTypeFilter.Value)
                    continue;

                if (lastMinutes > 0)
                {
                    if (entry.Timestamp < cutoffTime)
                    {
                        return result.AsEnumerable().Reverse().ToArray();
                    }
                }

                result.Add(entry);
                if (result.Count >= maxEntries)
                    return result.AsEnumerable().Reverse().ToArray();
            }

            // 2. Exit if we already have enough entries
            var neededLogsCount = maxEntries - result.Count;
            if (neededLogsCount <= 0)
                return result.AsEnumerable().Reverse().ToArray();

            result.Reverse();

            // 3. Get from file
            var fileEntries = base.QueryInternal(neededLogsCount, logTypeFilter, includeStackTrace, lastMinutes);
            result.AddRange(fileEntries);

            return result.ToArray();
        }

        ~BufferedFileLogStorage() => Dispose();
    }
}
