
#nullable enable
using System;
using System.ComponentModel;
using UnityAiBridge;
using UnityAiBridge.Serialization;
using UnityAiBridge.Utils;
using UnityAiBridge.Data;
using UnityAiBridge.Extensions;
using UnityAiBridge.Logger;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Object
    {
        public const string ObjectGetDataToolId = "object-get-data";
        [BridgeTool
        (
            ObjectGetDataToolId,
            Title = "Object / Get Data"
        )]
        [Description("Get data of the specified Unity Object. " +
            "Returns serialized data of the object including its properties and fields. " +
            "If need to modify the data use '" + ObjectModifyToolId + "' tool.")]
        public SerializedMember? GetData
        (
            ObjectRef objectRef
        )
        {
            if (objectRef == null)
                throw new ArgumentNullException(nameof(objectRef));

            if (!objectRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(objectRef));

            return UnityAiBridge.Utils.MainThread.Instance.Run(() =>
            {
                var obj = objectRef.FindObject();
                if (obj == null)
                    throw new Exception("Not found UnityEngine.Object with provided data.");

                return BridgePlugin.Reflector.Serialize(
                    obj,
                    name: obj.name,
                    recursive: true,
                    logger: BridgeLoggerFactory.CreateLogger<Tool_Object>()
                );
            });
        }
    }
}
