using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DynTypeNetwork;
using static EdenOnline.ArmaLog;

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
    public static async Task<string> CreateObject(string objectID, string type, Dictionary<string, object?> metadata)
    {
        if (!Client.IsTcpConnected())
            throw new Exception("Client is not connected. Cannot create object.");


        ArmaObject obj = new(objectID, metadata) {
            Type = type // Set the type of the object ("Object", "Trigger", "Waypoint", "Logic", "Marker", "Comment" or "Layer")
        };
        await Client.SendTcpMessageAsync(0, "CreateObject", obj);

        Log($"[CLIENT] CreateObject. Type: {type}, ID: {objectID}, Properties: {string.Join(", ", metadata.Keys)}");

        return obj.Id;
    }

    /// <summary>
    /// Updates an existing synchronized object and sends the changes to the server.
    /// </summary>
    public static async Task UpdateObject(string objectID, string type, Dictionary<string, object?> metadata)
    {
        if (!Client.IsTcpConnected())
            throw new Exception("Client is not connected. Cannot update object.");

        ArmaObject obj = new(objectID, metadata) {
            Type = type
        };

        Log($"[CLIENT] UpdateObject. Type: {type}, ID: {objectID}, Properties: {string.Join(", ", metadata.Keys)}");

        await Client.SendTcpMessageAsync(0, "UpdateObject", obj);
    }

    /// <summary>
    /// Removes a batch of synchronized objects and notifies the server with a single message.
    /// Arma sends [[id, type], ...], so a single delete is a one element list and a multi-select
    /// delete, a composition removal or an undo costs one packet instead of one per entity.
    /// </summary>
    public static async Task RemoveObjects(object?[][] objects)
    {
        if (!Client.IsTcpConnected()) throw new Exception("Client is not connected. Cannot remove objects.");

        List<ArmaObject> removed = [];

        foreach (object?[] pair in objects) {
            if (pair.Length < 2) continue;

            string? id = pair[0]?.ToString();
            if (string.IsNullOrWhiteSpace(id)) continue;

            // Type is "Object", "Trigger", "Waypoint", "Logic", "Marker" or "Comment"
            removed.Add(new ArmaObject(id) { Type = pair[1]?.ToString() ?? "Object" });
        }
        if (removed.Count == 0) return;

        Log($"[CLIENT] RemoveObjects: [{string.Join(", ", removed.Select(x => $"[{x.Type} ({x.Id})]"))}]");

        // Remote methods map one wire argument to one parameter, so the batch has to be sent as a
        // single ArmaObject[] argument instead of being spread over one argument per object.
        await Client.SendTcpMessageAsync(0, "RemoveObjects", [removed.ToArray()]);
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
