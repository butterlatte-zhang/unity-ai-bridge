#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using UnityAiBridge.Serialization;
using UnityEngine;

namespace UnityAiBridge.Editor
{
    /// <summary>
    /// Executes Bridge tools by name with JSON parameters.
    /// </summary>
    public class BridgeToolRunner
    {
        private readonly BridgeToolRegistry _registry;
        private readonly BridgeReflector _reflector;

        public BridgeToolRunner(BridgeToolRegistry registry, BridgeReflector reflector)
        {
            _registry = registry;
            _reflector = reflector;
        }

        /// <summary>
        /// Execute a tool by name with JSON parameters.
        /// Returns a BridgeToolResult with status and serialized message.
        /// If the tool method returns a Task, the result will have Status=Pending and AsyncTask set.
        /// The caller must poll AsyncTask.IsCompleted and then call SerializeTaskResult().
        /// </summary>
        public BridgeToolResult Execute(string toolName, Dictionary<string, JsonElement>? parameters)
        {
            var tool = _registry.GetTool(toolName);
            if (tool == null)
                return BridgeToolResult.Error($"Tool not found: {toolName}");

            try
            {
                var instance = tool.IsStatic ? null : _registry.GetOrCreateInstance(tool.DeclaringType);
                var args = DeserializeArguments(tool, parameters);
                var result = tool.Method.Invoke(instance, args);

                // 异步方法返回 Task/Task<T>，不能阻塞主线程等待结果，否则死锁。
                if (result is Task task)
                    return BridgeToolResult.Pending(task);

                return SerializeResult(result);
            }
            catch (System.Reflection.TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                Debug.LogError($"[BridgeToolRunner] Tool '{toolName}' threw: {inner.Message}\n{inner.StackTrace}");
                return BridgeToolResult.Error(inner.Message);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BridgeToolRunner] Failed to execute '{toolName}': {e.Message}\n{e.StackTrace}");
                return BridgeToolResult.Error(e.Message);
            }
        }

        /// <summary>
        /// 从已完成的 Task 中提取结果并序列化。
        /// 仅在 task.IsCompleted == true 时调用。
        /// </summary>
        public BridgeToolResult SerializeTaskResult(Task task)
        {
            try
            {
                if (task.IsFaulted)
                {
                    var ex = task.Exception?.InnerException ?? task.Exception;
                    return BridgeToolResult.Error(ex?.Message ?? "Async task faulted");
                }

                if (task.IsCanceled)
                    return BridgeToolResult.Error("Async task was canceled");

                // Task<T> → 通过反射获取 .Result
                var taskType = task.GetType();
                if (taskType.IsGenericType)
                {
                    var resultProp = taskType.GetProperty("Result");
                    if (resultProp != null && resultProp.PropertyType != typeof(void))
                    {
                        var result = resultProp.GetValue(task);
                        return SerializeResult(result);
                    }
                }

                // 非泛型 Task（无返回值）
                return BridgeToolResult.Success("null");
            }
            catch (Exception e)
            {
                return BridgeToolResult.Error($"Failed to serialize async result: {e.Message}");
            }
        }

        #region Argument Deserialization

        private object?[] DeserializeArguments(ToolEntry tool, Dictionary<string, JsonElement>? parameters)
        {
            var methodParams = tool.Method.GetParameters();
            var args = new object?[methodParams.Length];

            for (int i = 0; i < methodParams.Length; i++)
            {
                var paramInfo = methodParams[i];
                var paramName = paramInfo.Name!;

                // [RequestID] 参数：优先从 JSON 取，否则自动生成 GUID
                if (paramInfo.GetCustomAttribute<RequestIDAttribute>() != null)
                {
                    if (parameters != null && parameters.TryGetValue(paramName, out var reqIdElement)
                        && reqIdElement.ValueKind == JsonValueKind.String)
                    {
                        args[i] = reqIdElement.GetString();
                    }
                    else
                    {
                        args[i] = Guid.NewGuid().ToString("N");
                    }
                    continue;
                }

                if (parameters != null && parameters.TryGetValue(paramName, out var jsonElement))
                {
                    try
                    {
                        args[i] = DeserializeParameter(jsonElement, paramInfo.ParameterType);
                    }
                    catch (Exception e)
                    {
                        throw new ArgumentException(
                            $"Failed to deserialize parameter '{paramName}' " +
                            $"(expected {paramInfo.ParameterType.Name}): {e.Message}", paramName, e);
                    }
                }
                else if (paramInfo.HasDefaultValue)
                {
                    args[i] = paramInfo.DefaultValue;
                }
                else
                {
                    throw new ArgumentException(
                        $"Required parameter '{paramName}' not provided.", paramName);
                }
            }

