using System;
using System.Reflection;
using System.Threading;
using static EdenOnline.Logger;

namespace EdenOnline;

public static partial class Events {
    // EVENTS
    public static event Action<string>? OnVersionCalled;

    public static event Action<string>? OnMethodCalled;
    public static event Action<string, object?[], bool>? OnMethodCalledResponse;

    public static event Action<string, object?[]>? OnMethodCalledWithArgs;
    public static event Action<string, object?[], bool>? OnMethodCalledWithArgsResponse;

    public static event Action<string, int, object?[]>? OnAsyncTaskStarted;
    public static event Action<string, int, object?[], bool>? OnAsyncTaskCompleted;
    public static event Action<int, bool>? OnAsyncTaskCancelled;

    public static event Action<string, object?[]>? OnSendToArma;
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

    internal static void RaiseVersionCalled(string version) {
        if (OnVersionCalled != null) OnVersionCalled.InvokeFireAndForget(version);
    }


    internal static void RaiseMethodCalled(string method) {
        if (OnMethodCalled != null) OnMethodCalled.InvokeFireAndForget(method);
    }
    internal static void RaiseMethodCalledResponse(string method, object?[]? response, bool success) {
        if (OnMethodCalledResponse != null) OnMethodCalledResponse.InvokeFireAndForget(method, response ?? [], success);
    }


    internal static void RaiseMethodCalledWithArgs(string method, object?[]? unserializedData) {
        if (OnMethodCalledWithArgs != null) OnMethodCalledWithArgs.InvokeFireAndForget(method, unserializedData ?? []);
    }
    internal static void RaiseMethodCalledWithArgsResponse(string method, object?[]? response, bool success) {
        if (OnMethodCalledWithArgsResponse != null) OnMethodCalledWithArgsResponse.InvokeFireAndForget(method, response ?? [], success);
    }


    internal static void RaiseAsyncTaskStartd(string method, int asyncKey, object?[] unserializedData) {
        if (OnAsyncTaskStarted != null) OnAsyncTaskStarted.InvokeFireAndForget(method, asyncKey, unserializedData);
    }
    internal static void RaiseAsyncTaskCompleted(string method, int asyncKey, bool success, object?[]? unserializedData = null) {
        if (OnAsyncTaskCompleted != null) OnAsyncTaskCompleted.InvokeFireAndForget(method, asyncKey, unserializedData ?? [], success);
    }
    internal static void RaiseAsyncTaskCancelled(int asyncKey, bool success) {
        if (OnAsyncTaskCancelled != null) OnAsyncTaskCancelled.InvokeFireAndForget(asyncKey, success);
    }


    internal static void RaiseSendToArma(string method, object?[] data) {
        if (OnSendToArma != null) OnSendToArma.InvokeFireAndForget(method, data);
    }


    internal static void RaiseErrorOccurred(Exception ex) {
        if (OnErrorOccurred != null) OnErrorOccurred.InvokeFireAndForget(ex);
    }
}
