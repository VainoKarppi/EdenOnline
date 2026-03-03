using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static ArmaExtension.Logger;
using static ArmaExtension.Enums;
using static ArmaExtension.MethodSystem;
using static ArmaExtension.Events;
using System.Collections.Generic;

namespace ArmaExtension;

internal static class AsyncFactory {
    public static readonly ConcurrentDictionary<int, (Task Task, CancellationTokenSource CancelSource)> AsyncTasks = new();

    internal static bool CancelAsyncTask(int asyncKey) {
        if (!AsyncTasks.TryGetValue(asyncKey, out var tuple))
            return false;

        tuple.CancelSource.Cancel();

        CleanupTask(asyncKey);
        return true;
    }


    internal static ExtensionResultCode TaskStatus(int asyncKey) {
        if (!AsyncTasks.TryGetValue(asyncKey, out var tuple))
            return ExtensionResultCode.ASYNC_STATUS_NOT_FOUND;

        var task = tuple.Task;

        // TODO make task gets removed from AsyncTasks list if canceled/faulted/completed
        if (task.IsCanceled) return ExtensionResultCode.ASYNC_STATUS_NOT_FOUND; 
        if (task.IsFaulted) return ExtensionResultCode.ASYNC_STATUS_NOT_FOUND;
        if (task.IsCompleted) return ExtensionResultCode.ASYNC_STATUS_NOT_FOUND;

        return ExtensionResultCode.ASYNC_STATUS_RUNNING;
    }

    internal static bool ExecuteAsyncTask(MethodInfo methodToInvoke, string[] argArray, int asyncKey) {
        bool isVoid = IsVoidMethod(methodToInvoke) || asyncKey == -1;

        var source = new CancellationTokenSource();

        // Atomically reserve the asyncKey
        if (!AsyncTasks.TryAdd(asyncKey, (Task.CompletedTask, source))) {
            // Task is already running
            return false;
        }

        var capturedToken = source.Token;

        // Start the actual task
        var task = Task.Run(async () => {
            try {
                object?[] unserializedData = Serializer.DeserializeJsonArray(methodToInvoke, argArray);
                RaiseAsyncTaskStartd(methodToInvoke.Name, asyncKey, unserializedData);
                Log(@$"ARMA >> EXTENSION | ASYNC{(isVoid ? "(VOID)" : "")} >> [""{methodToInvoke.Name}|{asyncKey}"", {Serializer.PrintArray(unserializedData)}]");

                object?[] finalParams = Serializer.PrepareMethodParameters(methodToInvoke, unserializedData, asyncKey);
                object? result = methodToInvoke.Invoke(null, finalParams);

                if (isVoid) return;

                result = await UnwrapAsync(result);

                RaiseAsyncTaskCompleted(methodToInvoke.Name, asyncKey, true, [result]);
                Extension.SendAsyncResponseCallbackMessage(
                    ExtensionResultCode.ASYNC_RESPONSE.ToString(),
                    [result],
                    (int)ReturnCodes.Success,
                    asyncKey
                );
            } catch (Exception ex) {
                ex = ex.InnerException ?? ex;
                RaiseAsyncTaskCompleted(methodToInvoke.Name, asyncKey, false, [ex.Message]);
                Extension.SendAsyncResponseCallbackMessage(
                    ExtensionResultCode.ASYNC_SENT_FAILED.ToString(),
                    [ex.Message],
                    (int)ReturnCodes.Error,
                    asyncKey
                );
            } finally {
                CleanupTask(asyncKey);
            }
        }, capturedToken);

        // Replace placeholder with actual running task
        AsyncTasks[asyncKey] = (task, source);

        return true;
    }

    private static void CleanupTask(int asyncKey) {
        AsyncTasks.TryRemove(asyncKey, out _);
    }

    private static async Task<object?> UnwrapAsync(object? result) {
        if (result is not Task task)
            return result;

        await task.ConfigureAwait(false);

        return task switch {
            // Boolean
            Task<bool> t => t.Result,
            Task<bool?> t => t.Result,

            // Integer types
            Task<byte> t => t.Result,
            Task<byte?> t => t.Result,
            Task<sbyte> t => t.Result,
            Task<sbyte?> t => t.Result,
            Task<short> t => t.Result,
            Task<short?> t => t.Result,
            Task<ushort> t => t.Result,
            Task<ushort?> t => t.Result,
            Task<int> t => t.Result,
            Task<int?> t => t.Result,
            Task<uint> t => t.Result,
            Task<uint?> t => t.Result,
            Task<long> t => t.Result,
            Task<long?> t => t.Result,
            Task<ulong> t => t.Result,
            Task<ulong?> t => t.Result,

            // Floating point
            Task<float> t => t.Result,
            Task<float?> t => t.Result,
            Task<double> t => t.Result,
            Task<double?> t => t.Result,
            Task<decimal> t => t.Result,
            Task<decimal?> t => t.Result,

            // Other primitives
            Task<char> t => t.Result,
            Task<char?> t => t.Result,
            Task<string> t => t.Result,

            // Date & time
            Task<DateTime> t => t.Result,
            Task<DateTime?> t => t.Result,
            Task<DateTimeOffset> t => t.Result,
            Task<DateTimeOffset?> t => t.Result,
            Task<TimeSpan> t => t.Result,
            Task<TimeSpan?> t => t.Result,

            // Identifiers & versioning
            Task<Guid> t => t.Result,
            Task<Guid?> t => t.Result,
            Task<Version> t => t.Result,
            Task<Uri> t => t.Result,

            // Arrays
            Task<byte[]> t => t.Result,
            Task<int[]> t => t.Result,
            Task<string[]> t => t.Result,
            Task<object?[]> t => t.Result,

            // Collections (common)
            Task<List<int>> t => t.Result,
            Task<List<string>> t => t.Result,
            Task<List<object?>> t => t.Result,
            Task<Dictionary<string, object?>> t => t.Result,
            Task<Dictionary<string, string>> t => t.Result,

            // Tuples
            Task<(int, int)> t => t.Result,
            Task<(string, string)> t => t.Result,
            Task<(string Name, int Value)> t => t.Result,

            // Fallback object
            Task<object?> t => t.Result,

            // Non-generic Task or unsupported type
            _ => null
        };
    }

}
