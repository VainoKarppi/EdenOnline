
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

namespace EdenOnline;

public class ServerMethods {
    public static string GetServerTime() {
        return DateTime.UtcNow.ToString("o");
    }
}

public class ClientMethods {
    public static void UpdateCamera(ArmaCamera camera) {
        Log($"Received camera update from client {camera.Id}: Position: {string.Join(",", camera.Position)}, Direction: {string.Join(",", camera.Direction)}");
        Extension.SendToArma("CameraUpdate", [camera.Id, camera.Position, camera.Direction]);
    }

    public static void CreateObject(ArmaObject createdObj) {
        Log($"Received CreateObject for object {createdObj.Id} with attributes: {JsonSerializer.Serialize(createdObj.Attributes)}");
        Extension.SendToArma("ObjectCreated", [createdObj.Id, createdObj.Attributes]);
    }
    public static void UpdateObject(ArmaObject updatedObj) {
        Log($"Received UpdateObject for object {updatedObj.Id} with attributes: {JsonSerializer.Serialize(updatedObj.Attributes)}");
        Extension.SendToArma("ObjectUpdated", [updatedObj.Id, updatedObj.Attributes]);
    }
    public static void RemoveObject(ArmaObject removedObj) {
        Log($"Received RemoveObject for object {removedObj.Id}");
        Extension.SendToArma("ObjectRemoved", [removedObj.Id]);
    }

    public static void SetMissionAttribute(string section, string property, object value) {
        Log($"Received SetMissionAttribute for section: {section}, property: {property}, value: {value}");
        Extension.SendToArma("MissionAttributeUpdated", [section, property, value]);
    }
}


[ArmaExtensionPlugin]
public static class ArmaMethods {
    public static string Version() {
        return Extension.Version;
    }

    public static async Task<object[]> Connect(string host, int port, string username, string worldname, string armaVersion, object[] modHashes, string password = "") {
        if (!Client.IsTcpConnected()) throw new Exception("Client is already connected!");

        string clientHash = GetHash(new object[] {modHashes, Extension.Version, armaVersion});
        Log($"Connect Method Called: {host}:{port}, world: {worldname}, username: {username},  modHashes: {string.Join(",", modHashes)}, clientHash: {clientHash}, password: {password}");

        int clientID = await Client.ConnectAsync(host, port, username, true, clientHash);
        var otherClients = Client.GetOtherClients();


        // TODO SYNC objects, mission attributes, etc here before returning from connect method, so that client has the latest data when they receive the "Connected" event in Arma.

        return [clientID, otherClients];
    }

    public static async Task<object[]> StartServer(double port, string username, string worldname, string armaVersion, object[] modHashes, string password = "null") {
        try {
            string clientHash = GetHash(new object[] {modHashes, Extension.Version, armaVersion, worldname, password});

            MethodBuilder.RegisterServerMethods(new ServerMethods());

            await Server.StartAsync((int)port, true);

            int clientId = await Client.ConnectAsync("127.0.0.1", (int)port, username, true, clientHash);

            var otherClients = Client.GetOtherClients();

            // TODO otherClients data needs to be in format: [[playerId, playerName], [...]]

            return [clientId, otherClients.ToArray()];
        } catch (Exception ex) {
            Log($"Error starting server: {ex.Message}");
            Console.WriteLine(ex);
            return [-1, Array.Empty<object>()];
        }
    }

    public static async Task CameraUpdate(object[] position, object[] direction) {
        if (!Client.IsUdpConnected()) throw new Exception("Client is not connected. Cannot send camera position.");


        ArmaCamera camera = new() {
            Id = Client.ClientID,
            Position = position,
            Direction = direction
        };

        await Client.SendUdpMessageAsync(0, "UpdateCamera", camera);
    }
    
    public static async Task SetMissionAttribute(string section, string property, object value) {
        if (!Client.IsTcpConnected()) throw new Exception("Client is not connected. Cannot send mission attributes.");

        await Client.SendTcpMessageAsync(0, "SetMissionAttribute", section, property, value);
    }

    public static async Task<bool> Disconnect() {
        Log("Disconnect Method Called");
        
        if (Server.IsTcpServerRunning()) {
            await Server.StopAsync();
        }

        await Client.DisconnectAsync();

        return true;
    }

    public static async Task<string> CreateObject(string objectID, Dictionary<string, object?> metadata) {
        if (!Client.IsTcpConnected()) throw new Exception("Client is not connected. Cannot create object.");

        ArmaObject obj = new(objectID, metadata);

        await Client.SendTcpMessageAsync(0, "CreateObject", obj);

        return obj.Id;
    }

    public static async Task UpdateObject(string objectID, Dictionary<string, object?> metadata) {
        if (!Client.IsTcpConnected()) throw new Exception("Client is not connected. Cannot update object.");
        
        ArmaObject obj = new(objectID, metadata);
        
        await Client.SendTcpMessageAsync(0, "UpdateObject", obj);
    }

    public static async Task<bool> RemoveObject(string objectID) {
        if (!Client.IsTcpConnected()) throw new Exception("Client is not connected. Cannot remove object.");

        ArmaObject obj = new(objectID);
        await Client.SendTcpMessageAsync(0, "RemoveObject", obj);

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
        MessageBuilder.DEBUG = false;

        MethodSystem.RegisterMethods(typeof(ArmaMethods)); // Always register your methods

        Log("=== Custom Methods ===");

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
            var asmName = asm.GetName().Name;
 
            // Filter ONLY your assemblies
            if (asmName is null) continue;

            if (!asmName.StartsWith("ArmaExtension") &&
                !asmName.StartsWith("EdenOnline") &&
                !asmName.StartsWith("DynType")) continue;

            try {
                foreach (var type in asm.GetTypes()) {
                    // Skip compiler/system noise
                    if (!type.IsClass || type.IsAbstract && type.IsSealed) continue;

                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);

                    foreach (var method in methods) {
                        if (method.IsSpecialName) continue;

                        var parameters = method.GetParameters();
                        var paramString = string.Join(", ",
                            parameters.Select(p => $"{p.ParameterType.Name} {p.Name}")
                        );

                        Log($"[{asmName}] {type.FullName}.{method.Name}({paramString}) : {method.ReturnType.Name}");
                    }
                }
            }
            catch (Exception ex) {
                Log($"Failed to inspect assembly {asmName}: {ex.Message}");
            }
        }

        Log("=== End Custom Methods ===");

        
        MethodBuilder.RegisterClientMethods(new ClientMethods());
        
        
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