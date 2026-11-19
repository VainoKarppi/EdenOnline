
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using static EdenOnline.ArmaLog;
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

        // A client that registers either just joined or reconnected (new ID). Any lock it held in a
        // previous session is stale, so everything stored under its id is released.
        try {
            Extension.SendToArma("LocksReleased", [clientID, username]);
        } catch (Exception ex) {
            Log($"[CLIENT] Failed to send LocksReleased to Arma after RegisterUserName: {ex}");
        }

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
        Extension.SendToArma("ObjectCreated", [createdObj.Id, createdObj.Type, createdObj.Attributes]);
    }
    public static void UpdateObject(ArmaObject updatedObj) {
        Extension.SendToArma("ObjectUpdated", [updatedObj.Id, updatedObj.Type, updatedObj.Attributes]);
    }
    public static void RemoveObjects(ArmaObject[] removedObjects) {
        // One message for the whole batch: Arma receives [[id, type], ...] as the single argument
        // of EOEX_fnc_receiveObjectDelete.
        object?[][] pairs = removedObjects
            .Select(removedObj => new object?[] { removedObj.Id, removedObj.Type })
            .ToArray();

        Extension.SendToArma("ObjectRemoved", [pairs]);
    }

    public static void StartObjectDrag(string moveId, object[] objectIds, string username) {
        Extension.SendToArma("StartObjectDrag", [moveId, objectIds, username]);
    }

    public static void UpdateObjectDrag(string moveId, object[] delta) {
        Extension.SendToArma("UpdateObjectDrag", [moveId, delta]);
    }

    public static void EndObjectDrag(string moveId, object[] finalPositions) {
        Extension.SendToArma("EndObjectDrag", [moveId, finalPositions]);
    }

    public static void StartAttributesEdit(object[] objectIds, string username, int clientId) {
        Log($"[CLIENT] StartAttributesEdit. {username} ({clientId}) is editing {string.Join(", ", objectIds)}");
        Extension.SendToArma("AttributesEditStarted", [objectIds, username, clientId]);
    }

    public static void StopAttributesEdit(object[] objectIds) {
        Log($"[CLIENT] StopAttributesEdit. {string.Join(", ", objectIds)}");
        Extension.SendToArma("AttributesEditStopped", [objectIds]);
    }

    public static void CreateSyncConnection(ArmaSyncConnection connection) {
        Extension.SendToArma("CreateSyncConnection", [connection.FromID, connection.ToID, connection.Type]);
    }

    public static void RemoveSyncConnection(ArmaSyncConnection connection) {
        Extension.SendToArma("RemoveSyncConnection", [connection.FromID, connection.ToID, connection.Type]);
    }

    public static void LoadingScreen(bool enable, int progress = 1) {
        progress = Math.Clamp(progress, 1, 100);
    
        Extension.SendToArma("LoadingScreen", [enable, progress / 100.0]);
    }

    public static void SetMissionAttribute(MissionAttribute missionAttribute) {
        Log($"[CLIENT] Received SetMissionAttribute: [{missionAttribute.Section}, {missionAttribute.Property}, {missionAttribute.Value}]");
        Extension.SendToArma("SetMissionAttribute", [missionAttribute.Section!, missionAttribute.Property!, missionAttribute.Value!]);
    }
}
