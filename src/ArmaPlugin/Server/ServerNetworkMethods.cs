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



public class ServerNetworkMethods {

    public static void RegisterUserName(int clientID, string username)
    {
        ServerStateManager.UsernameList[clientID] = username;

        // Broadcast this username registration to all other connected clients
        try {
            _ = Server.SendTcpMessageAsync(-clientID, "RegisterUserName", clientID, username);
        } catch (Exception ex) {
            Log($"[SERVER] Failed to broadcast RegisterUserName: {ex}");
        }
    }

    public static void RemoveUserName(int clientID)
    {
        ServerStateManager.UsernameList.Remove(clientID);
    }

    public static Dictionary<int, string> GetAllUsernames()
    {
        return ServerStateManager.UsernameList;
    }
    public static void CreateObject(ArmaObject armaObject)
    {
        // Only update local server database
        ServerStateManager.ServerObjectManager.AddOrUpdateObject(armaObject);
    }

    public static void UpdateObject(ArmaObject armaObject)
    {
        // Only update local server database
        ServerStateManager.ServerObjectManager.AddOrUpdateObject(armaObject);
    }
    public static bool RemoveObject(string objectID)
    {
        // Only update local server database
        bool success = ServerStateManager.ServerObjectManager.RemoveObject(objectID);
        return success;
    }

    public static string GetServerTime() {
        Log($"[SERVER] Received GetServerTime request. Returning {DateTime.UtcNow.ToString("o")}");
        return DateTime.UtcNow.ToString("o");
    }

    public static int GetObjectCount() {
        return ServerStateManager.ServerObjectManager.Objects.Count;
    }

    public static List<ArmaObject> GetAllObjects() {
        return ServerStateManager.ServerObjectManager.GetAllObjects();
    }

    public static void SetMissionAttribute(MissionAttribute missionAttribute) {
        Log($"[SERVER] Setting Attribute: [{missionAttribute.Section}, {missionAttribute.Property}, {missionAttribute.Value}]");
        ServerStateManager.MissionAttributeManager.SetAttribute([missionAttribute.Section!, missionAttribute.Property!], missionAttribute.Value!);
    }

    public static void SetInitialMissionAttributes(MissionAttribute[] attributes)
    {
        Log($"[SERVER] Received SetInitialMissionAttributes ({attributes.Count()})");
        foreach (MissionAttribute attribute in attributes)
        {
            ServerStateManager.MissionAttributeManager.SetAttribute([attribute.Section!, attribute.Property!],attribute.Value);
        }
    }

    public static Dictionary<string[], object?> GetMissionAttributes() {
        try {
            var attributes = ServerStateManager.MissionAttributeManager.GetAllAttributes();

            return attributes;
        } catch (Exception ex) {
            Log($"[SERVER] Error while getting mission attributes: {ex}");
            return [];
        }
    }



    // ============================================================
    // CONNECTIONS
    // ============================================================

    /// <summary>
    /// Creates and stores a synchronization connection.
    /// </summary>
    public static void CreateSyncConnection(ArmaSyncConnection connection)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));

        if (string.IsNullOrWhiteSpace(connection.FromID)) throw new ArgumentException("Connection source ID cannot be empty.", nameof(connection));
        if (string.IsNullOrWhiteSpace(connection.ToID))throw new ArgumentException("Connection target ID cannot be empty.", nameof(connection));
        if (string.IsNullOrWhiteSpace(connection.Type)) throw new ArgumentException("Connection type cannot be empty.", nameof(connection));

        Log($"[SERVER] Received CreateSyncConnection: " + $"{connection.FromID} -> {connection.ToID} ({connection.Type})");

        // Prevent duplicate connections.
        bool exists = ServerStateManager.SyncConnections.Any(x =>
            string.Equals(x.FromID, connection.FromID, StringComparison.Ordinal) &&
            string.Equals(x.ToID, connection.ToID, StringComparison.Ordinal) &&
            string.Equals(x.Type, connection.Type, StringComparison.Ordinal)
        );

        if (exists) {
            Log($"[SERVER] Connection already exists: " + $"{connection.FromID} -> {connection.ToID} ({connection.Type})");
            return;
        }

        ServerStateManager.SyncConnections.Add(connection);

        Log($"[SERVER] Connection created: " + $"{connection.FromID} -> {connection.ToID} ({connection.Type})");
    }

    /// <summary>
    /// Removes an existing synchronization connection.
    /// </summary>
    public static bool RemoveSyncConnection(ArmaSyncConnection connection)
    {
        Log($"[SERVER] Received RemoveSyncConnection: " + $"{connection.FromID} -> {connection.ToID} ({connection.Type})");

        if (connection == null) throw new ArgumentNullException(nameof(connection));

        if (string.IsNullOrWhiteSpace(connection.FromID)) throw new ArgumentException("Connection source ID cannot be empty.", nameof(connection));
        if (string.IsNullOrWhiteSpace(connection.ToID)) throw new ArgumentException( "Connection target ID cannot be empty.", nameof(connection));
        if (string.IsNullOrWhiteSpace(connection.Type)) throw new ArgumentException( "Connection type cannot be empty.", nameof(connection));

        int removed = ServerStateManager.SyncConnections.RemoveAll(x => x.FromID == connection.FromID && x.ToID == connection.ToID && x.Type == connection.Type);

        if (removed == 0) {
            Log($"[SERVER] Connection not found: " + $"{connection.FromID} -> {connection.ToID} ({connection.Type})");
            return false;
        }

        Log($"[SERVER] Connection removed: " + $"{connection.FromID} -> {connection.ToID} ({connection.Type})");

        return true;
    }

    /// <summary>
    /// Gets all currently synchronized connections.
    /// </summary>
    public static List<ArmaSyncConnection> GetAllConnections() {
        return [.. ServerStateManager.SyncConnections];
    }
}
