
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

public class ServerMethods {
    public static string GetServerTime() {
        return DateTime.UtcNow.ToString("o");
    }

    public static int GetObjectCount() {
        return ObjectManager.Objects.Count;
    }

    public static List<ArmaObject> GetAllObjects() {
        return ObjectManager.GetAllObjects();
    }

    public static object[] GetClients(NetworkMessage message)
    {
        var clients = Server.Clients.Values
            .Where(c => c.Id != message.SenderId && c.HandshakeDone)
            .Select(c => new object[] { c.Id, "TEMP" })
            .ToArray<object>();

        return clients.Cast<object>().ToArray();
    }

    public static void SetMissionAttribute(string section, string property, object? value) {
        Log($"SERVER: Received SetMissionAttribute for section: {section}, property: {property}, value: {value}");
        // Store to server memory, so that new clients can get the latest mission attributes when they connect
        MissionAttributeManager.SetAttribute([property, section], value!);
    }
}

public class ServerEvents {
    public static async void OnClientConnected(int clientId) {
        Log($"Client connected with ID:{clientId}");

        // TODO send message to other clients, to freeze their games until sync has been done with the new client, to prevent desync issues.
        //await Server.SendTcpMessageAsync(-clientId, "LoadingScreen", [true, 50]);

        var clients = Server.Clients.Values
            .Where(c => c.HandshakeDone)
            .Select(c => new object[] { c.Id, "TEMP" })
            .ToArray<object>();
        
        await Server.SendTcpMessageAsync(-1, "UpdateClientList", clients);

        //await Task.Delay(500); // Wait a moment to make sure other clients are in loadign screen, before starting object sync, to prevent desync issues.

        //await Server.SendTcpMessageAsync(-clientId, "LoadingScreen", [false, 100]);
    }

    public static async void OnClientDisconnected(int clientId, bool success) {
        Log($"Client disconnected with ID:{clientId}, SUCCESS:{success}");

        var clients = Server.Clients.Values
            .Where(c => c.HandshakeDone)
            .Select(c => new object[] { c.Id, "TEMP" })
            .ToArray<object>();
        
        await Server.SendTcpMessageAsync(0, "UpdateClientList", clients);
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

    public static void UpdateClientList(object[] clients) {
        Log($"Updated ClientList {string.Join(", ", clients.Cast<object[]>().Select(c => $"ID: {c[0]}, Username: {c[1]}"))}");

        object[] filteredClients = clients
            .Cast<object[]>()
            .Where(client => client.Length > 0 && client[0] is int id && id != Client.ClientID)
            .Cast<object>()
            .ToArray();

        if (filteredClients.Length == 0) {
            Console.WriteLine("No other clients connected.");
        } else {
            Console.WriteLine($"Other connected clients: {string.Join(", ", filteredClients.Cast<object[]>().Select(c => $"ID: {c[0]}, Username: {c[1]}"))}");
            Extension.SendToArma("ClientListUpdate", [filteredClients]);
        }
    }

    public static void LoadingScreen(bool enable) {
        Log($"Received LoadingScreen: {enable}");
        Extension.SendToArma("LoadingScreen", [enable, 50 / 100.0]);
    }

    public static void SetMissionAttribute(string section, string property, object? value) {
        Log($"Received SetMissionAttribute for section: {section}, property: {property}, value: {value}");
        Extension.SendToArma("MissionAttributeUpdated", [section, property, value]);
    }
}

public class ClientEvents {
    public static async void OnConnected(int clientId) {
        Log($"Client connected to server with ID: {clientId}");

        // TODO send initial missionAttributes

        int objectCount = await Client.RequestDataAsync<int>(0, "GetObjectCount");
        Extension.SendToArma("ObjectSyncCount", [objectCount]);

        if (objectCount > 0) {
            List<ArmaObject>? objects = await Client.RequestDataAsync<List<ArmaObject>>(0, "GetAllObjects");
            if (objects == null || objects.Count == 0) {
                Log("Failed to sync objects: Received null from server");
                return;
            }

            foreach (var obj in objects) {
                Extension.SendToArma("ObjectSyncData", [obj.Id, obj.Attributes]);
            }
            
        }

    }

    public static void OnDisconnected(bool success) {
        Log("Client disconnected from server.");
    }

    public static void OnServerShutdown(bool intentional) {
        Log($"Server shutdown event received. Intentional: {intentional}");
        Extension.SendToArma("ServerShutdown", [intentional]);
    }
}


[ArmaExtensionPlugin]
public static class ArmaMethods {
    public static string Version() {
        return Extension.Version;
    }

    public static async Task<int> Connect(string host, int port, string username, string worldname, string armaVersion, object[] modHashes, string password = "") {
        string clientHash = GetHash(new object[] {modHashes, Extension.Version, armaVersion});
        Log($"Connect Method Called: {host}:{port}, world: {worldname}, username: {username},  modHashes: {string.Join(",", modHashes)}, clientHash: {clientHash}, password: {password}");

        RegisterClientMethods(new ClientMethods());
        PrintAvailableMethods("Client", GetAvailableClientMethods());

        int clientID = await Client.ConnectAsync(host, port, startUdp: true, clientHash);

        // subscribe events
        Client.OnClientConnected += ClientEvents.OnConnected;
        Client.OnClientDisconnected += ClientEvents.OnDisconnected;
        Client.OnServerShutdown += ClientEvents.OnServerShutdown;


        // TODO SYNC objects, mission attributes, etc here before returning from connect method, so that client has the latest data when they receive the "Connected" event in Arma.

        return clientID;
    }

    public static async Task<int> StartServer(double port, string username, string worldname, string armaVersion, object[] modHashes, string password = "null") {
        try {
            string clientHash = GetHash(new object[] {modHashes, Extension.Version, armaVersion, worldname, password});

            RegisterServerMethods(new ServerMethods());
            PrintAvailableMethods("Server", GetAvailableServerMethods());

            await Server.StartAsync((int)port, true);

            // Subscribe events
            Server.OnClientConnected += ServerEvents.OnClientConnected;
            Server.OnClientDisconnected += ServerEvents.OnClientDisconnected;

            int clientId = await Connect("127.0.0.1", (int)port, username, worldname, armaVersion, modHashes, password);

            return clientId;
        } catch (Exception ex) {
            Log($"Error starting server: {ex.Message}");
            Console.WriteLine(ex);
            return -1;
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

        MissionAttribute attribute = new(section, property, value);

        await Client.SendTcpMessageAsync(0, "SetMissionAttribute", attribute);
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
        MessageBuilder.DEBUG = true;

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
        Console.WriteLine($"\n=== {name} Methods ===");
        foreach (var method in methods)
        {
            string parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type.Name} {p.Name}"));
            string returnType = method.ReturnType?.Name ?? "void";
            Console.WriteLine($"{method.Name}({parameters}) : {returnType}");
        }
        Console.WriteLine("=====================\n");
    }
}