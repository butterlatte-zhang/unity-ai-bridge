
#nullable enable
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using UnityAiBridge;
using UnityAiBridge.Data;
using UnityAiBridge.Extensions;
using UnityAiBridge.Logger;
using UnityAiBridge.Serialization;
using UnityAiBridge.Utils;

namespace UnityAiBridge.Editor.Tools
{
    public partial class Tool_Reflection
    {
        public const string ReflectionMethodCallToolId = "reflection-method-call";
        [BridgeTool
        (
            ReflectionMethodCallToolId,
            Title = "Method C# / Call"
        )]
        [Description("Call C# method. Any method could be called, even private methods. " +
            "It requires to receive proper method schema. " +
            "Use '" + ReflectionMethodFindToolId + "' to find available method before using it. " +
            "Receives input parameters and returns result.")]
        public SerializedMember MethodCall
        (
            MethodRef filter,

            [Description("Set to true if 'Namespace' is known and full namespace name is specified in the 'filter.Namespace' property. Otherwise, set to false.")]
            bool knownNamespace = false,

            [Description("Minimal match level for 'typeName'. " +
                "0 - ignore 'filter.typeName', " +
                "1 - contains ignoring case (default value), " +
                "2 - contains case sensitive, " +
                "3 - starts with ignoring case, " +
                "4 - starts with case sensitive, " +
                "5 - equals ignoring case, " +
                "6 - equals case sensitive.")]
            int typeNameMatchLevel = 1,

            [Description("Minimal match level for 'MethodName'. " +
                "0 - ignore 'filter.MethodName', " +
                "1 - contains ignoring case (default value), " +
                "2 - contains case sensitive, " +
                "3 - starts with ignoring case, " +
                "4 - starts with case sensitive, " +
                "5 - equals ignoring case, " +
                "6 - equals case sensitive.")]
            int methodNameMatchLevel = 1,

            [Description("Minimal match level for 'Parameters'. " +
                "0 - ignore 'filter.Parameters', " +
                "1 - parameters count is the same, " +
                "2 - equals (default value).")]
            int parametersMatchLevel = 2,

            [Description("Specify target object to call method on. " +
                "Should be null if the method is static or if there is no specific target instance. " +
                "New instance of the specified class will be created if the method is instance method and the targetObject is null. " +
                "Required: type - full type name of the object to call method on, " +
                "value - serialized object value (it will be deserialized to the specified type).")]
            SerializedMember? targetObject = null,

            [Description("Method input parameters. " +
                "Per each parameter specify: type - full type name of the object to call method on, " +
                "name - parameter name, " +
                "value - serialized object value (it will be deserialized to the specified type).")]
            SerializedMemberList? inputParameters = null,

            [Description("Set to true if the method should be executed in the main thread. " +
                "Otherwise, set to false.")]
            bool executeInMainThread = true
        )
        {
            // Enhance filter with input parameters if no input parameters specified in the filter.
            if ((filter.InputParameters?.Count ?? 0) == 0 && (inputParameters?.Count ?? 0) > 0)
                filter.EnhanceInputParameters(inputParameters);

            var methodEnumerable = FindMethods(
                filter: filter,
                knownNamespace: knownNamespace,
                typeNameMatchLevel: typeNameMatchLevel,
                methodNameMatchLevel: methodNameMatchLevel,
                parametersMatchLevel: parametersMatchLevel
            );

            var methods = methodEnumerable.ToList();
            if (methods.Count == 0)
                throw new Exception($"Method not found.\n{filter}");

            var method = default(MethodInfo);

            if (methods.Count > 1)
            {
                // 无输入参数时，优先选择无参方法
                if (inputParameters == null || inputParameters.Count == 0)
                {
                    method = methods.FirstOrDefault(m => m.GetParameters().Length == 0);
                }

                if (method == null)
                {
                    var isValidParameterTypeName = inputParameters.IsValidTypeNames(
                        fieldName: nameof(inputParameters),
                        out var error
                    );

                    // Lets try to filter methods by parameters
                    method = isValidParameterTypeName
                        ? methods.FilterByParameters(inputParameters)
                        : null;
                }

                if (method == null)
                    throw new Exception(Error.MoreThanOneMethodFound(methods));
            }
            else
            {
                method = methods.First();
            }

            inputParameters?.EnhanceNames(method);
            inputParameters?.EnhanceTypes(method);

            // if (!inputParameters.IsMatch(method, out var matchError))
            //     return $"[Error] {matchError}";

            Func<SerializedMember> action = () =>
            {
                var reflector = BridgePlugin.Reflector;

                var logger = BridgeLoggerFactory.CreateLogger("Tool_Reflection.MethodCall");

                var dictInputParameters = inputParameters?.ToDictionary(
                    keySelector: p => p.name ?? throw new InvalidOperationException($"Input parameter name is null. Please specify 'name' property for each input parameter."),
                    elementSelector: p => reflector.Deserialize(p, logger: logger)
                );

                var methodWrapper = default(MethodWrapper);

                if (string.IsNullOrEmpty(targetObject?.typeName))
                {
                    // No object instance needed. Probably static method.
                    methodWrapper = new MethodWrapper(reflector, logger: logger, method);
                }
                else
                {
                    // Object instance needed. Probably instance method.
                    var obj = reflector.Deserialize(targetObject!, logger: logger);
                    if (obj == null)
                        throw new Exception($"'{nameof(targetObject)}' deserialized instance is null. Please specify the '{nameof(targetObject)}' properly.");

                    methodWrapper = new MethodWrapper(
                        reflector: reflector,
                        logger: logger,
                        targetInstance: obj,
                        methodInfo: method);
                }

                if (!methodWrapper.VerifyParameters(dictInputParameters, out var error))
                    throw new Exception(error);

                var task = dictInputParameters != null
                    ? methodWrapper.InvokeDict(dictInputParameters)
                    : methodWrapper.Invoke();

                var result = task.Result;
                if (result is SerializedMember serializedResult)
                    return serializedResult;

                return reflector.Serialize(
                    obj: result,
                    fallbackType: method.ReturnType,
                    logger: BridgeLoggerFactory.CreateLogger("Tool_Reflection.MethodCall")
                );
            };

            if (executeInMainThread)
                return UnityAiBridge.Utils.MainThread.Instance.Run(action);

            return action();
        }
    }
}
