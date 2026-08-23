using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using static EdenOnline.Logger;
using static EdenOnline.MethodSystem;
using static EdenOnline.PluginLoader;

namespace EdenOnline;

public static partial class Extension {
    public static bool DEBUG = true;

    /// <summary>
    /// Called only once when Arma 3 loads the extension.
    /// </summary>
    /// <param name="func">Pointer to Arma 3's callback function</param>
    [UnmanagedCallersOnly(EntryPoint = "RVExtensionRegisterCallback")]
    private static unsafe void RvExtensionRegisterCallback(delegate* unmanaged<string, string, string, int> callback) {
        Callback = callback;
    }
    internal static unsafe delegate* unmanaged<string, string, string, int> Callback;

    // ---------------------------------------------------------------------
    // Outbound message queue.
    //
    // Arma's callback buffer accepts at most 100 entries per frame. When the
    // buffer is full, the callback pointer returns a negative value and the
    // message is dropped unless retried. Rather than retrying inline (which
    // would block/spin the calling thread), every outbound message is queued
    // and drained by a single dedicated worker thread. This keeps ordering
    // intact and applies bounded backpressure during very large bursts instead
    // of retaining every serialized payload in memory. Delivery never gives
    // up on a message unless the callback pointer itself throws.
    // ---------------------------------------------------------------------
    private const int OutboundQueueCapacity = 256;
    private static readonly BlockingCollection<(string method, string data)> _outbox = new(
        new ConcurrentQueue<(string, string)>(),
        OutboundQueueCapacity
    );

    private static readonly Thread _outboxWorker = CreateOutboxWorker();

    // When the buffer is full we don't know exactly when it'll be drained,
    // so instead of sleeping a flat amount every time, we spin first (cheap,
    // catches the buffer clearing faster than a frame) and only fall back to
    // sleeping - with a small growing backoff - if it stays full. This keeps
    // large bursts (tens of thousands of queued calls) moving as fast as
    // Arma can actually drain them instead of being capped at one retry per
    // fixed interval.
    private const int SpinRetriesBeforeSleep = 20;
    private const int InitialBackoffMs = 1;
    private const int MaxBackoffMs = 16; // one frame at 60 fps, as a ceiling

    private static Thread CreateOutboxWorker() {
        Thread t = new(ProcessOutbox) {
            IsBackground = true,
            Name = "EdenOnline.ArmaCallbackWorker"
        };
        t.Start();
        return t;
    }

    private static void ProcessOutbox() {
        foreach ((string method, string data) in _outbox.GetConsumingEnumerable()) {
            DeliverToArma(method, data);
        }
    }

    /// <summary>
    /// Invokes Arma's registered callback pointer, if any. Contains the only
    /// unsafe code in this class - the unsafe block is scoped to just the
    /// pointer access/call, not the whole method, so callers don't need to
    /// be in an unsafe context themselves.
    /// </summary>
    /// <returns>
    /// True if the callback pointer was registered and got invoked (with the
    /// slots-remaining result in <paramref name="remainingSlots"/>); false if
    /// no callback is registered yet.
    /// </returns>
    private static bool TryInvokeCallback(string method, string data, out int remainingSlots) {
        unsafe {
            if (Callback == null) {
                remainingSlots = default;
                return false;
            }

            remainingSlots = Callback(ExtensionName, method, data);
            return true;
        }
    }

    /// <summary>
    /// Delivers a single message to Arma, retrying indefinitely whenever the
    /// callback buffer is full. Retries start as a tight spin (no delay) and
    /// only escalate to a small, growing sleep if the buffer stays full, so
    /// throughput adapts to how fast Arma is actually draining the buffer
    /// instead of assuming a fixed frame interval. Only stops if the
    /// callback itself throws.
    /// </summary>
    private static void DeliverToArma(string method, string data) {
        int spinAttempts = 0;
        int backoffMs = InitialBackoffMs;

        while (true) {
            try {
                if (!TryInvokeCallback(method, data, out int remainingSlots)) {
                    // Not registered yet (or unregistered) - wait and retry
                    // instead of dropping the message.
                    Thread.Sleep(MaxBackoffMs);
                    continue;
                }

                if (remainingSlots >= 0) return; // accepted this frame

                // Negative return = buffer was full. Spin a handful of times
                // with no delay first, since the buffer may clear well
                // before a full frame elapses (especially when calling from
                // a background thread, per Arma's own docs). Only once
                // spinning fails to find room do we start sleeping, and even
                // then we ramp the delay up gradually instead of jumping
                // straight to a full frame's worth of wait.
                if (spinAttempts < SpinRetriesBeforeSleep) {
                    spinAttempts++;
                    Thread.SpinWait(200);
                } else {
                    Thread.Sleep(backoffMs);
                    backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
                }
            } catch (Exception ex) {
                Events.RaiseErrorOccurred(ex);
                Error(ex.Message);
                return; // give up on this specific message only
            }
        }
    }



