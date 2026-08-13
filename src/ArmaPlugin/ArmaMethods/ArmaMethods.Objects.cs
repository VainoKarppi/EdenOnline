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
    /// Sends the current camera position and direction to other clients via UDP.
    /// </summary>
    public static async Task CameraUpdate(object[] position, object[] direction)
    {
        try
        {
            if (!Client.IsUdpConnected()) throw new Exception("Client is not connected. Cannot send camera position.");

            ArmaCamera camera = new()
            {
                Id = Client.ClientID,
                Position = position,
                Direction = direction
            };

            await Client.SendUdpMessageAsync(-1, "UpdateCamera", camera);
        }
        catch (Exception ex)
        {
            Log(ex);
            throw;
        }
    }
}
