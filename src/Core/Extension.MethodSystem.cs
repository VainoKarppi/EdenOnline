using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;

using static ArmaExtension.Logger;

namespace ArmaExtension;

public static partial class Extension {
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
        Log($"Registered method container: {methodsType.FullName}");
    }

    internal static bool MethodExists(string method) {
        if (string.IsNullOrEmpty(method)) return false;

        foreach (var container in MethodContainers) {
            MethodInfo? methodInfo = container.Type.GetMethod(method, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
            if (methodInfo != null) return true;
        }
        return false;
    }

    internal static bool IsVoidMethod(string method) {
        if (string.IsNullOrEmpty(method)) return false;

        foreach (var container in MethodContainers) {
            MethodInfo? methodInfo = container.Type.GetMethod(method, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
            if (methodInfo != null && methodInfo.ReturnType == typeof(void)) return true;
        }
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






    private static int ExecuteArmaMethod(nint output, int outputSize, string method, string[]? argArray = null) {
        try {
            argArray ??= [];

            int pipeIndex = method.IndexOf('|');
            string originalMethod = pipeIndex >= 0 ? method[..pipeIndex] : method;
            if (string.IsNullOrEmpty(originalMethod)) throw new Exception("Invalid Method");

            if (originalMethod.Equals(ResultCodes.ASYNC_CANCEL.ToString(), StringComparison.OrdinalIgnoreCase)) {
                string taskKey = pipeIndex >= 0 ? method[(pipeIndex + 1)..] : string.Empty;

                bool success = AsyncFactory.CancelAsyncTask(taskKey);
                AsyncTaskCancelled.InvokeFireAndForget(taskKey, success);

                return WriteOutput(output, outputSize, originalMethod,
                    $@"[""{(success ? ResultCodes.ASYNC_CANCEL_SUCCESS : ResultCodes.ASYNC_CANCEL_FAILED)}"",[]]",
                    success ? (int)ReturnCodes.Success : (int)ReturnCodes.Error);
            }

            if (!MethodExists(originalMethod)) throw new Exception("Invalid Method");

            MethodInfo methodToInvoke = GetMethod(originalMethod);
            bool isVoid = IsVoidMethod(originalMethod);

            int asyncKey = 0;
            bool async = pipeIndex >= 0 && int.TryParse(method[(pipeIndex + 1)..], out asyncKey);


            if (async) {
                string cancelKey = AsyncFactory.ExecuteAsyncTask(methodToInvoke, argArray, asyncKey);
                string outputPayload = isVoid
                    ? $@"[""{ResultCodes.ASYNC_SENT_VOID}"",[]]"
                    : $@"[""{ResultCodes.ASYNC_SENT}"",[""{cancelKey}""]]";

                return WriteOutput(output, outputSize, originalMethod, outputPayload, (int)ReturnCodes.Success);
            }


            ParameterInfo[] parameters = methodToInvoke.GetParameters();
            if (parameters.Length > 0 && argArray.Length == 0)
                throw new Exception("Parameters missing!");

            object?[] unserializedData = Serializer.DeserializeJsonArray(argArray);

            if (isVoid) {
                MethodCalledWithArgs.InvokeFireAndForget(originalMethod, unserializedData);
                Task.Run(() => methodToInvoke.Invoke(null, unserializedData));
                return WriteOutput(output, outputSize, originalMethod,
                    $@"[""{ResultCodes.SUCCESS_VOID}"",[]]",
                    (int)ReturnCodes.Success);
            }

            object? result = methodToInvoke.Invoke(null, unserializedData);
            if (result is Task task) task.Wait();
            object? returnValue = result is Task t && t.GetType().IsGenericType
                ? ((dynamic)t).Result
                : result;


            MethodCalledWithArgsResponse.InvokeFireAndForget(originalMethod, new object?[] { returnValue });

            return WriteOutput(output, outputSize, originalMethod,
                $@"[""{ResultCodes.SUCCESS}"",{Serializer.PrintArray([returnValue])}]",
                (int)ReturnCodes.Success);
        } catch (Exception ex) {
            ErrorOccurred.InvokeFireAndForget(ex);

            return WriteOutput(output, outputSize, method,
                $@"[""{ResultCodes.ERROR}"",[""{ex.Message}""]]",
                (int)ReturnCodes.Error);
        }
    }
}