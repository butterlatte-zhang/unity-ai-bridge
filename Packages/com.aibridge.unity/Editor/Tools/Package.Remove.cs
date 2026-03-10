
#nullable enable
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using UnityAiBridge;

using UnityAiBridge.Utils;
using UnityAiBridge.Editor.Utils;
using UnityEditor.PackageManager;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Package
    {
        public const string PackageRemoveToolId = "package-remove";
        [BridgeTool
        (
            PackageRemoveToolId,
            Title = "Package Manager / Remove"
        )]
        [Description("Remove (uninstall) a package from the Unity project. " +
            "This removes the package from the project's manifest.json and triggers package resolution. " +
            "Note: Built-in packages and packages that are dependencies of other installed packages cannot be removed. " +
            "Note: Package removal may trigger a domain reload. The result will be sent after the reload completes. " +
            "Use '" + PackageListToolId + "' tool to list installed packages first.")]
        public static ResponseCallTool Remove
        (
            [Description("The ID of the package to remove. Example: 'com.unity.textmeshpro'. Do not include version number.")]
            string packageId,
            [RequestID]
            string? requestId = null
        )
        {
            if (requestId == null || string.IsNullOrWhiteSpace(requestId))
                return ResponseCallTool.Error("[Error] Original request with valid RequestID must be provided.");

            if (string.IsNullOrWhiteSpace(packageId))
                return ResponseCallTool.Error(Error.PackageNameIsEmpty()).SetRequestID(requestId);

            // Remove version suffix if accidentally included
            var cleanPackageId = packageId.Contains("@", StringComparison.OrdinalIgnoreCase)
                ? packageId.Substring(0, packageId.IndexOf('@', StringComparison.OrdinalIgnoreCase))
                : packageId;

            UnityAiBridge.Utils.MainThread.Instance.RunAsync(async () =>
            {
                await Task.Yield();

                // First verify the package is installed
                var listRequest = Client.List(offlineMode: true);
                while (!listRequest.IsCompleted)
                    await Task.Yield();

                if (listRequest.Status == StatusCode.Success)
                {
                    var isInstalled = false;
                    foreach (var pkg in listRequest.Result)
                    {
                        if (pkg.name.Equals(cleanPackageId, StringComparison.OrdinalIgnoreCase))
                        {
                            isInstalled = true;
                            break;
                        }
                    }

                    if (!isInstalled)
                    {
                        _ = BridgeCompat.NotifyToolRequestCompleted(new RequestToolCompletedData
                        {
                            RequestId = requestId,
                            Result = ResponseCallTool.Error(Error.PackageNotFound(cleanPackageId)).SetRequestID(requestId)
                        });
                        return;
                    }
                }

                var removeRequest = Client.Remove(cleanPackageId);

                while (!removeRequest.IsCompleted)
                    await Task.Yield();

                if (removeRequest.Status == StatusCode.Failure)
                {
                    var errorMessage = Error.PackageOperationFailed("remove", cleanPackageId, removeRequest.Error?.message ?? "Unknown error");
                    _ = BridgeCompat.NotifyToolRequestCompleted(new RequestToolCompletedData
                    {
                        RequestId = requestId,
                        Result = ResponseCallTool.Error(errorMessage).SetRequestID(requestId)
                    });
                    return;
                }

                // Schedule notification to be sent after domain reload completes
                PackageUtils.SchedulePostDomainReloadNotification(
                    requestId,
                    cleanPackageId,
                    "remove",
                    expectedResult: true
                );
            });

            return ResponseCallTool.Processing($"Removing package '{cleanPackageId}'. Waiting for package resolution and potential domain reload...").SetRequestID(requestId);
        }
    }
}
