using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using static ArmaExtension.Logger;
using static ArmaExtension.Enums;
using static ArmaExtension.MethodSystem;
using static ArmaExtension.PluginLoader;
using System.Diagnostics;

namespace ArmaExtension;

public static partial class Extension {
    /// <summary>
    /// Get the context information passed from Arma 3.
    /// </summary>
    public readonly static List<string> ExtensionContext = []; // TODO: What should we actually contain in this
    private static bool _contextInitialized = false;

    [UnmanagedCallersOnly(EntryPoint = "RVExtensionContext")]
    private static unsafe void RVExtensionContext(IntPtr* args, uint argsCnt) {
        if (_contextInitialized) return;

        ExtensionContext.Clear();

        // We expect at least 5 elements (CaptionText, SteamID, FileSource, MissionName, ServerName)
        // Defensive: if argsCnt < 5, fill missing with empty string
        string captionText = argsCnt > 0 && args[0] != IntPtr.Zero ? Marshal.PtrToStringAnsi(args[0]) ?? string.Empty : string.Empty;
        string steamId = argsCnt > 1 && args[1] != IntPtr.Zero ? Marshal.PtrToStringAnsi(args[1]) ?? string.Empty : string.Empty;
        string fileSource = argsCnt > 2 && args[2] != IntPtr.Zero ? Marshal.PtrToStringAnsi(args[2]) ?? string.Empty : string.Empty;
        string missionName = argsCnt > 3 && args[3] != IntPtr.Zero ? Marshal.PtrToStringAnsi(args[3]) ?? string.Empty : string.Empty;
        string serverName = argsCnt > 4 && args[4] != IntPtr.Zero ? Marshal.PtrToStringAnsi(args[4]) ?? string.Empty : string.Empty;

        ExtensionContext.Add(captionText);
        ExtensionContext.Add(steamId);
        ExtensionContext.Add(fileSource);
        ExtensionContext.Add(missionName);
        ExtensionContext.Add(serverName);

        _contextInitialized = true;
    }

    /// <summary>
    /// Called only once when Arma 3 loads the extension.
    /// </summary>
    /// <param name="func">Pointer to Arma 3's callback function</param>
    [UnmanagedCallersOnly(EntryPoint = "RVExtensionRegisterCallback")]
    private static unsafe void RvExtensionRegisterCallback(delegate* unmanaged<string, string, string, int> callback) {
        Callback = callback;
    }
    internal static unsafe delegate* unmanaged<string, string, string, int> Callback;



    /// <summary>
    /// Called only once when Arma 3 loads the extension.
    /// The output will be written in the RPT logs.
    /// </summary>
    /// <param name="output">A pointer to the output buffer</param>
    /// <param name="outputSize">The maximum length of the buffer (always 32 for this particular method)</param>
    [UnmanagedCallersOnly(EntryPoint = "RVExtensionVersion")]
    private static void RVExtensionVersion(nint output, int outputSize) {
        Log($"\n==============================================================\nExtension ({ExtensionName}) Started | {AssemblyDirectory} | {Version} |\n==============================================================");

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

        return HandleExecuteExtensionMethod(output, outputSize, method, argArray);
    }



    /// <summary>
    /// Sends the response back to Arma 3.
    /// </summary>
    /// <param name="method"></param>
    /// <param name="data"></param>
    /// <returns>BOOL - Success/Failed</returns>
    public static bool SendToArma(string method, object?[] data) {
        if (string.IsNullOrEmpty(method)) Log("Empty function name in SendToArma.");

        Events.RaiseSendToArma(method, data);
        
        string dataString = Serializer.PrintArray(data);

        Debug(@$"EXTENSION >> ARMA >> [""{ExtensionName}"", ""{method}"", ""{dataString}""]");

        try {
            unsafe { Callback(ExtensionName, method, dataString); }

            return true;
        } catch (Exception ex) {
            Events.RaiseErrorOccurred(ex);
            Error(ex.Message);
            return false;
        }
    }


    internal static void SendAsyncResponseCallbackMessage(string method, object?[] data, int errorCode = 0, int asyncKey = -1) {
        if (string.IsNullOrEmpty(method)) Log("Empty function name in SendAsyncCallbackMessage.");

        method += $"|{asyncKey}|{errorCode}";

        string returnData = Serializer.PrintArray(data);

        Debug(@$"EXTENSION CALLBACK >> ARMA >> [""{ExtensionName}"", ""{method}"", ""{returnData}""]");

        try {
            unsafe { Callback(ExtensionName, method, returnData); }
        } catch (Exception ex) {
            Events.RaiseErrorOccurred(ex);
            Error(ex.Message);
        }
    }


    internal static int WriteOutput(nint output, int outputSize, string methodName, string message, int returnCode = 0) {
        Debug(@$"EXTENSION >> ARMA >> ({methodName}) >> {message}");

        byte[] bytes = Encoding.ASCII.GetBytes(message);
        int length = Math.Min(bytes.Length, outputSize - 1);
        Marshal.Copy(bytes, 0, output, length);
        Marshal.WriteByte(output, length, 0);

        return returnCode;
    }
}