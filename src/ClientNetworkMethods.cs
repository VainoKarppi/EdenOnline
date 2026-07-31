
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

// These are methods that other clients or the server can invoke remotely, and request data for

public class ClientNetworkMethods {
    public static void UpdateUserName(int clientID, string username)
    {
        Log($"[CLIENT] Received username list update! ({clientID}) {username}");
        ArmaMethods.UsernameList[clientID] = username;
    }
    public static void UpdateCamera(ArmaCamera camera) {
        Log($"[CLIENT] Received camera update from client {camera.Id}: Position: {string.Join(",", camera.Position)}, Direction: {string.Join(",", camera.Direction)}");
        Extension.SendToArma("CameraUpdate", [camera.Id, camera.Position, camera.Direction]);
    }

    public static void CreateObject(ArmaObject createdObj) {
        Log($"[CLIENT] Received CreateObject for object {createdObj.Id} with attributes: {createdObj.Attributes?.Count}");
        Extension.SendToArma("ObjectCreated", [createdObj.Id, createdObj.Attributes]);
    }
    public static void UpdateObject(ArmaObject updatedObj) {
        Log($"[CLIENT] Received UpdateObject for object {updatedObj.Id} with attributes: {updatedObj.Attributes?.Count}");
        Extension.SendToArma("ObjectUpdated", [updatedObj.Id, updatedObj.Attributes]);
    }
    public static void RemoveObject(string objectID) {
        Log($"[CLIENT] Received RemoveObject for object {objectID}");
        Extension.SendToArma("ObjectRemoved", [objectID]);
    }

    public static void LoadingScreen(bool enable) {
        Log($"[CLIENT] Received LoadingScreen: {enable}");
        Extension.SendToArma("LoadingScreen", [enable, 50 / 100.0]);
    }

    public static void SetMissionAttribute(string section, string property, object? value) {
        Log($"[CLIENT] Received SetMissionAttribute for section: {section}, property: {property}, value: {value}");
        Extension.SendToArma("MissionAttributeUpdated", [section, property, value]);
    }
}
