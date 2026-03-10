#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityAiBridge.Editor.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_LightProbe
    {
        [BridgeTool
        (
            LightProbeClearToolId,
            Title = "LightProbe / Clear Auto"
        )]
        [Description("Remove auto-generated LightProbeGroup GameObjects from the scene. " +
            "By default only removes groups whose name starts with 'LightProbeGroup_Auto'. " +
            "Set removeAll=true to remove ALL LightProbeGroups.")]
        public ClearResult ClearAutoProbes
        (
            [Description("If true, remove ALL LightProbeGroups in the scene, not just auto-generated ones.")]
            bool removeAll = false
        )
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                var allGroups = UnityEngine.Object.FindObjectsByType<LightProbeGroup>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (allGroups.Length == 0)
                {
                    return new ClearResult
                    {
                        RemovedCount = 0,
                        RemainingCount = 0,
                        Messages = new List<string> { "No LightProbeGroups found in scene." }
                    };
                }

                var toRemove = removeAll
                    ? allGroups.ToList()
                    : allGroups.Where(g => g.gameObject.name.StartsWith("LightProbeGroup_Auto")).ToList();

                var removedNames = new List<string>();
                foreach (var group in toRemove)
                {
                    removedNames.Add(group.gameObject.name);
                    UnityEngine.Object.DestroyImmediate(group.gameObject);
                }

                int remaining = allGroups.Length - toRemove.Count;

                if (removedNames.Count > 0)
                {
                    EditorSceneManager.MarkSceneDirty(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                    EditorUtils.RepaintAllEditorWindows();
                }

                return new ClearResult
                {
                    RemovedCount = removedNames.Count,
                    RemainingCount = remaining,
                    RemovedNames = removedNames,
                    Messages = new List<string>
                    {
                        removedNames.Count > 0
                            ? $"[Success] Removed {removedNames.Count} LightProbeGroup(s): {string.Join(", ", removedNames)}."
                            : "No matching LightProbeGroups found to remove.",
                        remaining > 0
                            ? $"{remaining} LightProbeGroup(s) remaining in scene."
                            : "No LightProbeGroups remaining in scene."
                    }
                };
            });
        }

        #region Clear Response Types

        public class ClearResult
        {
            [Description("Number of LightProbeGroups removed.")]
            public int RemovedCount { get; set; }

            [Description("Number of LightProbeGroups remaining in scene.")]
            public int RemainingCount { get; set; }

            [Description("Names of removed GameObjects.")]
            public List<string>? RemovedNames { get; set; }

            [Description("Status messages.")]
            public List<string>? Messages { get; set; }
        }

        #endregion
    }
}
