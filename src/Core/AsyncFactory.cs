using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static ArmaExtension.Logger;
using static ArmaExtension.Extension;

namespace ArmaExtension {
    public static class AsyncFactory {
        private static readonly ConcurrentDictionary<int, Task> AsyncTasks = new();
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> CancelTokens = new();


        public static bool CancelAsyncTask(string token) {
            if (CancelTokens.TryGetValue(token, out CancellationTokenSource? source)) {
                source.Cancel();
                return CancelTokens.TryRemove(token, out _);
            }
            return false;
        }

        public static string ExecuteAsyncTask(MethodInfo method, string[] argArray, int asyncKey) {
            bool isVoid = IsVoidMethod(method.Name) || asyncKey == -1;

            // Only create cancel token if we’ll actually use it
            CancellationTokenSource? source = null;
            string token = string.Empty;

            if (!isVoid) {
                source = new CancellationTokenSource();
                token = Guid.NewGuid().ToString("N").ToUpper();
                CancelTokens[token] = source;
            }

            var capturedToken = source?.Token ?? CancellationToken.None;

            Task.Run(async () => {
                try {
                    object?[] unserializedData = Serializer.DeserializeJsonArray(argArray);

                    RaiseAsyncTaskStartd(method.Name, asyncKey, unserializedData);

                    Log(@$"ASYNC RESPONSE {(isVoid ? "(VOID)" : "")} >> [""{method.Name}|{asyncKey}"", {Serializer.PrintArray(unserializedData)}]");

                    ParameterInfo[] parameters = method.GetParameters();
                    if (unserializedData.Length > parameters.Length)
                        unserializedData = unserializedData.Take(parameters.Length).ToArray();

                    if (unserializedData.Length != parameters.Length)
                        throw new ArmaAsyncException(asyncKey, $"Parameter count mismatch for method {method.Name}. Expected {parameters.Length}, got {unserializedData.Length}.");

                    object? result = method.Invoke(null, unserializedData);

                    if (isVoid) return;

                    if (result is Task taskResult) {
                        await taskResult;
                        if (taskResult.GetType().IsGenericType)
                            result = ((dynamic)taskResult).Result;
                    }
                    
                    RaiseAsyncTaskCompleted(method.Name, asyncKey, true, [result]);
                    SendAsyncCallbackMessage(ResultCodes.ASYNC_RESPONSE.ToString(), [result], (int)ReturnCodes.Success, asyncKey);
                } catch (Exception ex) {
                    RaiseAsyncTaskCompleted(method.Name, asyncKey, false, [ex.Message]);
                    SendAsyncCallbackMessage(ResultCodes.ASYNC_FAILED.ToString(), [ex.Message], (int)ReturnCodes.Error, asyncKey);
                } finally {
                    AsyncTasks.TryRemove(asyncKey, out _);
                    if (!string.IsNullOrEmpty(token))
                        CancelTokens.TryRemove(token, out _);
                }
            }, capturedToken);

            AsyncTasks[asyncKey] = Task.CompletedTask;

            return token;
        }

    }
}