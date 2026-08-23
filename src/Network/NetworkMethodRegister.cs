using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using static DynTypeNetwork.Settings.Logging;

namespace DynTypeNetwork;


public static class MethodBuilder {
    private sealed class RegisteredRpcMethod(Delegate invoker, MethodInfo? method, bool invokeWithArgumentArray) {
        public Delegate Invoker { get; } = invoker;
        public MethodInfo? Method { get; } = method;
        public bool InvokeWithArgumentArray { get; } = invokeWithArgumentArray;
    }

    public class RpcMethodParameter {
        public string Name { get; set; } = null!;
        public Type Type { get; set; } = null!;
    }

    public class RpcMethodInfo {
        public string Name { get; set; } = null!;
        public RpcMethodParameter[] Parameters { get; set; } = null!;
        public Type? ReturnType { get; set; }
    }

    private static readonly Dictionary<string, RegisteredRpcMethod> _serverMethods = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, RegisteredRpcMethod> _clientMethods = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, RpcMethodInfo> _clientMethodInfos = [];
    private static readonly Dictionary<string, RpcMethodInfo> _serverMethodInfos = [];

    // Get metadata
    public static RpcMethodInfo[] GetAvailableClientMethods() => _clientMethodInfos.Values.ToArray();
    public static RpcMethodInfo[] GetAvailableServerMethods() => _serverMethodInfos.Values.ToArray();

    // Register all public static methods from a type as client methods
    public static void RegisterClientMethods(object instance) {
        RegisterMethodsFromType(instance.GetType(), isServer: false);
    }

    // Register all public static methods from a type as server methods
    public static void RegisterServerMethods(object instance) {
        RegisterMethodsFromType(instance.GetType(), isServer: true);
    }

    internal static int RegisterFromHandshake(RpcMethodInfo[] methods, bool isServer)
    {
        int registeredCount = 0;

        foreach (var rpcInfo in methods)
        {
            var dictInfos = !isServer ? _serverMethodInfos : _clientMethodInfos;
            if (dictInfos.ContainsKey(rpcInfo.Name)) continue;

            Delegate del;
            if (rpcInfo.ReturnType == null)
            {
                del = new Action<object?[]>(args =>
                    throw new InvalidOperationException($"Remote method '{rpcInfo.Name}' cannot be called locally"));
            }
            else
            {
                del = new Func<object?[], object?>(args =>
                    throw new InvalidOperationException($"Remote method '{rpcInfo.Name}' cannot be called locally"));
            }

            if (!isServer)
                _serverMethods[rpcInfo.Name] = new RegisteredRpcMethod(del, null, invokeWithArgumentArray: true);
            else
                _clientMethods[rpcInfo.Name] = new RegisteredRpcMethod(del, null, invokeWithArgumentArray: true);

            dictInfos[rpcInfo.Name] = rpcInfo;
            registeredCount++;
        }

        return registeredCount;
    }

    private static void RegisterMethodsFromType(Type type, bool isServer) {
        MethodInfo[]? methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
        
        foreach (var method in methods) {
            if (method.IsSpecialName) continue;

            Delegate del;
            bool invokeWithArgumentArray = false;
            try {
                // Try creating a strongly-typed delegate
                var paramTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
                Type delegateType = method.ReturnType == typeof(void)
                    ? Expression.GetActionType(paramTypes)
                    : Expression.GetFuncType(paramTypes.Concat([method.ReturnType]).ToArray());

                del = Delegate.CreateDelegate(delegateType, method);
            } catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException) {
                // Fallback: use DynamicInvoke wrapper when native delegate creation is not supported in AOT/trimming builds.
                del = new Func<object?[], object?>(args => method.Invoke(null, args));
                invokeWithArgumentArray = true;
            }

            var rpcInfo = new RpcMethodInfo {
                Name = method.Name,
                Parameters = method.GetParameters()
                                    .Where(p => p.ParameterType != typeof(NetworkMessage))
                                    .Select(p => new RpcMethodParameter { Name = p.Name!, Type = p.ParameterType })
                                    .ToArray(),
                ReturnType = method.ReturnType == typeof(void) ? null : method.ReturnType
            };

            var registeredMethod = new RegisteredRpcMethod(del, method, invokeWithArgumentArray);

            if (isServer) {
                _serverMethods[method.Name] = registeredMethod;
                _serverMethodInfos[method.Name] = rpcInfo;
            } else {
                _clientMethods[method.Name] = registeredMethod;
                _clientMethodInfos[method.Name] = rpcInfo;
            }
        }
    }

    internal static T? CallServerMethod<T>(string methodName, NetworkMessage message, params object[] args) =>
        CallWithNetworkMessage<T>(_serverMethods, methodName, message, args);

    internal static T? CallClientMethod<T>(string methodName, NetworkMessage message, params object[] args) =>
        CallWithNetworkMessage<T>(_clientMethods, methodName, message, args);

    // Helper for both server and client
    private static T? CallWithNetworkMessage<T>(Dictionary<string, RegisteredRpcMethod> methods, string methodName, NetworkMessage message, object[] args)
    {
        if (!methods.TryGetValue(methodName, out RegisteredRpcMethod? registeredMethod))
            throw new InvalidOperationException($"{methodName} not registered.");

        MethodInfo method = registeredMethod.Method ?? registeredMethod.Invoker.Method;
        ParameterInfo[] parameters = method.GetParameters();

        object?[] finalArgs;

        if (FirstParameterIsNetworkMessage(method, out _)) {
            finalArgs = new object?[parameters.Length];

            // Inject message as first parameter
            finalArgs[0] = message;

            // Shift args right
            for (int i = 0; i < args.Length && i + 1 < finalArgs.Length; i++)
                finalArgs[i + 1] = args[i];
        } else {
            finalArgs = args;
        }
        
        if (LogItem(LogLevel.Debug)) Console.WriteLine($"Invoking:{methodName} with args: [{string.Join(", ", finalArgs.Select(a => a?.ToString() ?? "null"))}] ({finalArgs.Length})");
        
        if (registeredMethod.InvokeWithArgumentArray)
            return (T?)registeredMethod.Invoker.DynamicInvoke([finalArgs]);

        return (T?)registeredMethod.Invoker.DynamicInvoke(finalArgs);
    }

    private static bool FirstParameterIsNetworkMessage(MethodInfo method, out bool isOptional) {
        ParameterInfo[] parameters = method.GetParameters();
        isOptional = false;
        if (parameters.Length == 0) return false;

        ParameterInfo firstParam = parameters[0];
        Type firstType = Nullable.GetUnderlyingType(firstParam.ParameterType) ?? firstParam.ParameterType;

        if (firstType == typeof(NetworkMessage))
        {
            isOptional = firstParam.HasDefaultValue || firstParam.IsOptional;
            return true;
        }

        return false;
    }

    public static string ComputeMethodsHash(RpcMethodInfo[] methods)
    {
        // Create a string representation of the method signatures
        var methodSignatures = methods
            .OrderBy(m => m.Name) // Ensure consistent order
            .Select(m =>
            {
                string parameters = string.Join(",", m.Parameters.Select(p => $"{p.Type.FullName} {p.Name}"));
                string returnType = m.ReturnType?.FullName ?? "void";
                return $"{m.Name}({parameters}):{returnType}";
            });

        string combined = string.Join("|", methodSignatures);

        // Compute a hash (e.g., SHA256) of the combined string
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
        return Convert.ToBase64String(hashBytes);
    }
}
