
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using static ArmaExtension.Logger;
using ArmaExtension;

using EdenOnline.Models;
using EdenOnline.Network;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace EdenOnline;


[ArmaExtensionPlugin]
public static class ArmaMethods {
    public static string Version() {
        return Extension.Version;
    }

    public static async Task<object[]> Connect(string host, int port, string username, string worldname, string armaVersion, object[] modHashes, string password = "") {
        if (!Client.IsConnected) throw new Exception("Client is already connected!");

        string clientHash = GetHash(new object[] {modHashes, Extension.Version, armaVersion});
        Log($"Connect Method Called: {host}:{port}, world: {worldname}, username: {username},  modHashes: {string.Join(",", modHashes)}, clientHash: {clientHash}, password: {password}");

        (int clientID, object[] otherClients) = await Client.Connect(host, port, username, worldname, clientHash, password);


        return [clientID, otherClients];
    }

    public static async Task<object[]> StartServer(double port, string username, string worldname, string armaVersion, object[] modHashes, string password = "null") {
        string clientHash = GetHash(new object[] {modHashes, Extension.Version, armaVersion});

        Server.Start((int)port, clientHash, worldname, password);
        (int clientID, object[] otherClients) = await Client.Connect("127.0.0.1", (int)port, username, worldname, clientHash, password);

        return [clientID, otherClients];
    }
    }

    public static bool Disconnect() {
        Log("Disconnect Method Called");
        
        if (Server.IsRunning) {
            Server.Stop();
        }

        Client.Disconnect();

        return true;
    }

    public static string CreateObject(string objectID, Dictionary<string, object?> metadata) {
        if (!Client.IsConnected) throw new Exception("Client is not connected. Cannot create object.");

        ArmaObject obj = new(objectID, metadata);

        NetworkHelper.SendClientMessage(MessageType.ObjectCreate, obj);

        return obj.Id;
    }

    public static void UpdateObject(string objectID, Dictionary<string, object?> metadata) {
        if (!Client.IsConnected) throw new Exception("Client is not connected. Cannot update object.");
        
        ArmaObject obj = new(objectID, metadata);
        
        NetworkHelper.SendClientMessage(MessageType.ObjectUpdate, obj);
    }

    public static bool RemoveObject(string objectID) {
        if (!Client.IsConnected) throw new Exception("Client is not connected. Cannot remove object.");

        ArmaObject obj = new(objectID);
        NetworkHelper.SendClientMessage(MessageType.ObjectRemove, obj);
        return true;
    }


    public static string GetHash(object item)
    {
        return HashUtils.GetHash(item);
    }



    // ! INITIALIZED WHEN FIRST EXTENSION CALL IS MADE
    // If just public static void is used in Main(), it will block the Arma 3 until this method is finished
    // If its using public static async Task, this will not block the Arma 3, but events might not have been registered yet.
    public static void Main()
    {
        Log("Called EdenOnline Main method");
        CurrentLogLevel = LogLevel.Debug;

        MethodSystem.RegisterMethods(typeof(ArmaMethods)); // Always register your methods
        
        
        // Subscribe to events
        // The Events class prefixes all event names with "On". Use the correct identifiers below.
        Events.OnVersionCalled += version => Debug($"VersionCalled event triggered with version: {version}");

        Events.OnMethodCalled += methodName => Debug($"MethodCalled event triggered with method: {methodName}");
        Events.OnMethodCalledResponse += (methodName, response, success) => Debug($"MethodCalledResponse event: {methodName} with response count: {response?.Length ?? 0}, success: {success}");

        Events.OnMethodCalledWithArgs += (methodName, args) => Debug($"MethodCalledWithArgs event: {methodName} with args count: {args?.Length ?? 0}");
        Events.OnMethodCalledWithArgsResponse += (methodName, response, success) => Debug($"MethodCalledWithArgsResponse event: {methodName} with response count: {response?.Length ?? 0}, success: {success}");

        Events.OnAsyncTaskStarted += (method, asyncKey, args) => Debug($"AsyncTaskStarted event triggered with method: {method}, asyncKey: {asyncKey}, args count: {args?.Length ?? 0}");
        Events.OnAsyncTaskCompleted += (method, asyncKey, response, success) => Debug($"AsyncTaskCompleted event triggered with method: {method}, asyncKey: {asyncKey}, success: {success}, response count: {response?.Length ?? 0}");
        Events.OnAsyncTaskCancelled += (asyncKey, success) => Debug($"AsyncTaskCancelled event triggered with asyncKey: {asyncKey}, success: {success}");

        Events.OnSendToArma += (method, data) => Debug($"OnSendToArma event triggered with method: {method}, data count: {data?.Length ?? 0}");
        
        Events.OnErrorOccurred += ex => Debug($"ErrorOccurred event triggered: {ex.Message}");

        UIEvents.OnLButtonUp += (obj, x, y) => {
            Console.WriteLine($"LButtonUp triggered at {x}, {y}");
        };

        Log("EdenOnline Extension Initialized");
    }
}