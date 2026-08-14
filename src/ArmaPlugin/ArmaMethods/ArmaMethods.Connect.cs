using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DynTypeNetwork;
using static EdenOnline.Logger;


namespace EdenOnline;

/// <summary>
/// Handles the connection flow between Arma 3 and the EdenOnline server.
/// Manages username sync, mission attribute sync, and object sync.
/// </summary>
public static partial class ArmaMethods
{


    /// <summary>
    /// Connects this client to a remote server.
    /// </summary>
    public static async Task<int> Connect(string host, int port, string username, string worldname, string armaVersion, object[] modHashes, string password = "")
    {
        if (Client.IsTcpConnected() && !Settings.ALLOW_DUAL_CONNECTIONS) throw new Exception("Client is already connected. Please disconnect before connecting again.");

        string clientHash = HashUtils.GetHash(new object[] { Extension.Version });
        Log($"[CLIENT] Connect: {host}:{port}, world: {worldname}, user: {username}, hash: {clientHash}");

        RegisterClientAuthentication(worldname, armaVersion, modHashes, password);

        int clientID = await Client.ConnectAsync(host, port, startUdp: true, clientHash);

        try
        {
            SubscribeClientEvents();

            await ShowLoadingScreen(true, 10);
            await SyncUsernames(clientID, username);

            await ShowLoadingScreen(true, 20);
            await SyncMissionAttributes();

            await ShowLoadingScreen(true, 30);
            await SyncObjects();

            await ShowLoadingScreen(true, 70);
            await SyncConnections();
            

            Log($"[CLIENT] Connect finished: {host}:{port}, clientID: {clientID}");
            await ShowLoadingScreen(false, 100);

            return clientID;
        }
        catch (Exception ex)
        {
            Log($"[CLIENT] Exception during connect: {ex}");

            // TODO: Server should toggle loading screen off if sync client disconnects mid-sync.

            await ShowLoadingScreen(false, 100);
            await Client.DisconnectAsync();
            throw;
        }
    }

    private static void RegisterClientAuthentication(string worldname, string armaVersion, object[] modHashes, string password)
    {
        Authentication.SetClientAuthentication(() =>
        {
            Console.WriteLine("[CLIENT] Sending authentication data to server...");
            return Task.FromResult<object[]?>([worldname, armaVersion, modHashes, password]);
        });
    }
    


    private static string? _serverWorldName;
    private static string? _serverArmaVersion;
    private static object[]? _serverModHashes;
    private static string? _serverPassword;
    /// <summary>
    /// Starts a local server and connects this client to it (host mode).
    /// </summary>
    public static async Task<int> StartServer(double port, string username, string worldname, string armaVersion, object[] modHashes, string password = "null")
    {
        if (Client.IsTcpConnected()) throw new Exception("Server is already running. Please disconnect the client before starting a server.");

        string clientHash = HashUtils.GetHash(new object[] { Extension.Version });

        MethodBuilder.RegisterServerMethods(new ServerNetworkMethods());
        ExtensionPlugin.PrintAvailableMethods("Server", MethodBuilder.GetAvailableServerMethods());

        RegisterServerAuthentication(worldname, armaVersion, modHashes, password);

        await Server.StartAsync((int)port, true, clientHash);

        Server.OnClientConnected += ServerNetworkEvents.OnClientConnected;
        Server.OnClientDisconnected += ServerNetworkEvents.OnClientDisconnected;
        Server.OnServerShutdown += ServerNetworkEvents.OnServerShutdown;

        int clientId = await Connect("127.0.0.1", (int)port, username, worldname, armaVersion, modHashes, password);
        return clientId;
    }

    private static void RegisterServerAuthentication(string worldname, string armaVersion, object[] modHashes, string password)
    {
        _serverWorldName = worldname;
        _serverArmaVersion = armaVersion;
        _serverModHashes = modHashes;
        _serverPassword = password;

        Authentication.SetServerValidator(ServerValidateAuthenticationAsync);
    }

    private static async Task<bool> ServerValidateAuthenticationAsync(object[]? parameters)
    {
        if (parameters is null || parameters.Length != 4)
            throw new Exception("Invalid authentication parameters.");

        string? worldName = parameters[0]?.ToString();
        string? armaVersion = parameters[1]?.ToString();
        object[]? modHashes = parameters[2] as object[];
        string? password = parameters[3]?.ToString();

        if (!string.Equals(worldName, _serverWorldName, StringComparison.Ordinal))
            throw new Exception("World name does not match.");

        if (!string.Equals(armaVersion, _serverArmaVersion, StringComparison.Ordinal))
            throw new Exception("Arma version does not match.");

        if (!string.Equals(password, _serverPassword, StringComparison.Ordinal))
            throw new Exception("Incorrect password.");

        if (!ModHashesEqual(modHashes, _serverModHashes))
            throw new Exception("Mod configuration does not match.");

        return true;
    }
    private static bool ModHashesEqual(object[]? clientHashes, object[]? serverHashes)
    {
        if (clientHashes is null || serverHashes is null)
            return clientHashes == serverHashes;

        return clientHashes
            .Select(x => x?.ToString())
            .ToHashSet()
            .SetEquals(serverHashes.Select(x => x?.ToString()));
    }

