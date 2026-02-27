using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static ArmaExtension.Logger;
using static ArmaExtension.Enums;
using static ArmaExtension.MethodSystem;

namespace ArmaExtension;

public static partial class Events {
    // EVENTS
    public static event Action<string>? OnVersionCalled;
    public static event Action<string>? OnMethodCalled;
    public static event Action<string, object?[]>? OnMethodCalledWithArgs;
    public static event Action<string, object?[], bool>? OnMethodCalledWithArgsResponse;
    public static event Action<string, int, object?[]>? OnAsyncTaskStarted;
    public static event Action<string, int, object?[], bool>? OnAsyncTaskCompleted;
    public static event Action<string, object?[]>? OnSendToArma;
    public static event Action<int, bool>? OnAsyncTaskCancelled;
    public static event Action<Exception>? OnErrorOccurred;
    
    
    



    internal static void InvokeFireAndForget(this MulticastDelegate? eventDelegate, params object[] args) {
        if (eventDelegate == null) return;

        foreach (var handler in eventDelegate.GetInvocationList()) {
            ThreadPool.QueueUserWorkItem(_ => {
                try {
                    handler.DynamicInvoke(args);
                } catch (TargetParameterCountException) {
                    // Skip handlers with mismatched signatures
                    Debug($"Event handler skipped due to parameter count mismatch. ({handler.Method.Name}) Expected: {handler.Method.GetParameters().Length}, Got: {args.Length}");
                } catch (Exception ex) {
                    Error($"Error invoking event handler: {ex}");
                }
            });
        }
    }

    internal static void RaiseAsyncTaskStartd(string method, int asyncKey, object?[] unserializedData) {
        OnAsyncTaskStarted.InvokeFireAndForget(method, asyncKey, unserializedData);
    }
    internal static void RaiseAsyncTaskCompleted(string method, int asyncKey, bool success, object?[]? unserializedData = null) {
        OnAsyncTaskCompleted.InvokeFireAndForget(method, asyncKey, unserializedData ?? [], success);
    }

    // helper methods for raising events from outside the Events class
    internal static void RaiseAsyncTaskCancelled(int asyncKey, bool success) {
        OnAsyncTaskCancelled.InvokeFireAndForget(asyncKey, success);
    }

    internal static void RaiseMethodCalledWithArgsResponse(string method, object?[]? response, bool success) {
        OnMethodCalledWithArgsResponse.InvokeFireAndForget(method, response ?? [], success);
    }

    internal static void RaiseErrorOccurred(Exception ex) {
        OnErrorOccurred.InvokeFireAndForget(ex);
    }

    // Additional helpers used by other core components
    internal static void RaiseVersionCalled(string version) {
        OnVersionCalled.InvokeFireAndForget(version);
    }

    internal static void RaiseMethodCalled(string method) {
        OnMethodCalled.InvokeFireAndForget(method);
    }

    internal static void RaiseSendToArma(string method, object?[] data) {
        OnSendToArma.InvokeFireAndForget(method, data);
    }
}