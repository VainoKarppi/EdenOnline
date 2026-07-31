
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using static ArmaExtension.Logger;
using ArmaExtension;

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


// These are methods that other clients can invoke remotely, and request data for

public class ServerNetworkMethods {

    // TODO Move these elsewhere -> Or create new ones, everytime server is started again. Remove existing data, if server is restarted.
    public static Dictionary<int, string> UsernameList { get; set; } = [];
    public static ObjectManager ServerObjectManager { get; } = new ObjectManager();
    public static MissionAttributeManager MissionAttributeManager { get; } = new MissionAttributeManager();

    public static void UpdateUserName(int clientID, string username)
    {
        Log($"[SERVER] Adding user {clientID} {username} to UsernameList");
        UsernameList[clientID] = username;
    }

    public static Dictionary<int, string> GetAllUsernames()
    {
        return UsernameList;
    }
    public static void CreateObject(ArmaObject armaObject)
    {
        Log($"[SERVER] Received CreateObject for object {armaObject.Id} with attributes: {armaObject.Attributes?.Count}");
        // Only update local server database
        ServerObjectManager.AddOrUpdateObject(armaObject);
    }

    public static void UpdateObject(ArmaObject armaObject)
    {
        Log($"[SERVER] Received UpdateObject for object {armaObject.Id} with attributes: {armaObject.Attributes?.Count}");
        // Only update local server database
        ServerObjectManager.AddOrUpdateObject(armaObject);
    }
    public static bool RemoveObject(string objectID)
    {
        Log($"[SERVER] Received RemoveObject for object {objectID}");
        // Only update local server database
        bool success = ServerObjectManager.RemoveObject(objectID);
        return success;
    }

    public static string GetServerTime() {
        Log($"[SERVER] Received GetServerTime request. Returning {DateTime.UtcNow.ToString("o")}");
        return DateTime.UtcNow.ToString("o");
    }

    public static int GetObjectCount() {
        return ServerObjectManager.Objects.Count;
    }

    public static List<ArmaObject> GetAllObjects() {
        return ServerObjectManager.GetAllObjects();
    }

    public static void SetMissionAttribute(MissionAttribute missionAttribute) {
        Log($"[SERVER] Setting Attribute: [{missionAttribute.Section}, {missionAttribute.Property}, {missionAttribute.Value}]");
        MissionAttributeManager.SetAttribute([missionAttribute.Section!, missionAttribute.Property!], missionAttribute.Value!);
    }

    public static void SetInitialMissionAttributes(MissionAttribute[] attributes)
    {
        Log($"[SERVER] Received SetInitialMissionAttributes ({attributes.Count()})");
        foreach (MissionAttribute attribute in attributes)
        {
            MissionAttributeManager.SetAttribute([attribute.Section!, attribute.Property!],attribute.Value);
        }
    }

    public static Dictionary<string[], object?> GetMissionAttributes() {
        try {
            var attributes = MissionAttributeManager.GetAllAttributes();

            return attributes;
        } catch (Exception ex) {
            Log($"[SERVER] Error while getting mission attributes: {ex}");
            return [];
        }
    }
}
