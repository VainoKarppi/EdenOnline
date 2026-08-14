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

        ArmaSyncConnection connection = new() {
            FromID = fromID,
            ToID = toID,
            Type = type
        };

        Log($"[CLIENT] Sending CreateSyncConnection: {fromID} -> {toID} ({type})");

        await Client.SendTcpMessageAsync(0, "CreateSyncConnection", connection);

        Log($"[CLIENT] CreateSyncConnection sent: {fromID} -> {toID} ({type})");
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
