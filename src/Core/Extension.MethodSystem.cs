using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using static ArmaExtension.Extension;
using static ArmaExtension.Logger;
using static ArmaExtension.Enums;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ArmaExtension;

public static partial class MethodSystem {
    public class AnnotatedType(Type type) {
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
        public Type Type { get; } = type;
    }

    private static readonly List<AnnotatedType> MethodContainers = [];

    /// <summary>
    /// Registers a static class containing methods to be called by the extension.
    /// Supports multiple method containers.
    /// </summary>
    /// <param name="methodsType">The type of the static class that contains public static methods.</param>
    public static void RegisterMethods(Type methodsType) {
        ArgumentNullException.ThrowIfNull(methodsType);

        if (!methodsType.IsClass || !methodsType.IsAbstract || !methodsType.IsSealed)
            throw new ArgumentException("Method container must be a static class.", nameof(methodsType));

        if (MethodContainers.Exists(x => x.Type == methodsType)) return;

        MethodContainers.Add(new AnnotatedType(methodsType));

        PrintMethodInfo();
    }
    

    internal static int HandleExecuteExtensionMethod(nint output, int outputSize, string method, string[]? argArray = null) {
        try {
            argArray ??= [];

            int pipeIndex = method.IndexOf('|');
            string originalMethod = pipeIndex >= 0 ? method[..pipeIndex] : method;
            if (string.IsNullOrEmpty(originalMethod)) throw new Exception("Invalid Method");

            int asyncKey = 0;
            bool async = pipeIndex >= 0 && int.TryParse(method[(pipeIndex + 1)..], out asyncKey);

            // Add method info to context
            ExtensionContext = (ExtensionContext ?? new ExtensionCallContext()) with { MethodName = method, AsyncKey = null };

            // Handle internal tool requests
            if (Enum.TryParse<ExtensionResultCode>(originalMethod, true, out var code)) {
                switch (code) {
                    case ExtensionResultCode.ASYNC_CANCEL:
                        return HandleAsyncCancel(output, outputSize, originalMethod, asyncKey);

                    case ExtensionResultCode.ASYNC_STATUS:
                        return HandleAsyncStatus(output, outputSize, originalMethod, asyncKey);

                    case ExtensionResultCode.GET_AVAILABLE_METHODS:
                        return HandleGetMethods(output, outputSize, originalMethod);
                    
                    case ExtensionResultCode.MANAGE_FLAGS:
                        return HandleManageFlags(output, outputSize, originalMethod, argArray);
                }
            }

            if (!MethodExists(originalMethod)) throw new Exception($"Method {originalMethod} not found!");

            // Execute method
            if (asyncKey >= 0) {
                return ExecuteAsyncMethod(originalMethod, argArray, asyncKey, output, outputSize);
            } else {
                return ExecuteSyncMethod(originalMethod, argArray, output, outputSize);
            }

        } catch (Exception ex) {
            ex = ex.InnerException ?? ex;
            Events.RaiseErrorOccurred(ex);

            return WriteOutput(output, outputSize, method,
                $@"[""{ExtensionResultCode.ERROR}"",[""{ex.Message}""]]",
                (int)ReturnCodes.Error);
        }
    }

    private static int ExecuteSyncMethod(string originalMethod, string[] argArray, nint output, int outputSize)
    {
        MethodInfo methodToInvoke = GetMethod(originalMethod);

        if (IsAsyncWithReturn(methodToInvoke)) {
            throw new Exception($"Method {methodToInvoke.Name} can only be called using async key!");
        }

        object?[] unserializedData = Serializer.DeserializeJsonArray(methodToInvoke, argArray);

        // Prepare parameters (truncate, validate, fill defaults)
        object?[] finalParams = Serializer.PrepareMethodParameters(methodToInvoke, unserializedData);

        bool isVoid = IsVoidMethod(methodToInvoke);

        object? returnValue = null;
        if (isVoid && IsAsync(methodToInvoke)) {
            // Fire and forget
            _ = Task.Run(() => methodToInvoke.Invoke(null, finalParams));
        } else {
            returnValue = methodToInvoke.Invoke(null, finalParams);
        }


        Events.RaiseMethodCalledWithArgsResponse(originalMethod, [returnValue], true);

        return WriteOutput(output, outputSize, originalMethod,
            $@"[""{ExtensionResultCode.SUCCESS}"",{(isVoid ? "[]" : Serializer.PrintArray([returnValue]))}]",
            (int)ReturnCodes.Success);
    }

    private static int HandleGetMethods(nint output, int outputSize, string originalMethod)
    {
        object[]? methodInfo = BuildArmaMethodList();

        return WriteOutput(output, outputSize, originalMethod,
            $@"[""{ExtensionResultCode.SUCCESS}"",{Serializer.PrintArray(methodInfo)}]",
            (int)ReturnCodes.Success);
    }

