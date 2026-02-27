using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using static ArmaExtension.Extension;
using static ArmaExtension.Logger;
using static ArmaExtension.Enums;
using static ArmaExtension.MethodSystem;

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
        Debug($"Registered method container: {methodsType.FullName}");
    }

    internal static bool MethodExists(string method) {
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

        // async void equivalent
        if (returnType == typeof(Task)) return true;
        if (returnType == typeof(ValueTask)) return true;

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


    internal static int HandleExecuteExtensionMethod(nint output, int outputSize, string method, string[]? argArray = null) {
        try {
            argArray ??= [];

            int pipeIndex = method.IndexOf('|');
            string originalMethod = pipeIndex >= 0 ? method[..pipeIndex] : method;
            if (string.IsNullOrEmpty(originalMethod)) throw new Exception("Invalid Method");

            int asyncKey = 0;
            bool async = pipeIndex >= 0 && int.TryParse(method[(pipeIndex + 1)..], out asyncKey);

            // Handle cancellation request
            if (originalMethod.Equals(ExtensionResultCode.ASYNC_CANCEL.ToString(), StringComparison.OrdinalIgnoreCase)) {
                return HandleAsyncCancel(output, outputSize, originalMethod, asyncKey);
            }

            // Handle status request
            if (originalMethod.Equals(ExtensionResultCode.ASYNC_STATUS.ToString(), StringComparison.OrdinalIgnoreCase)) {
                return HandleAsyncStatus(output, outputSize, originalMethod, asyncKey);
            }

            if (!MethodExists(originalMethod)) throw new Exception("Invalid Method");

            // Execute method
            if (async) {
                return ExecuteAsyncMethod(originalMethod, argArray, asyncKey, output, outputSize);
            } else {
                return ExecuteSyncMethod(originalMethod, argArray, asyncKey, output, outputSize);
            }

        } catch (Exception ex) {
            ex = ex.InnerException ?? ex;
            Events.RaiseErrorOccurred(ex);

            return WriteOutput(output, outputSize, method,
                $@"[""{ExtensionResultCode.ERROR}"",[""{ex.Message}""]]",
                (int)ReturnCodes.Error);
        }
    }


    private static int ExecuteSyncMethod(string originalMethod, string[] argArray, int asyncKey, nint output, int outputSize)
    {
        MethodInfo methodToInvoke = GetMethod(originalMethod);
        bool isVoid = IsVoidMethod(methodToInvoke);

        object?[] unserializedData = Serializer.DeserializeJsonArray(argArray);

        // Prepare parameters (truncate, validate, fill defaults)
        object?[] finalParams = Serializer.PrepareMethodParameters(methodToInvoke, unserializedData, asyncKey);


        // TODO if method is using async: what to do when called without async key?
        // TODO return later to arma without key?
        // Invoke method
        object? result = methodToInvoke.Invoke(null, finalParams);

        Console.WriteLine($"Method '{originalMethod}' invoked. Result: {result}");

        if (result is Task task) task.Wait();

        object? returnValue = result is Task t && t.GetType().IsGenericType
            ? ((dynamic)t).Result
            : result;

        Events.RaiseMethodCalledWithArgsResponse(originalMethod, [returnValue], true);

        return WriteOutput(output, outputSize, originalMethod,
            $@"[""{ExtensionResultCode.SUCCESS}"",{(isVoid ? "[]" : Serializer.PrintArray([returnValue]))}]",
            (int)ReturnCodes.Success);
    }

    private static int ExecuteAsyncMethod(string originalMethod, string[] argArray, int asyncKey, nint output, int outputSize) {
        MethodInfo methodToInvoke = GetMethod(originalMethod);

        bool success = AsyncFactory.ExecuteAsyncTask(methodToInvoke, argArray, asyncKey);

        // TODO Do we really need to send back the cancelKeyToken, or would the request id be enough?
        /*
            *Pros:
            Arma doesn’t need to store or deal with the long token string.
            Async tasks are still uniquely tracked internally.
            Simplifies Arma script logic.

            *Cons:
            If a developer sends the same asyncKey twice before the first completes, it could overwrite the mapping. Usually asyncKey should be globally unique per request.
            We need to veify that no task is running with this key, before starting it --> Slows down the process
        */

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
        string status = AsyncFactory.TaskStatus(asyncKey).ToString();

        return WriteOutput(output, outputSize, method,
            $@"[""{ExtensionResultCode.ASYNC_STATUS}"",[""{status}""]]",
            (int)ReturnCodes.Success);
    }


}