
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
        ClientStateManager.ObjectDragSessions.ObserveGeneration(createdObj.Id, createdObj.Timestamp);
        Extension.SendToArma("ObjectCreated", [createdObj.Id, createdObj.Attributes]);
    }
    public static void CreateObjectsBatch(List<ArmaObject> createdObjects) {
        foreach (ArmaObject createdObject in createdObjects) {
            ClientStateManager.ObjectDragSessions.ObserveGeneration(createdObject.Id, createdObject.Timestamp);
            Extension.SendToArma("ObjectCreated", [createdObject.Id, createdObject.Attributes]);
        }
    }
    public static void UpdateObject(ArmaObject updatedObj) {
        ClientStateManager.ObjectDragSessions.ObserveGeneration(updatedObj.Id, updatedObj.Timestamp);
        Extension.SendToArma("ObjectUpdated", [updatedObj.Id, updatedObj.Attributes]);
    }

    public static void StartObjectDrag(NetworkMessage request, ObjectDragStart start)
    {
        ObjectDragStartResult result = ClientStateManager.ObjectDragSessions.TryStart(request.SenderId, start);
        if (result is ObjectDragStartResult.Accepted or ObjectDragStartResult.Replaced)
        {
            Extension.SendToArma("ObjectDragStarted", [
                start.ObjectId,
                start.DragId,
                request.SenderId
            ]);
        }
    }

    public static void UpdateObjectDrag(NetworkMessage request, ObjectDragUpdate update)
    {
        if (!ClientStateManager.ObjectDragSessions.TryAdvance(request.SenderId, update)) return;

        Extension.SendToArma("ObjectDragUpdated", [
            update.ObjectId,
            update.DragId,
            update.Sequence,
            update.Position,
            update.Rotation
        ]);
    }

    public static void EndObjectDrag(NetworkMessage request, ObjectDragEnd end)
    {
        if (!ClientStateManager.ObjectDragSessions.TryEnd(request.SenderId, end)) return;

        Extension.SendToArma("ObjectDragEnded", [
            end.ObjectId,
            end.DragId,
            end.FinalSequence,
            end.Position,
            end.Rotation
        ]);
    }

    public static void RemoveObject(string objectID) {
        Extension.SendToArma("ObjectRemoved", [objectID]);
    }

    public static void CreateSyncConnection(ArmaSyncConnection connection) {
        Extension.SendToArma("CreateSyncConnection", [connection.FromID, connection.ToID, connection.Type]);
    }
    public static void CreateSyncConnectionsBatch(List<ArmaSyncConnection> connections) {
        foreach (ArmaSyncConnection connection in connections)
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
