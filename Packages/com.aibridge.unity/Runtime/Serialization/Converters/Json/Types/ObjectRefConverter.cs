
#nullable enable
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityAiBridge.Data;

namespace UnityAiBridge.Serialization.Converters.Json
{
    public class ObjectRefConverter : JsonConverter<ObjectRef>
    {
        public override ObjectRef? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected start of object token.");

            var objectRef = new ObjectRef();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return objectRef;

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read(); // Move to the value token

                    switch (propertyName)
                    {
                        case ObjectRef.ObjectRefProperty.InstanceID:
                            objectRef.InstanceID = reader.GetInt32();
                            break;
                        default:
                            throw new JsonException($"Unexpected property name: {propertyName}. "
                                + $"Expected '{ObjectRef.ObjectRefProperty.InstanceID}'.");
                    }
                }
            }

            throw new JsonException("Expected end of object token.");
        }

        public override void Write(Utf8JsonWriter writer, ObjectRef value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteStartObject();

                // Write the "instanceID" property
                writer.WriteNumber(ObjectRef.ObjectRefProperty.InstanceID, 0);

                writer.WriteEndObject();
                return;
            }

            writer.WriteStartObject();

            // Write the "instanceID" property
            writer.WriteNumber(ObjectRef.ObjectRefProperty.InstanceID, value.InstanceID);

            writer.WriteEndObject();
        }
    }
}
