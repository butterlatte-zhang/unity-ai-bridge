
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using UnityAiBridge.Utils;
using UnityAiBridge.Logger;
using UnityEngine;

namespace UnityAiBridge.Logger
{
    public class FileLogStorage : ILogStorage, IDisposable
    {
        protected const int DefaultMaxFileSizeMB = 512;

        protected readonly IBridgeLogger _logger;
        protected readonly string _directoryPath;
        protected readonly string _requestedFileName;
        protected readonly JsonSerializerOptions _jsonOptions;
        protected readonly object _fileMutex = new();
        protected readonly int _fileBufferSize;
        protected readonly long _maxFileSizeBytes;
        protected readonly ThreadSafeBool _isDisposed = new(false);

        protected string fileName;
        protected string filePath;

        protected FileStream? fileWriteStream;

        public FileLogStorage(
            IBridgeLogger? logger = null,
            string? directoryPath = null,
            string? requestedFileName = null,
            int fileBufferSize = 4096,
            int maxFileSizeMB = DefaultMaxFileSizeMB,
            JsonSerializerOptions? jsonOptions = null)
        {
            if (!MainThread.Instance.IsMainThread)
                throw new Exception($"{nameof(FileLogStorage)} must be initialized on the main thread.");

            if (fileBufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(fileBufferSize), "File buffer size must be greater than zero.");

            if (maxFileSizeMB <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxFileSizeMB), "Max file size must be greater than zero.");

            _logger = logger ?? BridgeLoggerFactory.CreateLogger(GetType().Name);

            _directoryPath = Path.GetFullPath(directoryPath ?? (Application.isEditor
                ? $"{Path.GetDirectoryName(Application.dataPath)}/Temp/UnityBridge/logs"
                : $"{Application.persistentDataPath}/Temp/UnityBridge/logs"));

            _requestedFileName = requestedFileName ?? (Application.isEditor
                ? "ai-editor-logs.txt"
                : "ai-player-logs.txt");

            if (!Directory.Exists(_directoryPath))
                Directory.CreateDirectory(_directoryPath);

            _fileBufferSize = fileBufferSize;
            _maxFileSizeBytes = maxFileSizeMB * 1024L * 1024L;

            _jsonOptions = jsonOptions ?? new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            fileWriteStream = CreateWriteStream(_requestedFileName, out fileName, out filePath);
        }