    private static int ExecuteAsyncMethod(string originalMethod, string[] argArray, int asyncKey, nint output, int outputSize) {
        MethodInfo methodToInvoke = GetMethod(originalMethod);

        bool success = AsyncFactory.ExecuteAsyncTask(methodToInvoke, argArray, asyncKey);

        if (!success) {
            // Task already running
            return WriteOutput(output, outputSize, originalMethod,
                $@"[""{ExtensionResultCode.ASYNC_SENT_FAILED}"",[""Task with asyncKey {asyncKey} is already running""]]",
                (int)ReturnCodes.Error);
        }

        bool isVoid = IsVoidMethod(methodToInvoke);

        string outputPayload = isVoid
            ? $@"[""{ExtensionResultCode.ASYNC_SENT_VOID}"",[]]"
            : $@"[""{ExtensionResultCode.ASYNC_SENT}"",[]]";

        return WriteOutput(output, outputSize, originalMethod, outputPayload, (int)ReturnCodes.Success);
    }

    internal static object[] BuildArmaMethodList() {
        var methodsArray = new List<object>();

        foreach (AnnotatedType? container in MethodContainers) {
            if (container?.Type == null) continue;

            foreach (var method in container.Type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
                if (method.Name == "Main") continue;

                var paramList = method.GetParameters()
                    .Where(p => p.Name != null) // skip parameters with null names
                    .Select(p => new object[] { p.Name ?? "Unknown", GetArmaParameterType(p.ParameterType), p.IsOptional })
                    .ToArray();

                Type returnTypeType = method.ReturnType;
                bool isAsync = false;

                // Handle Task and Task<T>
                if (returnTypeType == typeof(Task)) {
                    isAsync = true;
                    returnTypeType = typeof(void); // Task → nil
                } else if (returnTypeType.IsGenericType && returnTypeType.GetGenericTypeDefinition() == typeof(Task<>)) {
                    isAsync = true;
                    returnTypeType = returnTypeType.GetGenericArguments()[0];
                }

                string returnType = GetArmaParameterType(returnTypeType);

                methodsArray.Add(new object[] { method.Name, paramList, returnType, isAsync });
            }
        }

        return methodsArray.ToArray();
    }

    


    #region Helpers

