using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DynTypeNetwork;
using static EdenOnline.Logger;

namespace EdenOnline;

/// <summary>
/// Object CRUD operations exposed to Arma 3 via callExtension.
/// Creates, updates, and removes synchronized objects on the mission.
/// </summary>
public static partial class ArmaMethods
{
    /// <summary>
    /// Confirms that every initial host object and connection reached the server.
    /// Remote clients are admitted only after this succeeds.
    /// </summary>
    public static async Task<bool> CompleteInitialSync(int expectedObjects, int expectedConnections)
    {
        if (!Client.IsTcpConnected() || !Server.IsTcpServerRunning())
            throw new Exception("Only the connected host can complete initial synchronization.");

        return await Client.RequestTcpDataAsync<bool>(
            Server.SERVER_ID,
            "CompleteInitialSync",
            expectedObjects,
            expectedConnections
        );
    }

    /// <summary>
    /// Creates a new synchronized object and sends it to the server.
    /// </summary>
    public static async Task<string> CreateObject(string objectID, Dictionary<string, object?> metadata)
    {
        if (!Client.IsTcpConnected())
            throw new Exception("Client is not connected. Cannot create object.");


        ArmaObject obj = new(objectID, metadata);
        await Client.SendTcpMessageAsync(0, "CreateObject", obj);

        return obj.Id;
    }

    /// <summary>
    /// Creates an initial group of synchronized objects with one extension and
    /// one network call instead of one request/response cycle per object.
    /// </summary>
    public static async Task CreateObjectsBatch(object[] objectData)
    {
        if (!Client.IsTcpConnected())
            throw new Exception("Client is not connected. Cannot create objects.");

        List<ArmaObject> objects = ParseObjectBatch(objectData);
        await Client.SendTcpMessageAsync(0, "CreateObjectsBatch", objects);
    }

    internal static List<ArmaObject> ParseObjectBatch(object[] objectData)
    {
        var objects = new List<ArmaObject>(objectData.Length);

        foreach (object? item in objectData)
        {
            if (item is not object[] objectEntry || objectEntry.Length != 2)
                throw new ArgumentException("Every object batch entry must contain an ID and an attribute array.", nameof(objectData));

            string objectId = objectEntry[0]?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(objectId))
                throw new ArgumentException("Object batch entries must have a non-empty ID.", nameof(objectData));

            if (objectEntry[1] is not object[] attributeEntries)
                throw new ArgumentException($"Object '{objectId}' has an invalid attribute array.", nameof(objectData));

            var attributes = new Dictionary<string, object?>();
            foreach (object? attribute in attributeEntries)
            {
                if (attribute is not object[] pair || pair.Length != 2 || pair[0] is not string key)
                    throw new ArgumentException($"Object '{objectId}' contains an invalid attribute entry.", nameof(objectData));

                attributes[key] = pair[1];
            }

            objects.Add(new ArmaObject(objectId, attributes));
        }

        return objects;
    }

    /// <summary>
    /// Updates an existing synchronized object and sends the changes to the server.
    /// </summary>
    public static async Task UpdateObject(string objectID, Dictionary<string, object?> metadata)
    {
        if (!Client.IsTcpConnected())
            throw new Exception("Client is not connected. Cannot update object.");

        ArmaObject obj = new(objectID, metadata);
        await Client.SendTcpMessageAsync(0, "UpdateObject", obj);
    }

    /// <summary>
    /// Removes a synchronized object and notifies the server.
    /// </summary>
    public static async Task RemoveObject(string objectID)
    {
        if (!Client.IsTcpConnected()) throw new Exception("Client is not connected. Cannot remove object.");

        await Client.SendTcpMessageAsync(0, "RemoveObject", objectID);
    }

    /// <summary>
    /// Creates a synchronization connection between two Eden objects.
    /// </summary>
    public static async Task CreateSyncConnection(string fromID, string toID, string type)
    {
        if (!Client.IsTcpConnected()) throw new Exception("Client is not connected. Cannot create connection.");

        if (string.IsNullOrWhiteSpace(fromID)) throw new ArgumentException("Source object ID cannot be empty.", nameof(fromID));
        if (string.IsNullOrWhiteSpace(toID)) throw new ArgumentException("Target object ID cannot be empty.", nameof(toID));
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("Connection type cannot be empty.", nameof(type));

        Log($"[CLIENT] CreateSyncConnection. From: {fromID}, To: {toID}, Type: {type}");

        ArmaSyncConnection syncoConnection = new() {
            FromID = fromID,
            ToID = toID,
            Type = type
        };

        Log($"[CLIENT] Sending CreateSyncConnection: {fromID} -> {toID} ({type})");

        await Client.SendTcpMessageAsync(0, "CreateSyncConnection", syncoConnection);

        Log($"[CLIENT] CreateSyncConnection sent: {fromID} -> {toID} ({type})");
    }

    public static async Task CreateSyncConnectionsBatch(object[] connectionData)
    {
        if (!Client.IsTcpConnected())
            throw new Exception("Client is not connected. Cannot create connections.");

        List<ArmaSyncConnection> connections = ParseConnectionBatch(connectionData);
        await Client.SendTcpMessageAsync(0, "CreateSyncConnectionsBatch", connections);
    }

    internal static List<ArmaSyncConnection> ParseConnectionBatch(object[] connectionData)
    {
        var connections = new List<ArmaSyncConnection>(connectionData.Length);

        foreach (object? item in connectionData)
        {
            if (item is not object[] entry || entry.Length != 3)
                throw new ArgumentException("Every connection batch entry must contain source, target, and type.", nameof(connectionData));

            string fromId = entry[0]?.ToString() ?? string.Empty;
            string toId = entry[1]?.ToString() ?? string.Empty;
            string type = entry[2]?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(fromId) || string.IsNullOrWhiteSpace(toId) || string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Connection batch values cannot be empty.", nameof(connectionData));

            connections.Add(new ArmaSyncConnection(fromId, toId, type));
        }

        return connections;
    }

    /// <summary>
    /// Removes an existing synchronization connection.
    /// </summary>
    public static async Task RemoveSyncConnection(string fromID, string toID, string type)
    {
        if (!Client.IsTcpConnected()) throw new Exception("Client is not connected. Cannot remove connection.");

        if (string.IsNullOrWhiteSpace(fromID)) throw new ArgumentException("Source object ID cannot be empty.", nameof(fromID));
        if (string.IsNullOrWhiteSpace(toID)) throw new ArgumentException("Target object ID cannot be empty.", nameof(toID));
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("Connection type cannot be empty.", nameof(type));

        ArmaSyncConnection connection = new() {
            FromID = fromID,
            ToID = toID,
            Type = type
        };

        Log($"[CLIENT] Sending RemoveSyncConnection: {fromID} -> {toID} ({type})");

        await Client.SendTcpMessageAsync(0, "RemoveSyncConnection", connection);

        Log($"[CLIENT] RemoveSyncConnection sent: {fromID} -> {toID} ({type})");
    }
}
