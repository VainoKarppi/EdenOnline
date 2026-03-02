
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using static ArmaExtension.Logger;
using ArmaExtension;

using EdenOnline.Models;
using EdenOnline.Network;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace EdenOnline;


[ArmaExtensionPlugin]
public static class ArmaMethods {
    // CALLED FROM ARMA USING:
    // _data = "ArmaExtension" callExtension "Version";
    // _data == "[""SUCCESS"",[""1.0.0.0""]]"
    public static string Version() {
        return Extension.Version;
    }

    public static async Task<bool> Connect(string ip, int port, string username, string worldname, string armaVersion, object[] mods, string password) {
        string clientHash = GetHash(new object[] {mods, Extension.Version, armaVersion});
        Log($"Connect Method Called: {ip}:{port}, world: {worldname}, username: {username},  mods: {string.Join(",", mods)}, clientHash: {clientHash}, password: {password}");

        await Client.Connect(ip, port, username, worldname, clientHash);
        return true;
    }

    public static async Task<int> StartServer(string username, double port, string worldname, string armaVersion, object[] mods, string? password = null) {
        string clientHash = GetHash(new object[] {mods, Extension.Version, armaVersion});

        Server.Start(clientHash, worldname, password);
        int clientID = await Client.Connect("127.0.0.1", (int)port, username, worldname, clientHash);

        return clientID;
    }

    public static bool Disconnect() {
        Log("Disconnect Method Called");
        
        if (Server.IsRunning) {
            Server.Stop();
        } else {
            Client.Disconnect();
        }

        return true;
    }

    public static string CreateObject(string objectID, string classname, object[] position, object[] rotation) {
        Log($"CreateObject Method Called: {objectID}, {classname}, position: [{string.Join(",", position)}], rotation: [{string.Join(",", rotation)}]");
        //if (!Client.IsConnected) throw new Exception("Client is not connected. Cannot create object.");
        
        ArmaObject obj = new(
            "testObject3",
            "Land_CncBarrier_striped_F",
            [0, 5, 0],
            [0, 5, 0]
        );
        obj.Metadata = new() {
            ["health"] = "100",
            ["damage"] = 0,
            ["owner"] = "server",
            ["simulation"] = true,
            ["isLocked"] = "false",
            ["fuel"] = 1.0,
            ["ammo"] = 1.0,
        };
        
        ObjectManager.AddObject(obj);

        NetworkHelper.SendClientMessage(MessageType.ObjectCreate, obj);

        return obj.Id;
    }
    public static string GetHash(object item) {
        return item.GetHashCode().ToString();
    }



    public static void TestNetwork()
    {
        try {
            // ✅ Create a test ServerObject
            ArmaObject obj = new(
                "testObject",
                "Land_CncBarrier_striped_F",
                [0, 0, 0],
                [0, 0, 0],
                "parentid",
                "groupid"
            );
            obj.Metadata = new Dictionary<string, object?>
            {
                ["health"] = "100",
                ["damage"] = "0",
                ["owner"] = "server",
                ["simulation"] = "true",
                ["isLocked"] = "false",
                ["fuel"] = "1.0",
                ["ammo"] = "1.0",
            };

            byte[] full = NetworkSerializer.PackMessage(2, MessageType.ObjectSync, 1, obj);
            Console.WriteLine("Serialized bytes length: " + full.Length);
            Console.WriteLine("Serialized bytes (UTF8 string): " + System.Text.Encoding.UTF8.GetString(full));

            var (fullRespId, fullMethod, fullSender, fullTypeName, fullData) = NetworkSerializer.UnpackMessage(full);
            Console.WriteLine($"message unpacked: responseId={fullRespId}, method={fullMethod}, senderId={fullSender}, type={fullTypeName}, data={(fullData == null ? "null" : fullData.ToString())}");
            
            //listData = [{"id":"obj1","classname":"asd"}]
            Console.WriteLine($"DATA 555: {fullData}");
            ArmaObject? deserialized2 = NetworkSerializer.DeserializeData<ArmaObject>(fullData!);
            Console.WriteLine(deserialized2?.Metadata);
        } catch (Exception ex) {
            Console.WriteLine(ex);
        }
        





        try {
            List<ArmaObject> objects = [
                new("obj1", "Land_CncBarrier_striped_F", [0, 0, 0], [0, 0, 0]),
                new("obj2", "Land_Tank_01", [10, 0, 0], [0, 0, 0]),
                new("obj3", "Land_CncBarrier_striped_F", [20, 0, 0], [0, 0, 0]),
            ];
            byte[] listMessage = NetworkSerializer.PackMessage(2, MessageType.ObjectSync, 1, objects);
            Console.WriteLine("Network message: " + System.Text.Encoding.UTF8.GetString(listMessage[4..]));

            var (listRespId, listMethod, listSender, listTypeName, listData) = NetworkSerializer.UnpackMessage(listMessage);
            Console.WriteLine($"message unpacked: responseId={listRespId}, method={listMethod}, senderId={listSender}, type={listTypeName}, data={(listData == null ? "null" : listData.ToString())}");
            
            //listData = [{"id":"obj1","classname":"asd"}]
            Console.WriteLine($"DATA 2: {listData}");
            List<ArmaObject>? deserialized = NetworkSerializer.DeserializeData<List<ArmaObject>>(listData!);

            foreach (ArmaObject item in deserialized ?? [])
            {
                Console.WriteLine($"Restored ServerObject: Id={item.Id}, Classname={item.Classname}");
            }
        } catch (Exception ex) {
            Console.WriteLine(ex);
        }
    }




