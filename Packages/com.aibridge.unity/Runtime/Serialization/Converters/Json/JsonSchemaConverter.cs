#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UnityAiBridge.Utils;

namespace UnityAiBridge.Serialization.Converters.Json
{
    /// <summary>
    /// JSON schema generation for bridge tool parameters.
    /// </summary>
    public static class JsonSchema
    {
        public const string Type = "type";
        public const string Object = "object";
        public const string Description = "description";
        public const string Properties = "properties";
        public const string Pattern = "pattern";
        public const string Items = "items";
        public const string Array = "array";
        public const string AnyOf = "anyOf";
        public const string Required = "required";
        public const string Result = "result";
        public const string Error = "error";
        public const string AdditionalProperties = "additionalProperties";
        public const string Null = "null";
        public const string String = "string";
        public const string Integer = "integer";
        public const string Number = "number";
        public const string Boolean = "boolean";
        public const string Minimum = "minimum";
        public const string Maximum = "maximum";
        public const string Id = "$id";
        public const string Defs = "$defs";
        public const string Ref = "$ref";
        public const string RefValue = "#/$defs/";
    }

    /// <summary>
    /// Interface for JSON converters that provide schema information.
    /// </summary>
    public interface IJsonSchemaConverter
    {
        string Id { get; }
        JsonNode GetSchema();
        JsonNode GetSchemaRef();
        IEnumerable<Type> GetDefinedTypes();
    }

    /// <summary>
    /// Generic JSON schema converter.
    /// </summary>
    public abstract class JsonSchemaConverter<T> : JsonConverter<T>, IJsonSchemaConverter
    {
        private static readonly Type[] _emptyTypes = Array.Empty<Type>();

        public static string StaticId => typeof(T).GetTypeId();

        public virtual string Id => StaticId;

        public abstract JsonNode GetSchema();

        public abstract JsonNode GetSchemaRef();

        public virtual IEnumerable<Type> GetDefinedTypes()
        {
            return _emptyTypes;
        }
    }
}
