#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace UnityAiBridge.Serialization
{
    /// <summary>
    /// Hierarchical serialization node for Unity objects.
    /// </summary>
    public class SerializedMember
    {
        public string? name { get; set; }
        public string? typeName { get; set; }
        public JsonElement? value { get; set; }
        public List<SerializedMember>? fields { get; set; }
        public List<SerializedMember>? props { get; set; }
        public int? instanceID { get; set; }

        /// <summary>
        /// Mutable JSON element wrapper for in-place property modifications.
        /// </summary>
        public MutableJsonElement valueJsonElement => _valueJsonElement ??= new MutableJsonElement(this);
        private MutableJsonElement? _valueJsonElement;

        public SerializedMember() { }

        public SerializedMember(string? name, string? typeName, JsonElement? value = null)
        {
            this.name = name;
            this.typeName = typeName;
            this.value = value;
        }

        /// <summary>
        /// Get the deserialized value of this member.
        /// </summary>
        public T? GetValue<T>(BridgeReflector reflector)
        {
            if (value == null)
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(value.Value.GetRawText(), reflector.JsonSerializerOptions);
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// Get a nested field by name.
        /// </summary>
        public SerializedMember? GetField(string fieldName)
        {
            return fields?.FirstOrDefault(f => f.name == fieldName)
                ?? props?.FirstOrDefault(p => p.name == fieldName);
        }

        /// <summary>
        /// Serialize this member to JSON string.
        /// </summary>
        public string ToJson(BridgeReflector reflector)
        {
            return JsonSerializer.Serialize(this, reflector.JsonSerializerOptions);
        }

        /// <summary>
        /// Serialize this member to JSON string with default options.
        /// </summary>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, BridgeReflector.DefaultJsonOptions);
        }
    }

    /// <summary>
    /// Extension methods for SerializedMember collections.
    /// </summary>
    public static class SerializedMemberListExtensions
    {
        public static string ToJson(this IEnumerable<SerializedMember> members, BridgeReflector reflector)
        {
            return JsonSerializer.Serialize(members, reflector.JsonSerializerOptions);
        }

        public static string ToJson(this IEnumerable<SerializedMember> members)
        {
            return JsonSerializer.Serialize(members, BridgeReflector.DefaultJsonOptions);
        }
        // public static string ToJson(this IEnumerable<SerializedMember?> members, BridgeReflector reflector)
        // {
        //     return JsonSerializer.Serialize(members, reflector.JsonSerializerOptions);
        // }
    }

    /// <summary>
    /// Generic JSON serialization extension.
    /// </summary>
    public static class JsonExtensions
    {
        public static string ToJson<T>(this T obj, BridgeReflector reflector)
        {
            return JsonSerializer.Serialize(obj, reflector.JsonSerializerOptions);
        }

        public static string ToJson<T>(this T obj)
        {
            return JsonSerializer.Serialize(obj, BridgeReflector.DefaultJsonOptions);
        }
    }

    /// <summary>
    /// Mutable wrapper around a SerializedMember's value JsonElement.
    /// Allows in-place property modifications by rebuilding the underlying JSON.
    /// </summary>
    public class MutableJsonElement
    {
        private readonly SerializedMember _owner;

        public MutableJsonElement(SerializedMember owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// Set or override a property in the underlying JSON value object.
        /// If the value is not a JSON object, it will be replaced with one containing the property.
        /// </summary>
        public void SetProperty(string propertyName, int propertyValue)
        {
            var dict = new Dictionary<string, object?>();

            // Parse existing value if it's a JSON object
            if (_owner.value.HasValue && _owner.value.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in _owner.value.Value.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.Clone();
                }
            }

            dict[propertyName] = propertyValue;

            var json = JsonSerializer.Serialize(dict, BridgeReflector.DefaultJsonOptions);
            _owner.value = JsonDocument.Parse(json).RootElement.Clone();
        }

        /// <summary>
        /// Set or override a property in the underlying JSON value object.
        /// </summary>
        public void SetProperty(string propertyName, string? propertyValue)
        {
            var dict = new Dictionary<string, object?>();

            if (_owner.value.HasValue && _owner.value.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in _owner.value.Value.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.Clone();
                }
            }

            dict[propertyName] = propertyValue;

            var json = JsonSerializer.Serialize(dict, BridgeReflector.DefaultJsonOptions);
            _owner.value = JsonDocument.Parse(json).RootElement.Clone();
        }
    }
}
