using System;
using System.Threading.Tasks;

using static ArmaExtension.Logger;

namespace ArmaExtension;

public static partial class Extension {
    // EVENTS
    public static event Action<string>? VersionCalled;
    public static event Action<string>? MethodCalled;
    public static event Action<string, object?[]>? MethodCalledWithArgs;
    public static event Action<string, object?[], bool>? MethodCalledWithArgsResponse;
    public static event Action<string, int, object?[]>? AsyncTaskStarted;
    public static event Action<string, int, object?[], bool>? AsyncTaskCompleted;
    public static event Action<string, object?[]>? OnSendToArma;
    public static event Action<int, bool>? AsyncTaskCancelled;
    public static event Action<Exception>? ErrorOccurred;
    
    
    



    internal static void InvokeFireAndForget(this MulticastDelegate? eventDelegate, params object[] args) {
        if (eventDelegate == null) return;

        foreach (var handler in eventDelegate.GetInvocationList()) {
            try {
                Task.Run(() => handler.DynamicInvoke(args));
            } catch (Exception ex) {
                Log($"Error invoking event handler: {ex}");
            }
        }
    }

    internal static void RaiseAsyncTaskStartd(string method, int asyncKey, object?[] unserializedData) {
        AsyncTaskStarted.InvokeFireAndForget(method, asyncKey, unserializedData);
    }
    internal static void RaiseAsyncTaskCompleted(string method, int asyncKey, bool success, object?[]? unserializedData = null) {
        AsyncTaskCompleted.InvokeFireAndForget(method, asyncKey, unserializedData ?? [], success);
    }
}