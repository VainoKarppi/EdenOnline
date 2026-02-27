
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using static ArmaExtension.Logger;
using ArmaExtension;

using EdenOnline.Models;
using EdenOnline.Network;

namespace EdenOnline;


[ArmaExtensionPlugin]
public static class ArmaMethods {
    // CALLED FROM ARMA USING:
    // _data = "ArmaExtension" callExtension "Version";
    // _data == "[""SUCCESS"",[""1.0.0.0""]]"
    public static string Version() {
        return Extension.Version;
    }

    public static bool Connect(string ip, int port, string username, string worldname, string armaVersion, object[] mods, string password) {
        string clientHash = GetHash(new object[] {worldname, mods, Extension.Version, armaVersion});
        Log($"Connect Method Called: {ip}:{port}, world: {worldname}, username: {username},  mods: {string.Join(",", mods)}, clientHash: {clientHash}, password: {password}");

        Client.Connect(ip, port, username, clientHash);
        return true;
    }

    public static bool StartServer(string username, double port, string worldname, string armaVersion, object[] mods, string? password = null) {
        string clientHash = GetHash(new object[] {worldname, mods, Extension.Version, armaVersion});

        Server.Start(clientHash, password);
        Client.Connect("127.0.0.1", (int)port, username, clientHash);

        //TODO Wait until Client is connected before returning success and return client ID instead
        return true;
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

    public static async Task AsyncTest(string input = "test") {
        Log($"AsyncTest Method Called with input: {input}");
        await Task.Delay(2000); // Simulate some async work
        Log("AsyncTest Method Completed");
    }

    public static async Task<bool> AsyncReturnTest(string input = "test") {
        Log($"AsyncReturnTest Method Called with input: {input}");
        await Task.Delay(2000); // Simulate some async work
        Log("AsyncReturnTest Method Completed");

        return true;
    }

    public static bool CreateObject(string objectID, string classname, object[] position, object[] rotation, double scale = 1) {
        Log($"CreateObject Method Called: {objectID}, {classname}, position: [{string.Join(",", position)}], rotation: [{string.Join(",", rotation)}]");
        //if (!Client.IsConnected) throw new Exception("Client is not connected. Cannot create object.");
        
        ServerObject obj = new(
            objectID,
            classname,
            position,
            rotation
        );
        ServerObjectManager.AddOrUpdateObject(obj);

        Console.WriteLine(NetworkSerializer.SerializeToBytes(obj));

        object[] messageData = [obj];
        
        //Client.SendMessage(MessageType.ObjectCreate, messageData);
        
        return true;
    }
    public static string GetHash(object item) {
        return item.GetHashCode().ToString();
    }



    public static void TestNetwork()
    {

        // ✅ Create a test ServerObject
        ServerObject obj = new(
            "testObject",
            "Land_CncBarrier_striped_F",
            [0, 0, 0],
            [0, 0, 0]
        );

        // ✅ Serialize to bytes
        byte[] bytes = NetworkSerializer.SerializeToBytes(obj);
        Console.WriteLine("Serialized bytes length: " + bytes.Length);
        Console.WriteLine("Serialized bytes (UTF8 string): " + System.Text.Encoding.UTF8.GetString(bytes));

        // ✅ Serialize into a network message wrapper
        int responseId = 1;
        MessageType responseMethod = MessageType.ObjectUpdate;
        int senderId = 42;

        byte[] networkMessage = NetworkSerializer.PackMessage(responseId, responseMethod, senderId, obj);
        Console.WriteLine("Packed network message length: " + networkMessage.Length);
        Console.WriteLine("Packed network message (UTF8 string, skipping length prefix): " + System.Text.Encoding.UTF8.GetString(networkMessage[4..]));

        // ✅ Deserialize back from network message
        var (respId, method, sender, type, data) = NetworkSerializer.UnpackMessage(networkMessage);
        Console.WriteLine($"Unpacked message: responseId={respId}, method={method}, senderId={sender}, type={type}");

        // ✅ Reconstruct the actual ServerObject from dynamic data
        ServerObject? restoredObj = NetworkSerializer.Reconstruct<ServerObject>(data);
        Console.WriteLine($"Restored ServerObject: Id={restoredObj?.Id}, Classname={restoredObj?.Classname}, " +
            $"Position=[{string.Join(",", restoredObj?.Position ?? [])}], " +
            $"Rotation=[{string.Join(",", restoredObj?.Rotation ?? [])}], Timestamp={restoredObj?.Timestamp}");



        // Test with void data
        byte[] voidMessage = NetworkSerializer.PackMessage(responseId, MessageType.ClientDisconnect, senderId, null);
        Console.WriteLine("Packed network message (UTF8 string, skipping length prefix): " + System.Text.Encoding.UTF8.GetString(voidMessage[4..]));

        var (voidRespId, voidMethod, voidSender, voidTypeName, voidData) = NetworkSerializer.UnpackMessage(voidMessage);
        Console.WriteLine($"Void message unpacked: responseId={voidRespId}, method={voidMethod}, senderId={voidSender}, type={voidTypeName}, data={(voidData == null ? "null" : voidData.ToString())}");

        // Test with complex nested data


        List<ServerObject> objects = [
            new("obj1", "Land_CncBarrier_striped_F", [0, 0, 0], [0, 0, 0]),
            new("obj2", "Land_Tank_01", [10, 0, 0], [0, 0, 0]),
            new("obj3", "Land_CncBarrier_striped_F", [20, 0, 0], [0, 0, 0]),
        ];
        byte[] listMessage = NetworkSerializer.PackMessage(responseId, MessageType.ObjectUpdate, senderId, objects);
        Console.WriteLine("Packed network message (UTF8 string, skipping length prefix): " + System.Text.Encoding.UTF8.GetString(listMessage[4..]));

        var (listRespId, listMethod, listSender, listTypeName, listData) = NetworkSerializer.UnpackMessage(listMessage);
        Console.WriteLine($"List message unpacked: responseId={listRespId}, method={listMethod}, senderId={listSender}, type={listTypeName}, data count={((List<ServerObject>?)listData)?.Count}");


        List<ServerObject>? objectsRestored = NetworkSerializer.Reconstruct<List<ServerObject>>(listData);
        Console.WriteLine($"Restored List<ServerObject>: count={objectsRestored?.Count}");
        foreach (ServerObject item in objectsRestored ?? [])
        {
            Console.WriteLine($"Restored ServerObject: Id={item.Id}, Classname={item.Classname}");
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

        ServerObjectManager.AddOrUpdateObject(new ServerObject(
            "obj1",
            "Land_CncBarrier_striped_F",
            [0, 0, 0],
            [0, 0, 0]
        ));

        ServerObjectManager.AddOrUpdateObject(new ServerObject(
            "obj2",
            "Land_Tank_01",
            [10, 0, 0],
            [0, 0, 0]
        ));

        ServerObjectManager.AddOrUpdateObject(new ServerObject(
            "obj3",
            "Land_CncBarrier_striped_F",
            [20, 0, 0],
            [0, 0, 0]
        ));

        ServerObjectManager.AddOrUpdateObject(new ServerObject(
            "obj4",
            "Land_CncBarrier_striped_F",
            [30, 0, 0],
            [0, 0, 0]
        ));

        Log("EdenOnline Extension Initialized");
    }
}