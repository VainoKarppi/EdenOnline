using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DynTypeNetwork;

namespace EdenOnline;

/// <summary>
/// Mission attribute operations exposed to Arma 3 via callExtension.
/// Handles setting and syncing mission properties with the server.
/// </summary>
public static partial class ArmaMethods
{
    /// <summary>
    /// Sets a single mission attribute and sends it to the server.
    /// </summary>
    public static async Task SetMissionAttribute(string section, string property, object value)
    {
        if (!Client.IsTcpConnected())
            throw new Exception("Client is not connected. Cannot send mission attributes.");

        MissionAttribute attribute = new(section, property, value);
        await Client.SendTcpMessageAsync(0, "SetMissionAttribute", attribute);
    }

    /// <summary>
    /// Sends multiple initial mission attributes to the server at once.
    /// </summary>
    public static async Task SetInitialMissionAttributes(object[] attributes)
    {
        if (!Client.IsTcpConnected())
            throw new Exception("Client is not connected. Cannot send mission attributes.");

        MissionAttribute[] attributeList = attributes
            .Cast<object[]>()
            .Select(attrb => new MissionAttribute(
                (string)attrb[0],
                (string)attrb[1],
                attrb[2]
            ))
            .ToArray();

        await Client.SendTcpMessageAsync(1, "SetInitialMissionAttributes", [attributeList]);
    }
}
