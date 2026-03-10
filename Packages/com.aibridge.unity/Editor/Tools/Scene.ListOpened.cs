
#nullable enable
using System.ComponentModel;
using System.Linq;
using UnityAiBridge;
using UnityAiBridge.Utils;
using UnityAiBridge.Data;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Scene
    {
        public const string SceneListOpenedToolId = "scene-list-opened";
        [BridgeTool
        (
            SceneListOpenedToolId,
            Title = "Scene / List Opened"
        )]
        [Description("Returns the list of currently opened scenes in Unity Editor. " +
            "Use '" + SceneGetDataToolId + "' tool to get detailed information about a specific scene.")]
        public SceneDataShallow[] ListOpened()
        {
            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                return OpenedScenes
                    .Select(scene => scene.ToSceneDataShallow())
                    .ToArray();
            });
        }
    }
}
