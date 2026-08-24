using System;
using System.Collections.Generic;
using System.Linq;
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
        ClientStateManager.ObjectDragSessions.ObserveGeneration(obj.Id, obj.Timestamp);
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
        foreach (ArmaObject obj in objects)
            ClientStateManager.ObjectDragSessions.ObserveGeneration(obj.Id, obj.Timestamp);
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
        ClientStateManager.ObjectDragSessions.ObserveGeneration(obj.Id, obj.Timestamp);
        await Client.SendTcpMessageAsync(0, "UpdateObject", obj);
    }

    /// <summary>
    /// Starts a peer-to-peer drag session and returns the client-generated Drag ID.
    /// </summary>
    public static async Task<string> StartObjectDrag(string objectID)
    {
        if (!Client.IsTcpConnected())
            throw new Exception("Client is not connected. Cannot start object dragging.");
        if (string.IsNullOrWhiteSpace(objectID))
            throw new ArgumentException("Object ID cannot be empty.", nameof(objectID));

        const int acquisitionAttempts = 3;
        for (int attempt = 0; attempt < acquisitionAttempts; attempt++)
        {
            int[] expectedPeerIds = ClientStateManager.UsernameList.Keys
                .Where(clientId => clientId > Server.SERVER_ID && clientId != Client.ClientID)
                .Distinct()
                .ToArray();
            int clientRank = expectedPeerIds
                .Append(Client.ClientID)
                .Order()
                .ToList()
                .IndexOf(Client.ClientID);
            string dragID = Guid.CreateVersion7().ToString("N");
            var start = new ObjectDragStart(objectID, dragID)
            {
                Generation = ClientStateManager.ObjectDragSessions.GetGeneration(objectID)
            };
            ObjectDragStartResult result = ClientStateManager.ObjectDragSessions.TryBeginAcquisition(
                Client.ClientID,
                start,
                expectedPeerIds);
            if (result is not ObjectDragStartResult.Accepted) return "";

            bool acquired = false;
            try
            {
                await Client.SendTcpMessageAsync(-1, nameof(ClientNetworkMethods.StartObjectDrag), start);
                acquired = await ClientStateManager.ObjectDragSessions.WaitForAcquisitionAsync(
                    objectID,
                    dragID,
                    TimeSpan.FromMilliseconds(750));
                if (acquired) return dragID;

                await Client.SendTcpMessageAsync(-1, nameof(ClientNetworkMethods.CancelObjectDrag), start);
            }
            finally
            {
                if (!acquired)
                    ClientStateManager.ObjectDragSessions.TryCancel(Client.ClientID, objectID, dragID);
            }

            if (attempt + 1 < acquisitionAttempts)
            {
                int backoffMilliseconds = Math.Min(350, 75 * (clientRank + 1) * (attempt + 1));
                await Task.Delay(backoffMilliseconds);
            }
        }

        return "";
    }

    /// <summary>
    /// Sends an unordered drag sample to all other clients. Sequence checks on
    /// both the extension and SQF side discard duplicates and stale packets.
    /// </summary>
    public static async Task<bool> UpdateObjectDrag(
        string objectID,
        string dragID,
        double sequence,
        object[] position,
        object[] rotation)
    {
        if (!Client.IsUdpConnected())
            throw new Exception("Client is not connected. Cannot update object dragging.");

        long sequenceNumber = ParseDragSequence(sequence, allowZero: false);
        var update = new ObjectDragUpdate(objectID, dragID, sequenceNumber, position, rotation);
        if (!ClientStateManager.ObjectDragSessions.TryAdvance(Client.ClientID, update)) return false;

        await Client.SendUdpMessageAsync(-1, nameof(ClientNetworkMethods.UpdateObjectDrag), update);
        return true;
    }

    /// <summary>
    /// Ends a drag through reliable TCP and stores the same final transform in
    /// the server snapshot used by clients that join later.
    /// </summary>
    public static async Task<bool> EndObjectDrag(
        string objectID,
        string dragID,
        double finalSequence,
        object[] position,
        object[] rotation)
    {
        if (!Client.IsTcpConnected())
            throw new Exception("Client is not connected. Cannot end object dragging.");

        long sequenceNumber = ParseDragSequence(finalSequence, allowZero: true);
        if (!ClientStateManager.ObjectDragSessions.TryGetActive(objectID, out ObjectDragSession? active)
            || active!.OwnerClientId != Client.ClientID
            || active.DragId != dragID)
            return false;

        long nextGeneration = Math.Max(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            checked(active.Generation + 1));
        var end = new ObjectDragEnd(
            objectID,
            dragID,
            sequenceNumber,
            position,
            rotation,
            active.Generation,
            nextGeneration);
        if (!ClientStateManager.ObjectDragSessions.TryPrepareEnd(Client.ClientID, end)) return false;

        var finalObject = new ArmaObject(objectID, new Dictionary<string, object?>
        {
            ["Position"] = position,
            ["Rotation"] = rotation
        }) { Timestamp = nextGeneration };
        bool persisted = await Client.RequestTcpDataAsync<bool>(
            Server.SERVER_ID,
            nameof(ServerNetworkMethods.UpdateObjectConfirmed),
            finalObject);
        if (!persisted)
            throw new InvalidOperationException("The final drag state could not be stored on the server.");

        await Client.SendTcpMessageAsync(-1, nameof(ClientNetworkMethods.EndObjectDrag), end);
        return ClientStateManager.ObjectDragSessions.TryEnd(Client.ClientID, end);
    }

    private static long ParseDragSequence(double sequence, bool allowZero)
    {
        double minimum = allowZero ? 0 : 1;
        if (!double.IsFinite(sequence) || sequence < minimum || sequence > long.MaxValue || sequence != Math.Truncate(sequence))
            throw new ArgumentOutOfRangeException(nameof(sequence), "Drag sequence must be a non-negative integer in range.");

        return (long)sequence;
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
