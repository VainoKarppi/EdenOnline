
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


public class ClientNetworkEvents {
    public static async void OnConnected(int clientId) {
        
    }

    public static void OnDisconnected(bool success) {
        Log("[CLIENT] Client disconnected from server.");
    }

    public static async Task OnServerShutdown(ServerDisconnectReason reason) {
        Log($"[CLIENT] Server shutdown event received. Reason: {reason}");
        Extension.SendToArma("ServerShutdown", [reason.ToString()]);
    }

    public static void OnOtherClientConnected(int otherClientId) {
        Log($"[CLIENT] Other client connected: {otherClientId}");
    }
    public static void OnOtherClientDisconnected(int otherClientId, bool success) {
        Log($"[CLIENT] Other client disconnected: {otherClientId}");
        // Remove the user from local username list and notify Arma UI
        ArmaMethods.UsernameList.Remove(otherClientId);
        try {
            var otherUsersArray = ArmaMethods.UsernameList
                .Where(x => x.Key != Client.ClientID)
                .Select(kvp => new object[] { kvp.Key, kvp.Value })
                .Cast<object>()
                .ToArray();

            Extension.SendToArma("UpdateClientList", [otherUsersArray]);
        } catch (Exception ex) {
            Log($"[CLIENT] Failed to send UpdateClientList to Arma after other client disconnected: {ex}");
        }
    }
}