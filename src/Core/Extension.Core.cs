using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
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

        Log(@$"EXTENSION CALLBACK >> ARMA >> [""{ExtensionName}"", ""{method}"", ""{returnData}""]");

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