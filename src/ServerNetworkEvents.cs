
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



public class ServerNetworkEvents {
    public static async void OnClientConnected(int clientId) {
        Log($"[SERVER] Client connected to server with ID: {clientId}");

        //await Server.SendTcpMessageAsync(-clientId, "LoadingScreen", [true, 0]);

        //await Task.Delay(500); // Wait a moment to make sure other clients are in loadign screen, before starting object sync, to prevent desync issues.

        //await Server.SendTcpMessageAsync(-clientId, "LoadingScreen", [false, 100]);
    }

    public static async void OnClientDisconnected(int clientId, bool success) {
        //TODO returns false, when host client runs Disconnect, and shuts server down first. Should return true, when server is succesfully closed. Should we also return Disconnect reason instead of bool?
        Log($"Client disconnected with ID:{clientId}, SUCCESS:{success}");

    }

    public static async void OnServerShutdown()
    {
        Log($"[SERVER] Clearing mission state lists");
        ServerNetworkMethods.MissionAttributeManager.Clear();
        ServerNetworkMethods.ServerObjectManager.Clear();
        ServerNetworkMethods.UsernameList.Clear();
    }
    
}