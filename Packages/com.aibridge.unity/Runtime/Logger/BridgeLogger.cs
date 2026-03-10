#nullable enable

using System;
using UnityEngine;

namespace UnityAiBridge.Logger
{
    /// <summary>
    /// Simple logger interface, replaces Microsoft.Extensions.Logging.ILogger.
    /// </summary>
    public interface IBridgeLogger
    {
        void LogTrace(string message);
        void LogDebug(string message);
        void LogInformation(string message);
        void LogWarning(string message);
        void LogError(string message);
        void LogError(Exception exception, string message);
    }

    /// <summary>
    /// Unity Debug.Log based implementation.
    /// </summary>
    public class UnityBridgeLogger : IBridgeLogger
    {
        private readonly string _category;

        public UnityBridgeLogger(string category)
        {
            _category = category;
        }

        public void LogTrace(string message) { }  // no-op for trace
        public void LogDebug(string message) => Debug.Log($"[{_category}] {message}");
        public void LogInformation(string message) => Debug.Log($"[{_category}] {message}");
        public void LogWarning(string message) => Debug.LogWarning($"[{_category}] {message}");
        public void LogError(string message) => Debug.LogError($"[{_category}] {message}");
        public void LogError(Exception exception, string message) => Debug.LogError($"[{_category}] {message}\n{exception}");
    }

    /// <summary>
    /// Factory for creating loggers. Replaces UnityLoggerFactory.LoggerFactory.
    /// </summary>
    public static class BridgeLoggerFactory
    {
        public static IBridgeLogger CreateLogger<T>() => new UnityBridgeLogger(typeof(T).Name);
        public static IBridgeLogger CreateLogger(string name) => new UnityBridgeLogger(name);
    }
}
