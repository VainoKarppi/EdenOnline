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
}