    // -- Connection helpers --

    private static void SubscribeClientEvents()
    {
        Client.OnClientConnected += ClientNetworkEvents.OnConnected;
        Client.OnClientDisconnected += ClientNetworkEvents.OnDisconnected;
        Client.OnServerShutdown += ClientNetworkEvents.OnServerShutdown;
        Client.OnOtherClientConnected += ClientNetworkEvents.OnOtherClientConnected;
        Client.OnOtherClientDisconnected += ClientNetworkEvents.OnOtherClientDisconnected;
    }

    private static async Task ShowLoadingScreen(bool enable, int progress)
    {
        await Client.SendTcpMessageAsync(-1, "LoadingScreen", [enable, progress]);
        await Task.Delay(50);
    }

    private static async Task SyncUsernames(int clientID, string username)
    {
        Log("[CLIENT] Syncing client list and usernames...");
        await Client.SendTcpMessageAsync(1, "RegisterUserName", clientID, username);

        ClientStateManager.UsernameList = await Client.RequestTcpDataAsync<Dictionary<int, string>>(1, "GetAllUsernames") ?? [];

        if (ClientStateManager.UsernameList != null && ClientStateManager.UsernameList.Count > 0)
        {
            object[] otherUsersArray = BuildOtherUsersArray();
            Extension.SendToArma("UpdateClientList", [otherUsersArray]);
        }

        Log($"[CLIENT] Received {ClientStateManager.UsernameList?.Count - 1 ?? 0} other users.");
    }

    private static async Task SyncMissionAttributes()
    {
        Log("[CLIENT] Requesting mission attributes from server...");
        Dictionary<string[], object?>? missionAttributes = await Client.RequestTcpDataAsync<Dictionary<string[], object?>>(1, "GetMissionAttributes");

        if (missionAttributes == null) throw new Exception("Failed to sync mission attributes: Received null from server");

        Log($"[CLIENT] Received {missionAttributes.Count} mission attributes from server.");

        object?[] attributes = missionAttributes.Select(kvp => (object?)new object?[] {
                kvp.Key.Length > 0 ? kvp.Key[0] : "",
                kvp.Key.Length > 1 ? kvp.Key[1] : "",
                kvp.Value
            }).ToArray();

        Extension.SendToArma("SetInitialMissionAttributes", [attributes]);
    }

    private static async Task SyncObjects()
    {
        Log("[CLIENT] Requesting object count from server...");
        int objectCount = await Client.RequestTcpDataAsync<int>(1, "GetObjectCount");
        Extension.SendToArma("ObjectSyncCount", [objectCount]);

        if (objectCount > 0) {
            List<ArmaObject>? objects = await Client.RequestTcpDataAsync<List<ArmaObject>>(1, "GetAllObjects");

            if (objects == null || objects.Count == 0)
                throw new Exception("Failed to sync objects: Received null from server");

            foreach (var obj in objects)
                Extension.SendToArma("ObjectSyncData", [obj.Id, obj.Attributes]);
        }

        Log($"[CLIENT] Object sync complete. Total objects synced: {objectCount}");
    }

    private static async Task SyncConnections() {
        Log("[CLIENT] Requesting connections from server...");

        List<ArmaSyncConnection>? connections = await Client.RequestTcpDataAsync<List<ArmaSyncConnection>>(1, "GetAllConnections");

        if (connections == null) throw new Exception("Failed to sync connections: Received null from server");

        foreach (var connection in connections) {
            Extension.SendToArma("ConnectionSyncData", [connection.FromID, connection.ToID, connection.Type]);
        }

        Log($"[CLIENT] Connection sync complete. Total connections synced: {connections.Count}");
    }

    private static object[] BuildOtherUsersArray()
    {
        return ClientStateManager.UsernameList
            .Where(x => x.Key != Client.ClientID)
            .Select(kvp => new object[] { kvp.Key, kvp.Value })
            .Cast<object>()
            .ToArray();
    }

    /// <summary>
    /// Disconnects from the current session. If this client is hosting the server,
    /// the server is stopped. Otherwise, only the client connection is closed.
    /// </summary>
    public static async Task<bool> Disconnect()
    {
        if (Server.IsTcpServerRunning())
        {
            Log("[ArmaMethods] Disconnect requested - client is hosting server >> Stopping server");
            await Server.StopAsync();
        }
        else
        {
            Log("[ArmaMethods] Disconnect requested - client is connected to remote server >> Disconnecting");
            await Client.DisconnectAsync();
        }

        return true;
    }
}
