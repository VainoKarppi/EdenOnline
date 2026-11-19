using System;
using System.Threading.Tasks;
using DynTypeNetwork;

namespace EdenOnline;

/// <summary>
/// Entity attribute editing locks exposed to Arma 3 via callExtension.
/// While a client has the Eden attributes window open for an entity, that entity is locked for the
/// other clients: they cannot open its attributes or drag it until the lock is released.
/// </summary>
public static partial class ArmaMethods
{
    /// <summary>
    /// Notifies the other clients that this client started editing the attributes of the entities.
    /// Sent via TCP (fire-and-forget), similar to StartObjectDrag.
    /// </summary>
    public static async Task StartAttributesEdit(object[] objectIds, string username, int clientId)
    {
        if (!Client.IsTcpConnected())
            throw new Exception("Client is not connected. Cannot start attributes edit.");

        await Client.SendTcpMessageAsync(-1, "StartAttributesEdit", objectIds, username, clientId);
    }

    /// <summary>
    /// Releases the attribute editing locks of the entities.
    /// Sent via TCP (fire-and-forget), similar to EndObjectDrag.
    /// </summary>
    public static async Task StopAttributesEdit(object[] objectIds)
    {
        if (!Client.IsTcpConnected())
            throw new Exception("Client is not connected. Cannot stop attributes edit.");

        // One parameter, one wire argument: objectIds must be wrapped, otherwise every id is sent as
        // a separate argument and the receiver's DynamicInvoke fails with a parameter count mismatch
        // (the fire-and-forget task swallows it, so the release would silently never arrive).
        await Client.SendTcpMessageAsync(-1, "StopAttributesEdit", [objectIds]);
    }
}