    // ! INITIALIZED WHEN FIRST EXTENSION CALL IS MADE
    // If just public static void is used in Main(), it will block the Arma 3 until this method is finished
    // If its using public static async Task, this will not block the Arma 3, but events might not have been registered yet.
    public static void Main()
    {
        Log("Called EdenOnline Main method");
        CurrentLogLevel = LogLevel.Debug;
        MethodSystem.RegisterMethods(typeof(ArmaMethods)); // Always register your methods
        
        
        // Subscribe to events
        // The Events class prefixes all event names with "On". Use the correct identifiers below.
        Events.OnVersionCalled += version => Debug($"VersionCalled event triggered with version: {version}");

        Events.OnMethodCalled += methodName => Debug($"MethodCalled event triggered with method: {methodName}");
        Events.OnMethodCalledResponse += (methodName, response, success) => Debug($"MethodCalledResponse event: {methodName} with response count: {response?.Length ?? 0}, success: {success}");

        Events.OnMethodCalledWithArgs += (methodName, args) => Debug($"MethodCalledWithArgs event: {methodName} with args count: {args?.Length ?? 0}");
        Events.OnMethodCalledWithArgsResponse += (methodName, response, success) => Debug($"MethodCalledWithArgsResponse event: {methodName} with response count: {response?.Length ?? 0}, success: {success}");

        Events.OnAsyncTaskStarted += (method, asyncKey, args) => Debug($"AsyncTaskStarted event triggered with method: {method}, asyncKey: {asyncKey}, args count: {args?.Length ?? 0}");
        Events.OnAsyncTaskCompleted += (method, asyncKey, response, success) => Debug($"AsyncTaskCompleted event triggered with method: {method}, asyncKey: {asyncKey}, success: {success}, response count: {response?.Length ?? 0}");
        Events.OnAsyncTaskCancelled += (asyncKey, success) => Debug($"AsyncTaskCancelled event triggered with asyncKey: {asyncKey}, success: {success}");

        Events.OnSendToArma += (method, data) => Debug($"OnSendToArma event triggered with method: {method}, data count: {data?.Length ?? 0}");
        
        Events.OnErrorOccurred += ex => Debug($"ErrorOccurred event triggered: {ex.Message}");

        ObjectManager.AddObject(new ArmaObject(
            "obj1",
            "Land_CncBarrier_striped_F",
            [0, 0, 0],
            [0, 0, 0]
        ));

        ObjectManager.AddObject(new ArmaObject(
            "obj2",
            "Land_Tank_01",
            [10, 0, 0],
            [0, 0, 0]
        ));

        ObjectManager.AddObject(new ArmaObject(
            "obj3",
            "Land_CncBarrier_striped_F",
            [20, 0, 0],
            [0, 0, 0]
        ));

        ObjectManager.AddObject(new ArmaObject(
            "obj4",
            "Land_CncBarrier_striped_F",
            [30, 0, 0],
            [0, 0, 0]
        ));

        Log("EdenOnline Extension Initialized");
    }
}