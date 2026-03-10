#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityAiBridge.Logger;
using UnityEngine;

namespace UnityAiBridge.Serialization
{
    /// <summary>
    /// Reflection-based serializer for Unity objects.
    /// Serializes/deserializes Unity objects to/from SerializedMember trees.
    /// </summary>
    public class BridgeReflector
    {
        public static readonly JsonSerializerOptions DefaultJsonOptions = CreateDefaultOptions();

        public JsonSerializerOptions JsonSerializerOptions { get; }
        public ConverterCollection Converters { get; } = new();
        public JsonConverterCollection JsonSerializer { get; }

        private static readonly HashSet<Type> _blacklistedTypes = new();

        public BridgeReflector()
        {
            JsonSerializerOptions = CreateDefaultOptions();
            JsonSerializer = new JsonConverterCollection(JsonSerializerOptions);
        }

        private static JsonSerializerOptions CreateDefaultOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        #region Serialize

        /// <summary>
        /// Serialize a Unity object into a SerializedMember tree.
        /// </summary>
        public SerializedMember? Serialize(
            object? obj,
            string? name = null,
            Type? fallbackType = null,
            bool recursive = false,
            IBridgeLogger? logger = null)
        {
            if (obj == null)
                return null;

            var type = obj.GetType();
            var typeName = type.FullName ?? type.Name;

            // Check for custom reflection converter
            var converter = Converters.FindConverter(type);
            if (converter != null)
            {
                try
                {
                    return converter.Serialize(obj, name, recursive, this, logger);
                }
                catch (Exception e)
                {
                    logger?.LogWarning($"Converter failed for {typeName}: {e.Message}");
                }
            }

            var member = new SerializedMember
            {
                name = name,
                typeName = typeName
            };

            // For UnityEngine.Object, add instanceID
            if (obj is UnityEngine.Object unityObj)
            {
                member.instanceID = unityObj.GetInstanceID();
            }

            // Serialize fields
            var fields = GetSerializableFields(type);
            if (fields.Count > 0)
            {
                member.fields = new List<SerializedMember>();
                foreach (var field in fields)
                {
                    try
                    {
                        var fieldValue = field.GetValue(obj);
                        var fieldMember = SerializeValue(fieldValue, field.Name, field.FieldType, recursive, logger);
                        if (fieldMember != null)
                            member.fields.Add(fieldMember);
                    }
                    catch (Exception e)
                    {
                        logger?.LogWarning($"Failed to serialize field {field.Name}: {e.Message}");
                    }
                }
            }

            // Serialize properties
            var props = GetSerializableProperties(type);
            if (props.Count > 0)
            {
                member.props = new List<SerializedMember>();
                foreach (var prop in props)
                {
                    try
                    {
                        var propValue = prop.GetValue(obj);
                        var propMember = SerializeValue(propValue, prop.Name, prop.PropertyType, recursive, logger);
                        if (propMember != null)
                            member.props.Add(propMember);
                    }
                    catch (Exception e)
                    {
                        logger?.LogWarning($"Failed to serialize property {prop.Name}: {e.Message}");
                    }
                }
            }

            return member;
        }

        private SerializedMember? SerializeValue(object? value, string name, Type declaredType, bool recursive, IBridgeLogger? logger)
        {
            if (value == null)
                return new SerializedMember(name, declaredType.FullName ?? declaredType.Name);

            if (_blacklistedTypes.Contains(value.GetType()) || _blacklistedTypes.Contains(declaredType))
                return null;

            // Primitives, strings, enums → serialize directly to JsonElement
            var type = value.GetType();
            if (type.IsPrimitive || type == typeof(string) || type.IsEnum || type == typeof(decimal))
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(value, JsonSerializerOptions);
                    var element = JsonDocument.Parse(json).RootElement.Clone();
                    return new SerializedMember(name, type.FullName ?? type.Name, element);
                }
                catch
                {
                    return new SerializedMember(name, type.FullName ?? type.Name);
                }
            }

