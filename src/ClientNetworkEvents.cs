
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
}