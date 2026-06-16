
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


// These are methods that other clients can invoke remotely, and request data for

public class ServerNetworkMethods {
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

public class ServerNetworkEvents {
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