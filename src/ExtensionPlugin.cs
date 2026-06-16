
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using static ArmaExtension.Logger;
using ArmaExtension;

using EdenOnline;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using DynTypeNetwork;
using System.Reflection;
using static DynTypeNetwork.MethodBuilder;

namespace EdenOnline;

public static class Constants {
    // Development-only flag for solo testing. Enables mirror mode so the sender also receives its own messages
    public const bool MIRROR = true;
}


[ArmaExtensionPlugin]
public static partial class ArmaMethods {
 

    // ! INITIALIZED WHEN FIRST EXTENSION CALL IS MADE
    // If just public static void is used in Main(), it will block the Arma 3 until this method is finished
    // If its using public static async Task, this will not block the Arma 3, but events might not have been registered yet.
    public static void Main()
    {
        Log("Called EdenOnline Main method");
        CurrentLogLevel = LogLevel.Debug;
        MessageBuilder.DEBUG = false;

        MethodSystem.RegisterMethods(typeof(ArmaMethods)); // Always register your methods
        
        
        // Subscribe to events
        // The Events class prefixes all event names with "On". Use the correct identifiers below.
        /*
        Events.OnVersionCalled += version => Debug($"VersionCalled event triggered with version: {version}");

        Events.OnMethodCalled += methodName => Debug($"MethodCalled event triggered with method: {methodName}");
        Events.OnMethodCalledResponse += (methodName, response, success) => Debug($"MethodCalledResponse event: {methodName} with response count: {response?.Length ?? 0}, success: {success}");

        Events.OnMethodCalledWithArgs += (methodName, args) => Debug($"MethodCalledWithArgs event: {methodName} with args count: {args?.Length ?? 0}");
        Events.OnMethodCalledWithArgsResponse += (methodName, response, success) => Debug($"MethodCalledWithArgsResponse event: {methodName} with response count: {response?.Length ?? 0}, success: {success}");

        Events.OnAsyncTaskStarted += (method, asyncKey, args) => Debug($"AsyncTaskStarted event triggered with method: {method}, asyncKey: {asyncKey}, args count: {args?.Length ?? 0}");
        Events.OnAsyncTaskCompleted += (method, asyncKey, response, success) => Debug($"AsyncTaskCompleted event triggered with method: {method}, asyncKey: {asyncKey}, success: {success}, response count: {response?.Length ?? 0}");
        Events.OnAsyncTaskCancelled += (asyncKey, success) => Debug($"AsyncTaskCancelled event triggered with asyncKey: {asyncKey}, success: {success}");

        Events.OnSendToArma += (method, data) => Debug($"OnSendToArma event triggered with method: {method}, data count: {data?.Length ?? 0}");
        */
        
        Events.OnErrorOccurred += ex => Debug($"ErrorOccurred event triggered: {ex.Message}");

        UIEvents.OnLButtonUp += (obj, x, y) => {
            Console.WriteLine($"LButtonUp triggered at {x}, {y}");
        };

        Log("EdenOnline Extension Initialized");
    }



    private static void PrintAvailableMethods(string name, RpcMethodInfo[] methods)
    {
        const int BannerWidth = 84;

        static string FormatType(Type type)
        {
            if (type.IsArray)
                return $"{FormatType(type.GetElementType()!)}[]";

            if (type.IsGenericType)
            {
                string genericName = type.Name.Split('`')[0];
                string genericArgs = string.Join(", ",
                    type.GetGenericArguments().Select(FormatType));

                return $"{genericName}<{genericArgs}>";
            }

            return type.Name;
        }

        static string FormatMethod(RpcMethodInfo method)
        {
            string parameters = string.Join(", ",
                method.Parameters.Select(p => $"{FormatType(p.Type)} {p.Name}"));

            string returnType = method.ReturnType is null
                ? "void"
                : FormatType(method.ReturnType);

            return $"{method.Name}({parameters}) : {returnType}";
        }

        string header = $" Registered {name} Network Methods ";

        Log();
        Log(header.PadLeft((BannerWidth + header.Length) / 2, '=').PadRight(BannerWidth, '='));

        foreach (var method in methods.OrderBy(m => m.Name))
            Log(FormatMethod(method));

        Log(new string('=', BannerWidth));
        Log();
    }
}