    /// <summary>
    /// Called only once when Arma 3 loads the extension.
    /// The output will be written in the RPT logs.
    /// </summary>
    /// <param name="output">A pointer to the output buffer</param>
    /// <param name="outputSize">The maximum length of the buffer (always 32 for this particular method)</param>
    [UnmanagedCallersOnly(EntryPoint = "RVExtensionVersion")]
    private static void RVExtensionVersion(nint output, int outputSize) {
        
        //string dllPath = Path.Combine(Path.GetDirectoryName(AssemblyDirectory) ?? "", $"{ExtensionName}_x64.dll");
        //string dllHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dllPath)));

        // Calculate header lenght
        const int width = 79;
        string title = $" {ExtensionName} ";
        int padding = width - title.Length;
        string top = new string('=', padding / 2) + title + new string('=', padding - padding / 2);
        string bottom = new('=', width);

        Log(
            $"\n{top}" +
            //$"\nDLL Path: \"{dllPath}\"" +
            $"\nArma 3 Path: \"{AssemblyDirectory}\"" +
            //$"\nSHA-256: {dllHash}" +
            $"\nVersion: {Version}" +
            $"\n{bottom}"
        );

        bool firstRun = InitializePlugins(); // Initialize plugins if not already done
        if (!firstRun) Events.RaiseVersionCalled(Version);

        WriteOutput(output, outputSize, "Version", Version);
    }



    /// <summary>
    /// The entry point for the default "callExtension" command.
    /// </summary>
    /// <param name="output">A pointer to the output buffer</param>
    /// <param name="outputSize">The maximum size of the buffer (20480 bytes)</param>
    /// <param name="function">The function identifier passed in "callExtension"</param>
    [UnmanagedCallersOnly(EntryPoint = "RVExtension")]
    private static int RVExtension(nint output, int outputSize, nint function) {
        string method = Marshal.PtrToStringAnsi(function) ?? string.Empty;

        Debug(@$"ARMA >> EXTENSION (No Args) >> {method}");

        Events.RaiseMethodCalled(method);

        return HandleExecuteExtensionMethod(output, outputSize, method);
    }



    /// <summary>
    /// The entry point for the "callExtension" command with function arguments.
    /// </summary>
    /// <param name="output">A pointer to the output buffer</param>
    /// <param name="outputSize">The maximum size of the buffer (20480 bytes)</param>
    /// <param name="function">The function identifier passed in "callExtension"</param>
    /// <param name="argv">The args passed to "callExtension" as a string array</param>
    /// <param name="argc">Number of elements in "argv"</param>
    /// <returns>The return code</returns>
    [UnmanagedCallersOnly(EntryPoint = "RVExtensionArgs")]
    private static int RVExtensionArgs(nint output, int outputSize, nint function, nint args, int argsCnt) {
        string method = Marshal.PtrToStringAnsi(function) ?? string.Empty;

        // Get Args
        string[] argArray = new string[argsCnt];
        for (int i = 0; i < argsCnt; i++) {
            nint argPtr = Marshal.ReadIntPtr(args, i * nint.Size);
            argArray[i] = Marshal.PtrToStringAnsi(argPtr) ?? string.Empty;
        }

        Debug(@$"ARMA >> EXTENSION (Args) >> {method} [{string.Join(",", argArray)}]");

        return HandleExecuteExtensionMethod(output, outputSize, method, argArray);
    }



    /// <summary>
    /// Queues a response to be sent back to Arma 3. Delivery happens
    /// asynchronously on a dedicated worker thread, which automatically
    /// waits for the next frame and retries if Arma's callback buffer is
    /// full. The bounded queue applies backpressure to large producers so
    /// bursts do not retain unbounded serialized payloads in memory.
    /// </summary>
    /// <param name="method"></param>
    /// <param name="data"></param>
    /// <returns>BOOL - Whether the message was accepted into the outbound queue</returns>
    public static bool SendToArma(string method, object?[] data) {
        if (string.IsNullOrEmpty(method)) {
            Log("Empty function name in SendToArma.");
            return false;
        }

        Events.RaiseSendToArma(method, data);

        string dataString = Serializer.PrintArray(data);

        Debug($"EXTENSION >> ARMA >> method={method}, payloadChars={dataString.Length} (queued)");

        _outbox.Add((method, dataString));

        return true;
    }


    internal static void SendAsyncResponseCallbackMessage(string method, object?[] data, int errorCode = 0, int asyncKey = -1) {
        if (string.IsNullOrEmpty(method)) {
            Log("Empty function name in SendAsyncCallbackMessage.");
            return;
        }

        method += $"|{asyncKey}|{errorCode}";

        string returnData = Serializer.PrintArray(data);

        Debug($"EXTENSION CALLBACK >> ARMA >> method={method}, payloadChars={returnData.Length} (queued)");

        _outbox.Add((method, returnData));
    }


    internal static int WriteOutput(nint output, int outputSize, string methodName, string message, int returnCode = 0) {
        Debug(@$"EXTENSION >> ARMA >> ({methodName}) >> {message}");

        byte[] bytes = Encoding.ASCII.GetBytes(message);
        int length = Math.Min(bytes.Length, outputSize - 1);
        Marshal.Copy(bytes, 0, output, length);
        Marshal.WriteByte(output, length, 0);

        return returnCode;
    }


    private static string? GetArmaPath()
    {
        if (OperatingSystem.IsWindows())
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\WOW6432Node\Bohemia Interactive\ArmA 3"
            );

            string? armaPath = key?.GetValue("main") as string;

            return string.IsNullOrWhiteSpace(armaPath)
                ? null
                : armaPath;
        }

        if (OperatingSystem.IsLinux())
        {
            string? executable = Environment.ProcessPath;

            return executable != null
                ? Path.GetDirectoryName(executable)
                : null;
        }

        return null;
    }
}
