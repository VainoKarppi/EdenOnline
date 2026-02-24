
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Threading.Tasks;
using ArmaExtension;
using static ArmaExtension.Logger;

namespace ArmaExtension; // Do not change the namespace, it is updated automatically by the build system.


[ArmaExtensionPlugin]
public static partial class EdenOnline {
    public static class ArmaMethods {
        // CALLED FROM ARMA USING:
        // _data = "ArmaExtension" callExtension "Version";
        // _data == "[""SUCCESS"",[""1.0.0.0""]]"
        public static string Version() {
            return Extension.Version;
        }

        public static bool Connect(string ip, int port, string username, string worldname, string armaVersion, object[] mods, string password) {
            string clientHash = GetHash(new object[] {worldname, mods, Extension.Version, armaVersion});
            Log($"Connect Method Called: {ip}:{port}, world: {worldname}, username: {username},  mods: {string.Join(",", mods)}, clientHash: {clientHash}, password: {password}");

            Client.Connect(ip, port, username, clientHash);
            return true;
        }

        public static bool StartServer(string username, double port, string worldname, string armaVersion, object[] mods, string? password = null) {

            string clientHash = GetHash(new object[] {worldname, mods, Extension.Version, armaVersion});
            Log($"Starting server: {clientHash} for world: {worldname}, username: {username}, mods: {string.Join(",", mods)}, clientHash: {clientHash}, password: {password}");

            Server.Start(clientHash, password);
            Client.Connect("127.0.0.1", (int)port, username, clientHash);
            return true;
        }

        public static string GetHash(object item) {
            return item.GetHashCode().ToString();
        }

    }



    // ! INITIALIZED WHEN FIRST EXTENSION CALL IS MADE
    // If just public static void is used in Main(), it will block the Arma 3 until this method is finished
    // If its using public static async Task, this will not block the Arma 3, but events might not have been registered yet.
    public static void Main()
    {
        Extension.RegisterMethods(typeof(ArmaMethods)); // Always register your methods

        // Subscribe to events
        Extension.VersionCalled += version => Log($"VersionCalled event triggered with version: {version}");
        Extension.MethodCalled += methodName => Log($"MethodCalled event triggered with method: {methodName}");

        Extension.MethodCalledWithArgs += (methodName, args) => Log($"MethodCalledWithArgs event: {methodName} with args: {JsonSerializer.Serialize(args)}");
        Extension.MethodCalledWithArgsResponse += (methodName, response, success) => Log($"MethodCalledWithArgsAndReturn event: {methodName} with response: {JsonSerializer.Serialize(response)}, success: {success}");

        Extension.AsyncTaskStarted += (method, asyncKey, args) => Log($"AsyncTaskStarted event triggered with method: {method}, asyncKey: {asyncKey}, args: {JsonSerializer.Serialize(args)}");
        Extension.AsyncTaskCompleted += (method, asyncKey, response, success) => Log($"AsyncTaskCompleted event triggered with method: {method}, asyncKey: {asyncKey}, success: {success}, response: {JsonSerializer.Serialize(response)}");
        Extension.AsyncTaskCancelled += (asyncKey, success) => Log($"AsyncTaskCancelled event triggered with asyncKey: {asyncKey}, success: {success},");

        Extension.OnSendToArma += (method, data) => Log($"OnSendToArma event triggered with method: {method}, data: {JsonSerializer.Serialize(data)},");

        Extension.ErrorOccurred += ex => Log($"ErrorOccurred event triggered: {ex}");


        Log("EdenOnline Extension Initialized");
    }
}