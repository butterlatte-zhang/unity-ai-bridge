#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using UnityAiBridge.Logger;
using UnityAiBridge.Utils;

namespace UnityAiBridge.Serialization
{
    /// <summary>
    /// Wraps a MethodInfo for parameter deserialization and invocation.
    /// </summary>
    public class MethodWrapper
    {
        private readonly BridgeReflector _reflector;
        private readonly MethodInfo _methodInfo;
        private readonly object? _targetInstance;
        private readonly IBridgeLogger? _logger;

        /// <summary>
        /// Constructor for static methods.
        /// </summary>
        public MethodWrapper(BridgeReflector reflector, IBridgeLogger? logger, MethodInfo methodInfo)
        {
            _reflector = reflector ?? throw new ArgumentNullException(nameof(reflector));
            _methodInfo = methodInfo ?? throw new ArgumentNullException(nameof(methodInfo));
            _logger = logger;
            _targetInstance = null;
        }

        /// <summary>
        /// Constructor for instance methods with a target object.
        /// </summary>
        public MethodWrapper(BridgeReflector reflector, IBridgeLogger? logger, object targetInstance, MethodInfo methodInfo)
        {
            _reflector = reflector ?? throw new ArgumentNullException(nameof(reflector));
            _targetInstance = targetInstance ?? throw new ArgumentNullException(nameof(targetInstance));
            _methodInfo = methodInfo ?? throw new ArgumentNullException(nameof(methodInfo));
            _logger = logger;
        }

        /// <summary>
        /// Verify that the provided named parameters match the method signature.
        /// </summary>
        public bool VerifyParameters(Dictionary<string, object?>? parameters, out string error)
        {
            var methodParams = _methodInfo.GetParameters();

            if (methodParams.Length == 0)
            {
                if (parameters == null || parameters.Count == 0)
                {
                    error = string.Empty;
                    return true;
                }
                error = $"Method '{_methodInfo.Name}' does not accept any parameters, but {parameters.Count} were provided.";
                return false;
            }

            if (parameters == null)
            {
                // Check if all parameters have defaults
                bool allOptional = methodParams.All(p => p.HasDefaultValue);
                if (allOptional)
                {
                    error = string.Empty;
                    return true;
                }
                error = $"Method '{_methodInfo.Name}' requires parameters, but none were provided.";
                return false;
            }

            foreach (var kvp in parameters)
            {
                var paramInfo = methodParams.FirstOrDefault(p => p.Name == kvp.Key);
                if (paramInfo == null)
                {
                    error = $"Method '{_methodInfo.Name}' does not have a parameter named '{kvp.Key}'.";
                    return false;
                }

                if (kvp.Value != null && !(kvp.Value is JsonElement) && !paramInfo.ParameterType.IsInstanceOfType(kvp.Value))
                {
                    error = $"Parameter '{kvp.Key}' type mismatch. Expected '{paramInfo.ParameterType.GetTypeId()}', but got '{kvp.Value.GetType()}'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Invoke the method with named parameters.
        /// </summary>
        public Task<object?> InvokeDict(Dictionary<string, object?> parameters)
        {
            var methodParams = _methodInfo.GetParameters();
            var args = BuildParameters(methodParams, parameters);
            return InvokeInternal(args);
        }

        /// <summary>
        /// Invoke the method without parameters.
        /// </summary>
        public Task<object?> Invoke()
        {
            var methodParams = _methodInfo.GetParameters();
            var args = new object?[methodParams.Length];

            for (int i = 0; i < methodParams.Length; i++)
            {
                if (methodParams[i].HasDefaultValue)
                    args[i] = methodParams[i].DefaultValue;
                else if (!methodParams[i].ParameterType.IsValueType)
                    args[i] = null;
                else
                    args[i] = Activator.CreateInstance(methodParams[i].ParameterType);
            }

            return InvokeInternal(args);
        }

        private async Task<object?> InvokeInternal(object?[] args)
        {
            object? target = _targetInstance;

            // For instance methods without a target, try to create one
            if (target == null && !_methodInfo.IsStatic)
            {
                var declaringType = _methodInfo.DeclaringType;
                if (declaringType != null)
                {
                    try
                    {
                        target = Activator.CreateInstance(declaringType);
                    }
                    catch (Exception e)
                    {
                        _logger?.LogWarning($"Failed to create instance of {declaringType.Name}: {e.Message}");
                        throw;
                    }
                }
            }

            object? result = _methodInfo.Invoke(target, args);

            // Handle async methods (Task, Task<T>)
            if (result is Task task)
            {
                await task.ConfigureAwait(false);
                var resultProperty = task.GetType().GetProperty("Result");
                return resultProperty?.GetValue(task);
            }

            return result;
        }

        private object?[] BuildParameters(ParameterInfo[] methodParams, Dictionary<string, object?> namedParameters)
        {
            var args = new object?[methodParams.Length];

            for (int i = 0; i < methodParams.Length; i++)
            {
                var param = methodParams[i];
                var paramType = Nullable.GetUnderlyingType(param.ParameterType) ?? param.ParameterType;

                if (namedParameters.TryGetValue(param.Name!, out var value))
                {
                    args[i] = ConvertParameterValue(param, paramType, value);
                }
                else if (param.HasDefaultValue)
                {
                    args[i] = param.DefaultValue;
                }
                else if (!param.ParameterType.IsValueType)
                {
                    args[i] = null;
                }
                else
                {
                    args[i] = Activator.CreateInstance(param.ParameterType);
                }
            }

            return args;
        }

        private object? ConvertParameterValue(ParameterInfo param, Type paramType, object? value)
        {
            if (value == null)
                return null;

            if (value is JsonElement jsonElement)
            {
                try
                {
                    return JsonSerializer.Deserialize(
                        jsonElement.GetRawText(),
                        param.ParameterType,
                        _reflector.JsonSerializerOptions);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"Failed to deserialize parameter '{param.Name}' from JSON: {ex.Message}");

                    // Try deserializing as SerializedMember, then use reflector
                    try
                    {
                        var member = JsonSerializer.Deserialize<SerializedMember>(
                            jsonElement.GetRawText(),
                            _reflector.JsonSerializerOptions);

                        if (member != null)
                            return _reflector.Deserialize(member, logger: null);
                    }
                    catch
                    {
                        // Fallback failed
                    }

                    throw new ArgumentException(
                        $"Unable to convert value to parameter '{param.Name}' of type '{param.ParameterType.GetTypeId()}'.\n" +
                        $"Input value: {jsonElement}\nOriginal exception: {ex.Message}");
                }
            }

            if (paramType.IsInstanceOfType(value))
                return value;

            if (paramType.IsEnum && value is string enumStr)
            {
                return Enum.Parse(paramType, enumStr, ignoreCase: true);
            }

            throw new ArgumentException(
                $"Parameter '{param.Name}' type mismatch. Expected '{param.ParameterType.GetTypeId()}', but got '{value.GetType()}'.");
        }
    }
}