            // Unity value types (Vector3, Color, etc.) → serialize to JsonElement
            if (type.IsValueType && type.Namespace?.StartsWith("UnityEngine") == true)
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(value, type, JsonSerializerOptions);
                    var element = JsonDocument.Parse(json).RootElement.Clone();
                    return new SerializedMember(name, type.FullName ?? type.Name, element);
                }
                catch
                {
                    return new SerializedMember(name, type.FullName ?? type.Name);
                }
            }

            // Recursive: serialize as nested SerializedMember
            if (recursive && !(value is IEnumerable && value is not string))
            {
                return Serialize(value, name, recursive: false, logger: logger);
            }

            // Collections
            if (value is IEnumerable enumerable && value is not string)
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(value, type, JsonSerializerOptions);
                    var element = JsonDocument.Parse(json).RootElement.Clone();
                    return new SerializedMember(name, type.FullName ?? type.Name, element);
                }
                catch
                {
                    return new SerializedMember(name, type.FullName ?? type.Name);
                }
            }

            // Default: try JSON serialization
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(value, type, JsonSerializerOptions);
                var element = JsonDocument.Parse(json).RootElement.Clone();
                return new SerializedMember(name, type.FullName ?? type.Name, element);
            }
            catch
            {
                return new SerializedMember(name, type.FullName ?? type.Name);
            }
        }

        #endregion

        #region Deserialize / Populate

        /// <summary>
        /// Try to populate an object's fields/properties from a SerializedMember.
        /// </summary>
        public bool TryPopulate(SerializedMember member, object target)
        {
            if (member == null || target == null)
                return false;

            var type = target.GetType();
            var success = true;

            // Populate fields
            if (member.fields != null)
            {
                foreach (var fieldMember in member.fields)
                {
                    if (string.IsNullOrEmpty(fieldMember.name))
                        continue;

                    var field = type.GetField(fieldMember.name,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (field == null || field.IsLiteral)
                        continue;

                    try
                    {
                        if (fieldMember.value.HasValue)
                        {
                            var value = System.Text.Json.JsonSerializer.Deserialize(
                                fieldMember.value.Value.GetRawText(), field.FieldType, JsonSerializerOptions);
                            field.SetValue(target, value);
                        }
                    }
                    catch
                    {
                        success = false;
                    }
                }
            }

            // Populate properties
            if (member.props != null)
            {
                foreach (var propMember in member.props)
                {
                    if (string.IsNullOrEmpty(propMember.name))
                        continue;

                    var prop = type.GetProperty(propMember.name,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (prop == null || !prop.CanWrite)
                        continue;

                    try
                    {
                        if (propMember.value.HasValue)
                        {
                            var value = System.Text.Json.JsonSerializer.Deserialize(
                                propMember.value.Value.GetRawText(), prop.PropertyType, JsonSerializerOptions);
                            prop.SetValue(target, value);
                        }
                    }
                    catch
                    {
                        success = false;
                    }
                }
            }

            return success;
        }

        /// <summary>
        /// Try to populate an object's fields/properties from a SerializedMember, with logging support.
        /// This overload supports ref target for potential object replacement and detailed logging.
        /// </summary>
        public bool TryPopulate(ref object target, SerializedMember data, Logs? logs = null, IBridgeLogger? logger = null)
        {
            if (data == null || target == null)
                return false;

            var type = target.GetType();
            var anyModified = false;

            // Check for custom converter
            var converter = Converters.FindConverter(type);
            if (converter != null)
            {
                // Custom converters don't support populate - log and skip
                logger?.LogWarning($"Custom converter found for {type.Name}, populate may be limited.");
            }

            // Populate fields
            if (data.fields != null)
            {
                foreach (var fieldMember in data.fields)
                {
                    if (string.IsNullOrEmpty(fieldMember.name))
                        continue;

                    var field = type.GetField(fieldMember.name,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (field == null || field.IsLiteral)
                    {
                        logs?.Warning($"Field '{fieldMember.name}' not found or is constant on type '{type.Name}'.");
                        continue;
                    }

                    try
                    {
                        if (fieldMember.value.HasValue)
                        {
                            var value = System.Text.Json.JsonSerializer.Deserialize(
                                fieldMember.value.Value.GetRawText(), field.FieldType, JsonSerializerOptions);
                            field.SetValue(target, value);
                            logs?.Add($"Modified field '{fieldMember.name}' on '{type.Name}'.");
                            anyModified = true;
                        }
                        else if (fieldMember.fields != null || fieldMember.props != null)
                        {
                            // Nested object populate
                            var nestedObj = field.GetValue(target);
                            if (nestedObj != null)
                            {
                                var nestedSuccess = TryPopulate(ref nestedObj, fieldMember, logs, logger);
                                if (nestedSuccess)
                                {
                                    field.SetValue(target, nestedObj);
                                    anyModified = true;
                                }
                            }
                            else
                            {
                                logs?.Warning($"Field '{fieldMember.name}' value is null, cannot populate nested object.");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logs?.Error($"Failed to set field '{fieldMember.name}': {e.Message}");
                        logger?.LogWarning($"Failed to set field '{fieldMember.name}' on '{type.Name}': {e.Message}");
                    }
                }
            }

            // Populate properties
            if (data.props != null)
            {
                foreach (var propMember in data.props)
                {
                    if (string.IsNullOrEmpty(propMember.name))
                        continue;

                    var prop = type.GetProperty(propMember.name,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (prop == null || !prop.CanWrite)
                    {
                        logs?.Warning($"Property '{propMember.name}' not found or is read-only on type '{type.Name}'.");
                        continue;
                    }

                    try
                    {
                        if (propMember.value.HasValue)
                        {
                            var value = System.Text.Json.JsonSerializer.Deserialize(
                                propMember.value.Value.GetRawText(), prop.PropertyType, JsonSerializerOptions);
                            prop.SetValue(target, value);
                            logs?.Add($"Modified property '{propMember.name}' on '{type.Name}'.");
                            anyModified = true;
                        }
                        else if (propMember.fields != null || propMember.props != null)
                        {
                            // Nested object populate
                            var nestedObj = prop.GetValue(target);
                            if (nestedObj != null)
                            {
                                var nestedSuccess = TryPopulate(ref nestedObj, propMember, logs, logger);
                                if (nestedSuccess)
                                {
                                    prop.SetValue(target, nestedObj);
                                    anyModified = true;
                                }
                            }
                            else
                            {
                                logs?.Warning($"Property '{propMember.name}' value is null, cannot populate nested object.");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logs?.Error($"Failed to set property '{propMember.name}': {e.Message}");
                        logger?.LogWarning($"Failed to set property '{propMember.name}' on '{type.Name}': {e.Message}");
                    }
                }
            }

            return anyModified;
        }

        /// <summary>
        /// Deserialize a SerializedMember back into an object.
        /// </summary>
        public object? Deserialize(SerializedMember data, IBridgeLogger? logger = null)
        {
            if (data == null)
                return null;

            if (string.IsNullOrEmpty(data.typeName))
            {
                // No type info, try returning raw value
                if (data.value.HasValue)
                    return data.value.Value;
                return null;
            }

            // Resolve the type
            var type = Utils.TypeUtils.GetType(data.typeName);
            if (type == null)
            {
                logger?.LogWarning($"Cannot resolve type '{data.typeName}' for deserialization.");
                if (data.value.HasValue)
                    return data.value.Value;
                return null;
            }

            // If there's a direct value, deserialize it
            if (data.value.HasValue)
            {
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize(
                        data.value.Value.GetRawText(), type, JsonSerializerOptions);
                }
                catch (Exception e)
                {
                    logger?.LogWarning($"Failed to deserialize value for '{data.name}' as {data.typeName}: {e.Message}");
                }
            }

#if UNITY_EDITOR
            // If it has instanceID, try to find the Unity object
            if (data.instanceID.HasValue && data.instanceID.Value != 0)
            {
                var obj = UnityEditor.EditorUtility.InstanceIDToObject(data.instanceID.Value);
                if (obj != null)
                {
                    // If fields/props are provided, populate them
                    if ((data.fields?.Count ?? 0) > 0 || (data.props?.Count ?? 0) > 0)
                        TryPopulate(data, obj);
                    return obj;
                }
            }
#endif

            // Create instance and populate fields/props
            try
            {
                var instance = Activator.CreateInstance(type);
                if (instance != null)
                    TryPopulate(data, instance);
                return instance;
            }
            catch (Exception e)
            {
                logger?.LogWarning($"Failed to create instance of {data.typeName}: {e.Message}");
                return null;
            }
        }

        #endregion

        #region Reflection Helpers

        private static List<FieldInfo> GetSerializableFields(Type type)
        {
            return type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !f.IsLiteral && !f.IsInitOnly)
                .Where(f => !IsBlacklisted(f.FieldType))
                .ToList();
        }

        private static List<PropertyInfo> GetSerializableProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .Where(p => !IsBlacklisted(p.PropertyType))
                .ToList();
        }

        private static bool IsBlacklisted(Type type)
        {
            if (_blacklistedTypes.Contains(type))
                return true;
            if (type.IsGenericType && _blacklistedTypes.Contains(type.GetGenericTypeDefinition()))
                return true;
            return false;
        }

        #endregion
    }

    #region Converter Infrastructure

    /// <summary>
    /// Interface for custom reflection converters.
    /// </summary>
    public interface IReflectionConverter
    {
        bool CanConvert(Type type);
        SerializedMember? Serialize(object obj, string? name, bool recursive, BridgeReflector reflector, IBridgeLogger? logger);
    }

    /// <summary>
    /// Collection of reflection converters with type matching.
    /// </summary>
    public class ConverterCollection
    {
        private readonly List<IReflectionConverter> _converters = new();
        private readonly HashSet<Type> _blacklist = new();

        public void Add(IReflectionConverter converter)
        {
            _converters.Insert(0, converter); // latest added has priority
        }

        public void Remove<T>() where T : IReflectionConverter
        {
            _converters.RemoveAll(c => c is T);
        }

        public IReflectionConverter? FindConverter(Type type)
        {
            if (_blacklist.Contains(type))
                return null;

            return _converters.FirstOrDefault(c => c.CanConvert(type));
        }

        public void BlacklistType(Type type)
        {
            _blacklist.Add(type);
        }

        public void BlacklistTypeInAssembly(string assemblyNamePrefix, string typeFullName)
        {
            // Deferred blacklist - checked during serialization
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.GetName().Name?.StartsWith(assemblyNamePrefix) == true)
                    continue;

                var type = asm.GetType(typeFullName);
                if (type != null)
                    _blacklist.Add(type);
            }
        }

        public void BlacklistTypesInAssembly(string assemblyNamePrefix, string[] typeFullNames)
        {
            foreach (var name in typeFullNames)
                BlacklistTypeInAssembly(assemblyNamePrefix, name);
        }
    }

    /// <summary>
    /// Manages JSON converters for System.Text.Json.
    /// </summary>
    public class JsonConverterCollection
    {
        private readonly JsonSerializerOptions _options;

        public JsonConverterCollection(JsonSerializerOptions options)
        {
            _options = options;
        }

        public void AddConverter(System.Text.Json.Serialization.JsonConverter converter)
        {
            _options.Converters.Add(converter);
        }

        public T? Deserialize<T>(JsonElement element)
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(element.GetRawText(), _options);
        }

        public T? Deserialize<T>(string json)
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(json, _options);
        }

        /// <summary>
        /// Serialize an object to a JsonElement using configured converters.
        /// </summary>
        public JsonElement SerializeToNode<T>(T value)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(value, _options);
            return JsonDocument.Parse(json).RootElement.Clone();
        }
    }

    #endregion
}
