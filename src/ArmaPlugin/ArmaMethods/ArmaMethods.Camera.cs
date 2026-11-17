using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DynTypeNetwork;
using static EdenOnline.ArmaLog;

namespace EdenOnline;

/// <summary>
/// Object CRUD operations exposed to Arma 3 via callExtension.
/// Creates, updates, and removes synchronized objects on the mission.
/// </summary>
public static partial class ArmaMethods {
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
