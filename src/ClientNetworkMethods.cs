
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

// These are methods that other clients or the server can invoke remotely, and request data for

public class ClientNetworkMethods {
    public static void UpdateCamera(ArmaCamera camera) {
        Log($"Received camera update from client {camera.Id}: Position: {string.Join(",", camera.Position)}, Direction: {string.Join(",", camera.Direction)}");
        Extension.SendToArma("CameraUpdate", [camera.Id, camera.Position, camera.Direction]);
    }

    public static void CreateObject(ArmaObject createdObj) {
        Log($"Received CreateObject for object {createdObj.Id} with attributes: {createdObj.Attributes?.Count}");
        Extension.SendToArma("ObjectCreated", [createdObj.Id, createdObj.Attributes]);
    }
    public static void UpdateObject(ArmaObject updatedObj) {
        Log($"Received UpdateObject for object {updatedObj.Id} with attributes: {updatedObj.Attributes?.Count}");
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

public class ClientNetworkEvents {
    public static async void OnConnected(int clientId) {
        Log($"Client connected to server with ID: {clientId}");

        // TODO send initial missionAttributes

        int objectCount = await Client.RequestDataAsync<int>(1, "GetObjectCount");
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