    private static void PrintMethodInfo() {
        Debug("================ METHOD LIST ================");

        foreach (AnnotatedType? container in MethodContainers) {
            if (container?.Type == null) continue;

            foreach (var method in container.Type.GetMethods(
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)) {

                if (method.Name == "Main") continue;

                string parameters = string.Join(", ", method.GetParameters()
                    .Select(p => $"{p.ParameterType.Name} {p.Name}{(p.IsOptional ? " (optional)" : "")}"));

                string returnType;
                bool isAsync = false;

                if (method.ReturnType == typeof(Task)) {
                    returnType = "Void";
                    isAsync = true;
                } else if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)) {
                    returnType = method.ReturnType.GetGenericArguments()[0].Name;
                    isAsync = true;
                } else {
                    returnType = method.ReturnType.Name;
                }

                Debug($"Registered Method: {method.Name}({parameters}) --> {returnType}{(isAsync ? " [Async Only]" : "")}");
            }
        }

        Debug("=============================================");
    }


    /* 
        * Example usage of the MANAGE_FLAGS internal tool:

        * Get the current enabled feature flags for the extension
        callExtension ["MANAGE_FLAGS", "GET"];

        * Enable a specific feature flag (e.g., ContextStackTrace)
        callExtension ["MANAGE_FLAGS", "ENABLE", "ContextStackTrace"];

        * Disable a specific feature flag (e.g., ContextNoDefaultCall)
        callExtension ["MANAGE_FLAGS", "DISABLE", "ContextNoDefaultCall"];

        * Notes:
            - "GET" returns a comma-separated list of currently enabled flags.
            - "ENABLE" and "DISABLE" take the exact flag name (case-insensitive) as the second argument.
            - If an invalid flag name or unknown action is provided, an error message is returned.
    */
    internal static int HandleManageFlags(nint output, int outputSize, string method, string[] args) {
        // Usage: args[0] = "GET" | "ENABLE" | "DISABLE"
        //        args[1] = "ContextStackTrace" (optional for GET)
        if (args.Length == 0) return WriteOutput(output, outputSize, method,
            "Missing arguments",
            (int)ReturnCodes.Error);

        string action = args[0].ToUpperInvariant();

        switch (action) {
            case "GET":
                return WriteOutput(output, outputSize, method, FeatureFlags.ToString(), (int)ReturnCodes.Success);

            case "ENABLE":
                if (args.Length < 2) return WriteOutput(output, outputSize, method,
                    "Missing flag name",
                    (int)ReturnCodes.Error);

                if (Enum.TryParse<RVExtensionFeature>(args[1], true, out var enableFlag)) {
                    EnableFeature(enableFlag);
                    return WriteOutput(output, outputSize, method,
                        $"Enabled: {enableFlag}",
                        (int)ReturnCodes.Success);
                } else {
                    return WriteOutput(output, outputSize, method,
                        $"Error: Invalid flag {args[1]}",
                        (int)ReturnCodes.Error);
                }

            case "DISABLE":
                if (args.Length < 2) return WriteOutput(output, outputSize, method,
                    "Missing flag name",
                    (int)ReturnCodes.Error);

                if (Enum.TryParse<RVExtensionFeature>(args[1], true, out var disableFlag)) {
                    DisableFeature(disableFlag);

                    return WriteOutput(output, outputSize, method,
                        $"Disabled: {disableFlag}",
                        (int)ReturnCodes.Success);

                } else {
                    return WriteOutput(output, outputSize, method,
                        $"Error: Invalid flag {args[1]}",
                        (int)ReturnCodes.Error);
                }

            default:
                return WriteOutput(output, outputSize, method,
                    $"Error: Unknown action {action}",
                    (int)ReturnCodes.Error);
        }
    }

    private static string GetArmaParameterType(Type type) {
        if (type == typeof(void) || type == typeof(Task)) return "Nothing";

        // Handle Task<T> (async return types)
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            type = type.GetGenericArguments()[0];

        // Unwrap nullable types
        Type? underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null) type = underlying;

        if (type.IsEnum) return "String"; // Enums as strings

        // Primitive types
        if (type == typeof(string) || type == typeof(char)) return "String";
        if (type == typeof(bool)) return "Boolean";
        if (type == typeof(int) || type == typeof(uint) ||
            type == typeof(long) || type == typeof(ulong) ||
            type == typeof(short) || type == typeof(ushort) ||
            type == typeof(byte) || type == typeof(sbyte) ||
            type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return "Number";

        // Arrays
        if (type.IsArray || type == typeof(object[])) return "Array";

        // Generic collections
        if (type.IsGenericType) {
            var genDef = type.GetGenericTypeDefinition();
            if (genDef == typeof(List<>) || genDef == typeof(IEnumerable<>) || genDef == typeof(ICollection<>))
                return "Array";
            if (genDef == typeof(Dictionary<,>) || genDef == typeof(IDictionary<,>))
                return "HashMap";
        }

        // Nullable struct types that aren’t primitive
        if (type.IsValueType) return "Number";

        // Fallback for objects
        return "Anything";
    }

    internal static bool IsAsync(MethodInfo methodInfo) {
        if (methodInfo == null) return false;

        // Compiler adds this attribute to async methods
        if (methodInfo.GetCustomAttribute<AsyncStateMachineAttribute>() != null)
            return true;

        var type = methodInfo.ReturnType;

        if (type == typeof(Task) || type == typeof(ValueTask))
            return true;

        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            if (def == typeof(Task<>) || def == typeof(ValueTask<>))
                return true;
        }

        return false;
    }

    internal static bool IsAsyncWithReturn(MethodInfo methodInfo) {
        if (methodInfo == null) return false;

        var type = methodInfo.ReturnType;

        return type.IsGenericType && (
            type.GetGenericTypeDefinition() == typeof(Task<>)
            || type.GetGenericTypeDefinition() == typeof(ValueTask<>)
        );
    }
    
    private static bool MethodExists(string method) {
        if (string.IsNullOrEmpty(method)) return false;

        foreach (var container in MethodContainers) {
            MethodInfo? methodInfo = container.Type.GetMethod(method, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
            if (methodInfo != null) return true;
        }
        return false;
    }

    internal static bool IsVoidMethod(MethodInfo methodInfo) {
        if (methodInfo == null) return false;

        var returnType = methodInfo.ReturnType;

        // true void
        if (returnType == typeof(void)) return true;

        // async void equivalents (no result)
        if (returnType == typeof(Task) || returnType == typeof(ValueTask))
            return true;

        // Task<T> or ValueTask<T> → NOT void
        if (returnType.IsGenericType) {
            var def = returnType.GetGenericTypeDefinition();
            if (def == typeof(Task<>) || def == typeof(ValueTask<>))
                return false;
        }

        // everything else returns a value
        return false;
    }

    internal static MethodInfo GetMethod(string method) {
        if (string.IsNullOrEmpty(method)) throw new ArgumentException("Method name is null or empty.", nameof(method));

        foreach (var container in MethodContainers) {
            MethodInfo? methodInfo = container.Type.GetMethod(method, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
            if (methodInfo != null) return methodInfo;
        }
        throw new MissingMethodException($"Method '{method}' not found in registered method containers.");
    }

    // ----------------------------
    // Helper: Handle cancellation
    // ----------------------------
    private static int HandleAsyncCancel(nint output, int outputSize, string method, int asyncKey) {
        bool success = AsyncFactory.CancelAsyncTask(asyncKey);
        Events.RaiseAsyncTaskCancelled(asyncKey, success);

        return WriteOutput(output, outputSize, method,
            $@"[""{(success ? ExtensionResultCode.ASYNC_CANCEL_SUCCESS : ExtensionResultCode.ASYNC_CANCEL_FAILED)}"",[]]",
            success ? (int)ReturnCodes.Success : (int)ReturnCodes.Error);
    }

    // ----------------------------
    // Helper: Handle status query
    // ----------------------------
    private static int HandleAsyncStatus(nint output, int outputSize, string method, int asyncKey) {
        ExtensionResultCode status = AsyncFactory.TaskStatus(asyncKey);

        return WriteOutput(output, outputSize, method,
            $@"[""{status}"",[]]",
            (int)ReturnCodes.Success);
    }


    #endregion

}