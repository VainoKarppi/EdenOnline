
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using static EdenOnline.Logger;
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

        //await Task.Delay(2000);

        //await Server.SendTcpMessageAsync(-clientId, "LoadingScreen", [false, 100]);
    }

    public static async void OnClientDisconnected(int clientId, bool success, DisconnectReason reason) {
        //TODO returns false, when host client runs Disconnect, and shuts server down first. Should return true, when server is succesfully closed. Should we also return Disconnect reason instead of bool?
        Log($"[SERVER] Client disconnected with ID:{clientId}, SUCCESS:{success}, REASON:{reason}");

        // Remove username from server list. Other clients are notified of this via network event OnOtherClientDisconnected, which is triggered by the server when a client disconnects.
        ServerNetworkMethods.RemoveUserName(clientId);
    }

    public static async void OnServerShutdown(DisconnectReason reason)
    {
        Log($"[SERVER] Server shutdown event received. Reason: {reason}");
        Log($"[SERVER] Clearing mission state lists");

        ServerStateManager.Reset();
    }
    
}
