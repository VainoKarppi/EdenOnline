using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using static EdenOnline.ArmaLog;
using static DynTypeNetwork.MethodBuilder;

namespace EdenOnline;

/// <summary>
/// Entry point for the EdenOnline extension.
/// Called by Arma 3's callExtension mechanism via the Extension framework.
/// </summary>
[EdenOnlinePlugin]
public static partial class ExtensionPlugin
{
    /// <summary>
    /// Called on first extension invocation. Registers all methods and event handlers.
    /// This method is synchronous to ensure events are registered before Arma proceeds.
    /// </summary>
    public static void Main()
    {
        ConfigureLogging();

        Log("Called EdenOnline Main method");

        DynTypeNetwork.Settings.EnableVersionCheck = false;

        // Start RestAPI server
        _ = Task.Run(() => ArmaMethods.StartApiServerAsync());

        // Register all extension methods callable from Arma
        MethodSystem.RegisterMethods(typeof(ArmaMethods));

        // Register client-side network RPC methods
        RegisterClientMethods(new ClientNetworkMethods());
        PrintAvailableMethods("Client", GetAvailableClientMethods());

        // Wire up error and UI events
        Events.OnErrorOccurred += ex => Debug($"ErrorOccurred event triggered: {ex.Message}");

        UIEvents.OnLButtonUp += (obj, x, y) =>
        {
            Log($"LButtonUp triggered at {x}, {y}");
        };

        Log("EdenOnline Extension Initialized");
    }

    /// <summary>
    /// Composition root: wires up the file-backed loggers for every subsystem
    /// this host references. Core initializes itself; ArmaPlugin, DynTypeSerializer
    /// and DynTypeNetwork each get their own log file here.
    /// </summary>
    private static void ConfigureLogging() {
        ArmaLog.Configure(Logging.LogFactory.CreateFileLoggerFactory(Extension.LogFolder, "ArmaPlugin.log"));

        DynTypeSerializer.Logging.SerializerLogging.Configure(Logging.LogFactory.CreateFileLoggerFactory(Extension.LogFolder, "DynTypeSerializer.log").CreateLogger("DynTypeSerializer"));

        DynTypeNetwork.Log.Configure(Logging.LogFactory.CreateFileLoggerFactory(Extension.LogFolder, "Network.log"));
    }

    /// <summary>
    /// Prints a formatted banner of all registered network methods for debugging.
    /// </summary>
    public static void PrintAvailableMethods(string name, RpcMethodInfo[] methods) {
        const int bannerWidth = 84;

        string header = $" Registered {name} Network Methods ";
        Log();
        Log(header.PadLeft((bannerWidth + header.Length) / 2, '=').PadRight(bannerWidth, '='));

        foreach (var method in methods.OrderBy(m => m.Name)) {
            Log(FormatMethodSignature(method));
        }

        Log(new string('=', bannerWidth));
        Log();
    }

    private static string FormatMethodSignature(RpcMethodInfo method) {
        string parameters = string.Join(", ", method.Parameters.Select(p => $"{FormatTypeName(p.Type)} {p.Name}"));

        string returnType = method.ReturnType is null ? "void" : FormatTypeName(method.ReturnType);

        return $"{method.Name}({parameters}) : {returnType}";
    }

    private static string FormatTypeName(Type type) {
        if (type.IsArray) return $"{FormatTypeName(type.GetElementType()!)}[]";

        if (type.IsGenericType) {
            string genericName = type.Name.Split('`')[0];
            string genericArgs = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));

            return $"{genericName}<{genericArgs}>";
        }

        return type.Name;
    }
}