            return args;
        }

        /// <summary>
        /// 反序列化单个参数，特殊处理枚举类型和 System.Type 类型。
        /// </summary>
        private object? DeserializeParameter(JsonElement jsonElement, Type targetType)
        {
            // 处理 System.Type / Nullable<Type>：从字符串名称解析类型
            var actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (actualType == typeof(Type))
            {
                if (jsonElement.ValueKind == JsonValueKind.Null)
                    return null;
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    var typeName = jsonElement.GetString();
                    if (string.IsNullOrEmpty(typeName))
                        return null;
                    var resolved = UnityAiBridge.Utils.TypeUtils.GetType(typeName);
                    if (resolved == null)
                        throw new ArgumentException($"Cannot resolve type '{typeName}'. Ensure the type name is correct (e.g. 'Shader', 'UnityEngine.Shader', 'Material').");
                    return resolved;
                }
            }

            // 处理 Nullable<Enum>：先拆包再按枚举处理
            var underlyingType = Nullable.GetUnderlyingType(targetType);
            if (underlyingType != null && underlyingType.IsEnum)
            {
                if (jsonElement.ValueKind == JsonValueKind.Null)
                    return null;
                if (jsonElement.ValueKind == JsonValueKind.Number)
                    return Enum.ToObject(underlyingType, jsonElement.GetInt32());
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    var str = jsonElement.GetString();
                    if (str != null)
                    {
                        try { return Enum.Parse(underlyingType, str, ignoreCase: true); }
                        catch (ArgumentException) { /* fall through */ }
                    }
                }
            }

            // 处理非 Nullable 的 Enum
            if (targetType.IsEnum)
            {
                if (jsonElement.ValueKind == JsonValueKind.Number)
                    return Enum.ToObject(targetType, jsonElement.GetInt32());
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    var str = jsonElement.GetString();
                    if (str != null)
                    {
                        try { return Enum.Parse(targetType, str, ignoreCase: true); }
                        catch (ArgumentException) { /* fall through */ }
                    }
                }
            }

            // 默认走 JsonSerializer
            return JsonSerializer.Deserialize(
                jsonElement.GetRawText(), targetType, _reflector.JsonSerializerOptions);
        }

        #endregion

        #region Result Serialization

        private BridgeToolResult SerializeResult(object? result)
        {
            if (result == null)
                return BridgeToolResult.Success("null");

            try
            {
                var json = JsonSerializer.Serialize(result, result.GetType(), _reflector.JsonSerializerOptions);
                return BridgeToolResult.Success(json);
            }
            catch (Exception e)
            {
                // Fallback: try ToString
                return BridgeToolResult.Success(result.ToString() ?? "");
            }
        }

        #endregion
    }

    /// <summary>
    /// Result of a tool execution.
    /// </summary>
    public class BridgeToolResult
    {
        public BridgeToolStatus Status { get; set; }
        public string Message { get; set; } = "";

        /// <summary>
        /// 异步工具的 Task 引用。仅当 Status == Pending 时有值。
        /// </summary>
        public Task? AsyncTask { get; set; }

        public static BridgeToolResult Success(string message)
            => new() { Status = BridgeToolStatus.Success, Message = message };

        public static BridgeToolResult Error(string message)
            => new() { Status = BridgeToolStatus.Error, Message = message };

        public static BridgeToolResult Pending(Task task)
            => new() { Status = BridgeToolStatus.Pending, AsyncTask = task };
    }

    public enum BridgeToolStatus
    {
        Success,
        Error,
        Pending
    }
}
