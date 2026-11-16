
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using static EdenOnline.ArmaLog;
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


public class ClientNetworkEvents {
    public static async void OnConnected(int clientId) {
        
    }

    public static void OnDisconnected(bool success, DisconnectReason reason) {
        Log($"[CLIENT] Client disconnected from server. Success: {success}, Reason: {reason}");
    }

    public static async Task OnServerShutdown(DisconnectReason reason) {
        Log($"[CLIENT] Server shutdown event received. Reason: {reason}");
        Extension.SendToArma("ServerShutdown", [reason.ToString()]);
    }

    public static void OnOtherClientConnected(int otherClientId) {
        Log($"[CLIENT] Other client connected: {otherClientId}");
    }
    public static void OnOtherClientDisconnected(int otherClientId, bool success, DisconnectReason reason) {
        string username = ClientStateManager.UsernameList.TryGetValue(otherClientId, out string? value) ? value : "Unknown";

        Log($"[CLIENT] Other client disconnected: {otherClientId} ({username}). Success: {success}, Reason: {reason}");
        // Remove the user from local username list and notify Arma UI
        ClientStateManager.UsernameList.Remove(otherClientId);
        try {
            var otherUsersArray = ClientStateManager.UsernameList
                .Where(x => x.Key != Client.ClientID)
                .Select(kvp => new object[] { kvp.Key, kvp.Value })
                .Cast<object>()
                .ToArray();

            Extension.SendToArma("UpdateClientList", [otherUsersArray]);
        } catch (Exception ex) {
            Log($"[CLIENT] Failed to send UpdateClientList to Arma after other client disconnected: {ex}");
        }
    }




    // ============================================================
    // CONNECTION SYNCHRONIZATION
    // ============================================================

    /// <summary>
    /// Called when another client creates a synchronization connection.
    /// Forwards the connection to the local Arma 3 client.
    /// </summary>
    public static void CreateSyncConnection(ArmaSyncConnection connection) {
        try {
            Log($"[CLIENT] Received CreateSyncConnection: " + $"{connection.FromID} -> {connection.ToID} ({connection.Type})");
            Extension.SendToArma("CreateSyncConnection", [connection.FromID, connection.ToID, connection.Type]);
        } catch (Exception ex){
            Log($"[CLIENT] Failed to process CreateSyncConnection: {ex}");
        }
    }

    /// <summary>
    /// Called when another client removes a synchronization connection.
    /// Forwards the removal to the local Arma 3 client.
    /// </summary>
    public static void RemoveSyncConnection(ArmaSyncConnection connection){
        try{
            Log($"[CLIENT] Received RemoveSyncConnection: " +$"{connection.FromID} -> {connection.ToID} ({connection.Type})");
            Extension.SendToArma("RemoveSyncConnection", [connection.FromID, connection.ToID, connection.Type]);
        } catch (Exception ex) {
            Log($"[CLIENT] Failed to process RemoveSyncConnection: {ex}");
        }
    }
}