        protected virtual FileStream CreateWriteStream(string fileName, out string resultFileName, out string resultFilePath)
        {
            if (_isDisposed.Value)
            {
                _logger.LogWarning($"{nameof(CreateWriteStream)} called but already disposed, ignored.");
                throw new ObjectDisposedException(GetType().Name);
            }

            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            var currentFileName = fileName;
            int incrementIndex = 1;

            while (true)
            {
                if (incrementIndex > 1000)
                    throw new Exception("Failed to create unique log file name after 1000 attempts.");

                try
                {
                    var filePath = Path.GetFullPath(Path.Combine(_directoryPath, currentFileName));

                    _logger.LogDebug($"Creating log file stream: {filePath}");

                    var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read, bufferSize: _fileBufferSize, useAsync: false)
                        ?? throw new Exception("Failed to create file stream for log storage.");

                    resultFileName = currentFileName;
                    resultFilePath = filePath;

                    return stream;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to create log file stream for {currentFileName}. Retrying with a different file name.");
                    incrementIndex++;
                    currentFileName = $"{baseName}-{incrementIndex}{extension}";
                }
            }
        }

        public virtual void Flush()
        {
            if (_isDisposed.Value)
            {
                _logger.LogWarning($"{nameof(Flush)} called but already disposed, ignored.");
                return;
            }
            lock (_fileMutex)
            {
                fileWriteStream?.Flush();
            }
        }
        public virtual Task FlushAsync()
        {
            if (_isDisposed.Value)
            {
                _logger.LogWarning($"{nameof(FlushAsync)} called but already disposed, ignored.");
                return Task.CompletedTask;
            }
            return Task.Run(() =>
            {
                if (_isDisposed.Value)
                {
                    _logger.LogWarning($"{nameof(FlushAsync)} called but already disposed, ignored.");
                    return;
                }
                lock (_fileMutex)
                {
                    fileWriteStream?.Flush();
                }
            });
        }

        public virtual Task AppendAsync(params LogEntry[] entries)
        {
            if (_isDisposed.Value)
            {
                _logger.LogWarning($"{nameof(AppendAsync)} called but already disposed, ignored.");
                return Task.CompletedTask;
            }
            return Task.Run(() =>
            {
                if (_isDisposed.Value)
                {
                    _logger.LogWarning($"{nameof(AppendAsync)} called but already disposed, ignored.");
                    return;
                }
                lock (_fileMutex)
                {
                    AppendInternal(entries);
                }
            });
        }

        public virtual void Append(params LogEntry[] entries)
        {
            if (_isDisposed.Value)
            {
                _logger.LogWarning($"{nameof(Append)} called but already disposed, ignored.");
                return;
            }
            lock (_fileMutex)
            {
                AppendInternal(entries);
            }
        }

        protected virtual void AppendInternal(params LogEntry[] entries)
        {
            if (_isDisposed.Value)
            {
                _logger.LogWarning($"{nameof(AppendInternal)} called but already disposed, ignored.");
                return;
            }
            fileWriteStream ??= CreateWriteStream(_requestedFileName, out fileName, out filePath);

            // Check if file size limit reached and reset if needed
            if (fileWriteStream.Length >= _maxFileSizeBytes)
            {
                ResetLogFile();
            }

            foreach (var entry in entries)
            {
                System.Text.Json.JsonSerializer.Serialize(fileWriteStream, entry, _jsonOptions);
                fileWriteStream.WriteByte((byte)'\n');
            }
            fileWriteStream.Flush();
        }

        /// <summary>
        /// Resets the log file by deleting it and creating a new one.
        /// Called when file size limit is reached.
        /// </summary>
        protected virtual void ResetLogFile()
        {
            if (_isDisposed.Value)
            {
                _logger.LogWarning($"{nameof(ResetLogFile)} called but already disposed, ignored.");
                return;
            }

            _logger.LogInformation($"Log file size limit reached ({_maxFileSizeBytes / (1024 * 1024)}MB). Resetting log file.");

            fileWriteStream?.Flush();
            fileWriteStream?.Dispose();
            fileWriteStream = null;

            if (File.Exists(filePath))
                File.Delete(filePath);

            fileWriteStream = CreateWriteStream(_requestedFileName, out this.fileName, out filePath);
        }

        /// <summary>
        /// Closes and disposes the current file stream if open. Clears the log cache file.
        /// </summary>
        public virtual void Clear()
        {
            if (_isDisposed.Value)
            {
                _logger.LogWarning($"{nameof(Clear)} called but already disposed, ignored.");
                return;
            }
            lock (_fileMutex)
            {
                fileWriteStream?.Dispose();
                fileWriteStream = null;

                if (File.Exists(filePath))
                    File.Delete(filePath);

                if (File.Exists(filePath))
                    _logger.LogError($"Failed to delete cache file: {filePath}");
            }
        }

        public virtual Task<LogEntry[]> QueryAsync(
            int maxEntries = 100,
            LogType? logTypeFilter = null,
            bool includeStackTrace = false,
            int lastMinutes = 0)
        {
            if (_isDisposed.Value)
            {
                _logger.LogWarning($"{nameof(QueryAsync)} called but already disposed, ignored.");
                return Task.FromResult(Array.Empty<LogEntry>());
            }
            return Task.Run(() => Query(maxEntries, logTypeFilter, includeStackTrace, lastMinutes));
        }

        public virtual LogEntry[] Query(
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

        protected virtual LogEntry[] QueryInternal(
            int maxEntries = 100,
            LogType? logTypeFilter = null,
            bool includeStackTrace = false,
            int lastMinutes = 0)
        {
            if (!File.Exists(filePath))
                return Array.Empty<LogEntry>();

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var cutoffTime = lastMinutes > 0
                    ? DateTime.Now.AddMinutes(-lastMinutes)
                    : (DateTime?)null;

                var allLogs = ReadLogEntriesFromLinesInReverse(fileStream, cutoffTime);

                // Apply log type filter
                if (logTypeFilter.HasValue)
                {
                    allLogs = allLogs
                        .Where(log => log.LogType == logTypeFilter.Value);
                }

                // Take the most recent entries (up to maxEntries)
                var filteredLogs = allLogs
                    .Take(maxEntries)
                    .Reverse()
                    .ToArray();

                return filteredLogs;
            }
        }

        protected virtual IEnumerable<LogEntry> ReadLogEntriesFromLinesInReverse(FileStream fileStream, DateTime? cutoffTime = null)
        {
            if (_isDisposed.Value)
            {
                _logger.LogWarning($"{nameof(ReadLogEntriesFromLinesInReverse)} called but already disposed, ignored.");
                yield break;
            }
            var position = fileStream.Length;
            if (position == 0) yield break;

            var buffer = new byte[_fileBufferSize];
            var lineBuffer = new List<byte>();

            while (position > 0)
            {
                var bytesToRead = (int)Math.Min(position, _fileBufferSize);
                position -= bytesToRead;
                fileStream.Seek(position, SeekOrigin.Begin);
                fileStream.Read(buffer, 0, bytesToRead);

                for (int i = bytesToRead - 1; i >= 0; i--)
                {
                    var b = buffer[i];
                    if (b == '\n')
                    {
                        if (lineBuffer.Count > 0)
                        {
                            lineBuffer.Reverse();
                            var logEntry = DeserializeLogEntry(lineBuffer);
                            if (logEntry != null)
                            {
                                if (cutoffTime.HasValue && logEntry.Timestamp < cutoffTime.Value)
                                    yield break;

                                yield return logEntry;
                            }
                            lineBuffer.Clear();
                        }
                    }
                    else if (b == '\r')
                    {
                        // Ignore \r
                    }
                    else
                    {
                        lineBuffer.Add(b);
                    }
                }
            }

            if (lineBuffer.Count > 0)
            {
                lineBuffer.Reverse();
                var logEntry = DeserializeLogEntry(lineBuffer);
                if (logEntry != null)
                {
                    if (cutoffTime.HasValue && logEntry.Timestamp < cutoffTime.Value)
                        yield break;

                    yield return logEntry;
                }
            }
        }

        protected virtual LogEntry? DeserializeLogEntry(List<byte> jsonBytes)
        {
            var json = System.Text.Encoding.UTF8.GetString(jsonBytes.ToArray());
            return DeserializeLogEntry(json);
        }

        protected virtual LogEntry? DeserializeLogEntry(string json)
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<LogEntry>(json, _jsonOptions);
            }
            catch
            {
                return null;
            }
        }

        public virtual void Dispose()
        {
            if (!_isDisposed.TrySetTrue())
                return; // already disposed

            try
            {
                Flush();
            }
            finally
            {
                fileWriteStream?.Dispose();
                fileWriteStream = null;
            }

            GC.SuppressFinalize(this);
        }

        ~FileLogStorage() => Dispose();
    }
}
