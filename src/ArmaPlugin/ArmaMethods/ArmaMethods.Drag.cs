using System;
using System.Threading.Tasks;
using DynTypeNetwork;
using static EdenOnline.ArmaLog;

namespace EdenOnline;

/// <summary>
/// Object drag operations exposed to Arma 3 via callExtension.
/// Replicates object movement in 3DEN across connected clients.
/// START_MOVE/END_MOVE, the object drags are sent via TCP,
/// while the high-frequency position updates are sent via UDP.
/// </summary>
public static partial class ArmaMethods
{
    /// <summary>
    /// Notifies the server that an object drag has started.
    /// Sent via TCP (fire-and-forget), similar to CreateObject.
    /// </summary>
    public static async Task StartObjectDrag(string moveId, object[] objectIds)
    {
        if (!Client.IsTcpConnected())
            throw new Exception("Client is not connected. Cannot start object drag.");

        await Client.SendTcpMessageAsync(-1, "StartObjectDrag", moveId, objectIds);
    }

    /// <summary>
    /// Streams the ongoing object drag position delta to other clients via UDP,
    /// similar to CameraUpdate for high-frequency updates.
    /// </summary>
    public static async Task UpdateObjectDrag(string moveId, object[] delta)
    {
        if (!Client.IsUdpConnected())
            throw new Exception("Client is not connected. Cannot send object drag update.");

        await Client.SendUdpMessageAsync(-1, "UpdateObjectDrag", moveId, delta);
    }

    /// <summary>
    /// Notifies the server that an object drag has ended with authoritative final positions.
    /// Sent via TCP (fire-and-forget), similar to CreateObject.
    /// </summary>
    public static async Task EndObjectDrag(string moveId, object[] finalPositions)
    {
        if (!Client.IsTcpConnected())
            throw new Exception("Client is not connected. Cannot end object drag.");

        await Client.SendTcpMessageAsync(-1, "EndObjectDrag", moveId, finalPositions);
    }
}
