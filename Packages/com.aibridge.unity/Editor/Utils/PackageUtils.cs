
#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityAiBridge.Editor.Tools;

namespace UnityAiBridge.Editor.Utils
{
    /// <summary>
    /// Utility for scheduling package operation notifications after domain reload.
    /// Similar to ScriptUtils but for Package add/remove operations.
    /// </summary>
    [InitializeOnLoad]
    public static class PackageUtils
    {
        private const string PendingNotificationKeysKey = "Bridge_PackagePendingNotificationKeys";
        private const string NotificationDataSeparator = "<BRIDGE_SEP>";

        private static bool _processPendingScheduled = false;

        static PackageUtils()
        {
            ScheduleProcessPendingNotifications();
        }

        private static void ScheduleProcessPendingNotifications()
        {
            if (_processPendingScheduled)
                return;

            _processPendingScheduled = true;
            EditorApplication.update += ProcessPendingNotificationsOnce;
        }

        private static void ProcessPendingNotificationsOnce()
        {
            EditorApplication.update -= ProcessPendingNotificationsOnce;

            if (!_processPendingScheduled)
                return;

            _processPendingScheduled = false;
            ProcessPendingNotifications();
        }

        /// <summary>
        /// Schedules a notification to be sent after domain reload completes.
        /// Uses SessionState to persist across domain reloads.
        /// </summary>
        public static void SchedulePostDomainReloadNotification(
            string requestId,
            string packageInfo,
            string operationType,
            bool expectedResult = true)
        {
            var notificationKey = $"Bridge_PackagePendingNotification_{requestId}";
            var notificationData = $"{requestId}{NotificationDataSeparator}{packageInfo}{NotificationDataSeparator}{operationType}{NotificationDataSeparator}{expectedResult}";

            SessionState.SetString(notificationKey, notificationData);

            var existingKeys = SessionState.GetString(PendingNotificationKeysKey, string.Empty);
            var keyList = string.IsNullOrEmpty(existingKeys)
                ? new List<string>()
                : existingKeys.Split(',').Where(k => !string.IsNullOrEmpty(k)).ToList();

            if (!keyList.Contains(notificationKey))
            {
                keyList.Add(notificationKey);
                SessionState.SetString(PendingNotificationKeysKey, string.Join(",", keyList));
            }
        }

        /// <summary>
        /// Process any pending package notifications after domain reload.
        /// </summary>
        public static void ProcessPendingNotifications()
        {
            var pendingKeys = SessionState.GetString(PendingNotificationKeysKey, string.Empty);
            if (string.IsNullOrEmpty(pendingKeys))
                return;

            var keys = pendingKeys.Split(',').Where(k => !string.IsNullOrEmpty(k)).ToList();
            if (keys.Count == 0)
                return;

            var processedKeys = new List<string>();

            foreach (var key in keys)
            {
                var notificationData = SessionState.GetString(key, string.Empty);
                if (string.IsNullOrEmpty(notificationData))
                {
                    processedKeys.Add(key);
                    continue;
                }

                var parts = notificationData.Split(NotificationDataSeparator);
                if (parts.Length < 3)
                {
                    processedKeys.Add(key);
                    continue;
                }

                var requestId = parts[0];
                var packageInfo = parts[1];
                var operationType = parts[2];

                var message = $"[Success] Package {operationType} completed: {packageInfo}";
                var response = ResponseCallTool.Success(message).SetRequestID(requestId);

                _ = BridgeCompat.NotifyToolRequestCompleted(new RequestToolCompletedData
                {
                    RequestId = requestId,
                    Result = response
                });
                processedKeys.Add(key);
            }

            foreach (var key in processedKeys)
                SessionState.EraseString(key);

            var remainingKeys = keys.Except(processedKeys).ToList();
            if (remainingKeys.Count > 0)
            {
                SessionState.SetString(PendingNotificationKeysKey, string.Join(",", remainingKeys));
            }
            else
            {
                SessionState.EraseString(PendingNotificationKeysKey);
            }
        }
    }
}
