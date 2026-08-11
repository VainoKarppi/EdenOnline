
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


public static partial class ArmaMethods {
    public static Dictionary<int, string> UsernameList { get; set; } = [];
    public static string Version() {
        return Extension.Version;
    }

    public static async Task<int> Connect(string host, int port, string username, string worldname, string armaVersion, object[] modHashes, string password = "") {
        if (Client.IsTcpConnected() && !Settings.ALLOW_DUAL_CONNECTIONS) throw new Exception("Client is already connected. Please disconnect before connecting again.");

        string clientHash = GetHash(new object[] {modHashes, Extension.Version, armaVersion, worldname, password});
        Log($"[CLIENT] Connect Method Called: {host}:{port}, world: {worldname}, username: {username},  modHashes: {string.Join(",", modHashes)}, clientHash: {clientHash}, password: {password}");
        
        int clientID = await Client.ConnectAsync(host, port, startUdp: true, clientHash);


        // TODO Send network message to other clients to start loading screen to block edits. Wait for 1 second, before starting sync.
        // TODO Create backup on server to toggle loading screen off, if this client disconnects or loses connection while syncinc.
        
        // subscribe events
        Client.OnClientConnected += ClientNetworkEvents.OnConnected;
        Client.OnClientDisconnected += ClientNetworkEvents.OnDisconnected;
        Client.OnServerShutdown += ClientNetworkEvents.OnServerShutdown;
        Client.OnOtherClientConnected += ClientNetworkEvents.OnOtherClientConnected;
        Client.OnOtherClientDisconnected += ClientNetworkEvents.OnOtherClientDisconnected;

        //* SEND AND REQUEST USERNAMES FROM SERVER
        await Client.SendTcpMessageAsync(1, "RegisterUserName", clientID, username);
        Log($"[CLIENT] Syncing client list and usernames...");

        UsernameList = await Client.RequestTcpDataAsync<Dictionary<int, string>>(1, "GetAllUsernames") ?? [];
        Log($"[CLIENT] Got {UsernameList?.Count} users");
        if (UsernameList != null && UsernameList.Count > 0)
        {
            object[] otherUsersArray = UsernameList
                .Where(x => x.Key != clientID)
                .Select(kvp => new object[] { kvp.Key, kvp.Value })
                .Cast<object>()
                .ToArray();
            
            Extension.SendToArma("UpdateClientList", [otherUsersArray]);
        }

        // TODO verify user count
        Log($"[CLIENT] Received {UsernameList?.Count ?? 0} users. Should be: {Client.GetOtherClients().Count}");


        //* MISSION ATTRIBUTES SYNC
        Dictionary<string[], object?>? missionAttributes = await Client.RequestTcpDataAsync<Dictionary<string[], object?>>(1, "GetMissionAttributes");
        if (missionAttributes == null) {
            throw new Exception("Failed to sync mission attributes: Received null from server");
        }

        Log($"[CLIENT] Received {missionAttributes.Count} mission attributes from server.");

        object?[] attributes = missionAttributes
            .Select(kvp => (object?)new object?[]
            {
                kvp.Key.Length > 0 ? kvp.Key[0] : "", // section
                kvp.Key.Length > 1 ? kvp.Key[1] : "", // property
                kvp.Value
            })
            .ToArray();

        Extension.SendToArma("SetInitialMissionAttributes", [attributes]);


        //* MISSION OBJECTS SYNC
        Log($"[CLIENT] Requesting object count from server...");
        int objectCount = await Client.RequestTcpDataAsync<int>(1, "GetObjectCount");
        Extension.SendToArma("ObjectSyncCount", [objectCount]);

        if (objectCount > 0) {
            List<ArmaObject>? objects = await Client.RequestTcpDataAsync<List<ArmaObject>>(1, "GetAllObjects");
            if (objects == null || objects.Count == 0) {
                throw new Exception("Failed to sync objects: Received null from server");
            }

            foreach (var obj in objects) {
                Extension.SendToArma("ObjectSyncData", [obj.Id, obj.Attributes]);
            }
            
        }
        Log($"[CLIENT] Object sync complete. Total objects synced: {objectCount}");


        // TODO Send initial client camera positions.

        Log($"[CLIENT] Connect Method Finished: {host}:{port}");
        Log($"[CLIENT] Connected with ID: {clientID}");
        return clientID;
    }

    public static async Task<int> StartServer(double port, string username, string worldname, string armaVersion, object[] modHashes, string password = "null") {
        if (Client.IsTcpConnected()) throw new Exception("Server is already running. Please disconnect the client before starting a server.");
    
        string clientHash = GetHash(new object[] {modHashes, Extension.Version, armaVersion, worldname, password});

        RegisterServerMethods(new ServerNetworkMethods());
        PrintAvailableMethods("Server", GetAvailableServerMethods());

        await Server.StartAsync((int)port, true, clientHash);

        // Subscribe events
        Server.OnClientConnected += ServerNetworkEvents.OnClientConnected;
        Server.OnClientDisconnected += ServerNetworkEvents.OnClientDisconnected;
        Server.OnServerShutdown += ServerNetworkEvents.OnServerShutdown;

        int clientId = await Connect("127.0.0.1", (int)port, username, worldname, armaVersion, modHashes, password);

        return clientId;
    }

    public static async Task CameraUpdate(object[] position, object[] direction) {
        try {
            if (!Client.IsUdpConnected()) throw new Exception("Client is not connected. Cannot send camera position.");

            ArmaCamera camera = new() {
                Id = Client.ClientID,
                Position = position,
                Direction = direction
            };

            await Client.SendUdpMessageAsync(-1, "UpdateCamera", camera);
        } catch (Exception ex)
        {
            Log(ex);
            throw;
        }
    }
    
    public static async Task SetMissionAttribute(string section, string property, object value) {
        if (!Client.IsTcpConnected()) throw new Exception("Client is not connected. Cannot send mission attributes.");

        MissionAttribute attribute = new(section, property, value);

        await Client.SendTcpMessageAsync(0, "SetMissionAttribute", attribute);
    }

    public static async Task SetInitialMissionAttributes(object[] attributes) {
        if (!Client.IsTcpConnected()) throw new Exception("Client is not connected. Cannot send mission attributes.");

        MissionAttribute[] attributeList = attributes
            .Cast<object[]>()
            .Select(attrb => new MissionAttribute(
                (string)attrb[0],
                (string)attrb[1],
                attrb[2]
            ))
            .ToArray();

        await Client.SendTcpMessageAsync(1, "SetInitialMissionAttributes", [attributeList]);
    }

    public static async Task<bool> Disconnect() {

        if (Server.IsTcpServerRunning())
        {
            Log("[CLIENT] Disconnect requested >> Client is hosting the server >> Stopping server");
            await Server.StopAsync();
            await Client.ResetConnectionStatusAsync();
        }
        else
        {
            Log("[CLIENT] Disconnect requested >> Client is connected to a remote server >> Disconnecting");
            await Client.DisconnectAsync();
        }

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

    public static async Task RemoveObject(string objectID) {
        if (!Client.IsTcpConnected()) throw new Exception("Client is not connected. Cannot remove object.");

        await Client.SendTcpMessageAsync(0, "RemoveObject", objectID);
    }


    public static string GetHash(object item)
    {
        return HashUtils.GetHash(item);
    }
}