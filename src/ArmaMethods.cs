
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
    public static string Version() {
        return Extension.Version;
    }

    public static async Task<int> Connect(string host, int port, string username, string worldname, string armaVersion, object[] modHashes, string password = "") {
        string clientHash = GetHash(new object[] {modHashes, Extension.Version, armaVersion});
        Log($"Connect Method Called: {host}:{port}, world: {worldname}, username: {username},  modHashes: {string.Join(",", modHashes)}, clientHash: {clientHash}, password: {password}");

        RegisterClientMethods(new ClientNetworkMethods());
        PrintAvailableMethods("Client", GetAvailableClientMethods());

        int clientID = await Client.ConnectAsync(host, port, startUdp: true, clientHash);

        // subscribe events
        Client.OnClientConnected += ClientNetworkEvents.OnConnected;
        Client.OnClientDisconnected += ClientNetworkEvents.OnDisconnected;
        Client.OnServerShutdown += ClientNetworkEvents.OnServerShutdown;


        // TODO SYNC objects, mission attributes, etc here before returning from connect method, so that client has the latest data when they receive the "Connected" event in Arma.

        return clientID;
    }

    public static async Task<int> StartServer(double port, string username, string worldname, string armaVersion, object[] modHashes, string password = "null") {
        try {
            string clientHash = GetHash(new object[] {modHashes, Extension.Version, armaVersion, worldname, password});

            RegisterServerMethods(new ServerNetworkMethods());
            PrintAvailableMethods("Server", GetAvailableServerMethods());

            await Server.StartAsync((int)port, true);

            // Subscribe events
            Server.OnClientConnected += ServerNetworkEvents.OnClientConnected;
            Server.OnClientDisconnected += ServerNetworkEvents.OnClientDisconnected;

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

        Debug($"Sent CreateObject Message");

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
}