
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

// These are methods that other clients or the server can invoke remotely, and request data for

public class ClientNetworkMethods {
    public static void RegisterUserName(int clientID, string username)
    {
        Log($"[CLIENT] Received username list update! ({clientID}) {username}");
        ClientStateManager.UsernameList[clientID] = username;
        // Push updated client list to Arma UI (exclude ourselves)
        try {
            var otherUsersArray = ClientStateManager.UsernameList
                .Where(x => x.Key != Client.ClientID)
                .Select(kvp => new object[] { kvp.Key, kvp.Value })
                .Cast<object>()
                .ToArray();

            Extension.SendToArma("UpdateClientList", [otherUsersArray]);
        } catch (Exception ex) {
            Log($"[CLIENT] Failed to send UpdateClientList to Arma: {ex}");
        }
    }
    public static void UpdateCamera(ArmaCamera camera) {
        Extension.SendToArma("CameraUpdate", [camera.Id, camera.Position, camera.Direction]);
    }

    public static void CreateObject(ArmaObject createdObj) {
        Extension.SendToArma("ObjectCreated", [createdObj.Id, createdObj.Attributes]);
    }
    public static void UpdateObject(ArmaObject updatedObj) {
        Extension.SendToArma("ObjectUpdated", [updatedObj.Id, updatedObj.Attributes]);
    }
    public static void RemoveObject(string objectID) {
        Extension.SendToArma("ObjectRemoved", [objectID]);
    }

    public static void LoadingScreen(bool enable, int progress = 1) {
        progress = Math.Clamp(progress, 1, 100);
    
        Extension.SendToArma("LoadingScreen", [enable, progress / 100.0]);
    }

    public static void SetMissionAttribute(string section, string property, object? value) {
        Log($"[CLIENT] Received SetMissionAttribute for section: {section}, property: {property}, value: {value}");
        Extension.SendToArma("MissionAttributeUpdated", [section, property, value]);
    }
}
