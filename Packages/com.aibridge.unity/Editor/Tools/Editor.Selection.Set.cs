
#nullable enable
using System.ComponentModel;
using System.Linq;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityAiBridge.Data;
using UnityAiBridge.Extensions;
using UnityEditor;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Editor_Selection
    {
        public const string EditorSelectionSetToolId = "editor-selection-set";
        [BridgeTool
        (
            EditorSelectionSetToolId,
            Title = "Editor / Selection / Set"
        )]
        [Description("Set the current Selection in the Unity Editor to the provided objects. " +
            "Use '" + EditorSelectionGetToolId + "' tool to get the current selection first.")]
        public SelectionData Set(ObjectRef[] select)
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                var objects = select.Select(o => o.FindObject()).ToArray();
                if (objects.Any(o => o == null))
                    throw new System.Exception("One or more objects could not be found. Please ensure all provided ObjectRefs are valid.");

                Selection.objects = objects;

                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

                return SelectionData.FromSelection();
            });
        }
    }
}
