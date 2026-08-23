using System.Reflection;
using System.Text;
using DynTypeSerializer;
using DynTypeNetwork;
using EdenOnline;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

string hash1 = HashUtils.GetHash(new object[] { "eden", 3, true });
string hash2 = HashUtils.GetHash(new object[] { "eden", 3, true });

Assert(hash1 == hash2, "HashUtils must be deterministic for the same input.");
Assert(hash1.Length == 64, "HashUtils should produce a SHA-256 hex string.");

var camera = new ArmaCamera
{
    Id = 42,
    Position = [1.0, 2.0, 3.0],
    Direction = [0.0, 1.0, 0.0]
};

string json = Serializer.Serialize(camera);
ArmaCamera? roundTrip = Serializer.Deserialize<ArmaCamera>(json);

if (roundTrip is null) throw new InvalidOperationException("ArmaCamera should deserialize.");

Assert(roundTrip.Id == camera.Id, "ArmaCamera.Id should round-trip.");
Assert(roundTrip.Position.Length == 3, "ArmaCamera.Position should round-trip.");
Assert(roundTrip.Direction.Length == 3, "ArmaCamera.Direction should round-trip.");

var setMissionAttributeMethod = typeof(ArmaMethods).GetMethod("SetMissionAttribute", BindingFlags.Public | BindingFlags.Static);
Assert(setMissionAttributeMethod is not null, "SetMissionAttribute should exist.");

var serializerType = typeof(ArmaMethods).Assembly.GetType("EdenOnline.Serializer");
Assert(serializerType is not null, "Serializer type should exist.");

var deserializeMethod = serializerType?.GetMethod("DeserializeArmaArray", BindingFlags.NonPublic | BindingFlags.Static);
Assert(deserializeMethod is not null, "DeserializeArmaArray should exist.");

var values = (object?[])deserializeMethod!.Invoke(null, [setMissionAttributeMethod!, new string[] { "\"Briefing\"", "\"Scenario\"", "[1,2,3]" }, null])!;
Assert(values[2] is object[] array && array.Length == 3, "Array payloads should deserialize as object[] for object parameters.");

var buildObjectSyncBatchesMethod = typeof(ArmaMethods).GetMethod(
    "BuildObjectSyncBatches",
    BindingFlags.NonPublic | BindingFlags.Static
);
Assert(buildObjectSyncBatchesMethod is not null, "Object synchronization should expose a bounded batching seam.");

var objectsToSync = Enumerable.Range(0, 250)
    .Select(index => new ArmaObject($"object-{index}", new Dictionary<string, object?>
    {
        ["ItemClass"] = "Land_HelipadEmpty_F",
        ["Position"] = new object[] { index, 0, 0 }
    }))
    .ToList();

var objectSyncBatches = (IReadOnlyList<object?[]>)buildObjectSyncBatchesMethod!.Invoke(null, [objectsToSync])!;
Assert(objectSyncBatches.Count == 4, "250 objects should use four callbacks rather than 250 callbacks.");
Assert(objectSyncBatches.All(batch => batch.Length <= 64), "Object synchronization batches must stay bounded.");
Assert(objectSyncBatches.Sum(batch => batch.Length) == objectsToSync.Count, "Object synchronization batches must preserve every object.");
Assert(((object?[])objectSyncBatches[0][0]!)[0]?.ToString() == "object-0", "Object synchronization batches must preserve object order.");
Assert(((object?[])objectSyncBatches[^1][^1]!)[0]?.ToString() == "object-249", "The last object must remain in the final batch.");

var parseObjectBatchMethod = typeof(ArmaMethods).GetMethod("ParseObjectBatch", BindingFlags.NonPublic | BindingFlags.Static);
Assert(parseObjectBatchMethod is not null, "Host object upload should expose its batch parser seam.");
var parsedObjectBatch = (List<ArmaObject>)parseObjectBatchMethod!.Invoke(null, [new object[]
{
    new object[]
    {
        "host-object-1",
        new object[]
        {
            new object[] { "ItemClass", "Land_HelipadEmpty_F" },
            new object[] { "Position", new object[] { 1, 2, 3 } }
        }
    }
}])!;
Assert(parsedObjectBatch.Count == 1 && parsedObjectBatch[0].Id == "host-object-1", "Host object batches must preserve object IDs.");
Assert(parsedObjectBatch[0].Attributes.Count == 2, "Host object batches must preserve all attributes.");

const int largeObjectCount = 50_000;
const int tcpMessageLimitBytes = 10_000_000;
const int objectSyncPageSize = 1024;

var largeObjectSet = Enumerable.Range(0, largeObjectCount)
    .Select(index => new ArmaObject($"large-object-{index:D5}", new Dictionary<string, object?>
    {
        ["ItemClass"] = "Land_Cargo_House_V1_F",
        ["Position"] = new object[] { index % 1000, index / 1000, 0 },
        ["Rotation"] = new object[] { index % 360, 0, 0 },
        ["Name"] = $"Object {index}",
        ["Init"] = "this allowDamage false; this enableSimulationGlobal false;",
        ["Description"] = "EdenOnline 50k synchronization fixture",
        ["Skill"] = 0.5,
        ["Health"] = 1.0,
        ["Fuel"] = 1.0,
        ["Ammo"] = 1.0,
        ["DynamicSimulation"] = false,
        ["EnableSimulation"] = true
    }))
    .ToList();

var largeBatchStopwatch = System.Diagnostics.Stopwatch.StartNew();
var largeObjectSyncBatches = (IReadOnlyList<object?[]>)buildObjectSyncBatchesMethod.Invoke(null, [largeObjectSet])!;
largeBatchStopwatch.Stop();
Assert(largeObjectSyncBatches.Count == 782, "50,000 objects should use 782 callbacks rather than 50,000 callbacks.");
Assert(largeObjectSyncBatches.Sum(batch => batch.Length) == largeObjectCount, "The 50,000-object callback plan must preserve every object.");

var printArrayMethod = serializerType!.GetMethod("PrintArray", BindingFlags.NonPublic | BindingFlags.Static);
Assert(printArrayMethod is not null, "The Arma payload serializer should expose its callback serialization seam.");
string representativeArmaPayload = (string)printArrayMethod!.Invoke(null, [new object?[]
{
    "alpha",
    true,
    null,
    new object?[] { 1, 2 },
    new Dictionary<string, object?> { ["key"] = "value" }
}])!;
Assert(representativeArmaPayload == "[\"alpha\",true,nil,[1,2],[[\"key\",\"value\"]]]",
    "The optimized serializer must preserve the existing Arma wire format.");
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
long callbackSerializationAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
var callbackSerializationStopwatch = System.Diagnostics.Stopwatch.StartNew();
long totalObjectCallbackBytes = 0;
int largestObjectCallbackBytes = 0;
foreach (object?[] batch in largeObjectSyncBatches)
{
    string payload = (string)printArrayMethod!.Invoke(null, [new object?[] { batch }])!;
    int payloadBytes = Encoding.UTF8.GetByteCount(payload);
    totalObjectCallbackBytes += payloadBytes;
    largestObjectCallbackBytes = Math.Max(largestObjectCallbackBytes, payloadBytes);
}
callbackSerializationStopwatch.Stop();
long callbackSerializationAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - callbackSerializationAllocatedBefore;
Assert(largestObjectCallbackBytes < 1_000_000, "Representative object callback batches should remain comfortably bounded.");
Assert(callbackSerializationAllocatedBytes < 150_000_000, "50,000-object callback serialization should avoid recursive intermediate strings.");

string unpagedObjectPayload = DynTypeSerializer.Serializer.Serialize(largeObjectSet);
int unpagedObjectPayloadBytes = Encoding.UTF8.GetByteCount(unpagedObjectPayload);
Assert(unpagedObjectPayloadBytes > tcpMessageLimitBytes, "The 50,000-object fixture must exercise the TCP message-size failure mode.");

var beginObjectSyncMethod = typeof(ServerNetworkMethods).GetMethod("BeginObjectSync", BindingFlags.Public | BindingFlags.Static);
var getObjectSyncPageMethod = typeof(ServerNetworkMethods).GetMethod("GetObjectSyncPage", BindingFlags.Public | BindingFlags.Static);
var endObjectSyncMethod = typeof(ServerNetworkMethods).GetMethod("EndObjectSync", BindingFlags.Public | BindingFlags.Static);
Assert(beginObjectSyncMethod is not null && getObjectSyncPageMethod is not null && endObjectSyncMethod is not null,
    "Large object synchronization must use a bounded server snapshot API.");

ServerStateManager.ServerObjectManager.Clear();
foreach (ArmaObject obj in largeObjectSet)
    ServerStateManager.ServerObjectManager.AddOrUpdateObject(obj);

var syncRequest = new NetworkMessage { SenderId = 42 };
int snapshotCount = (int)beginObjectSyncMethod!.Invoke(null, [syncRequest])!;
Assert(snapshotCount == largeObjectCount, "The object synchronization snapshot must contain all 50,000 objects.");

int pagedObjectCount = 0;
int pageOffset = 0;
int pageCount = 0;
int largestPageBytes = 0;
while (pageOffset < snapshotCount)
{
    var page = (List<ArmaObject>)getObjectSyncPageMethod!.Invoke(null, [syncRequest, pageOffset, objectSyncPageSize])!;
    Assert(page.Count > 0 && page.Count <= objectSyncPageSize, "Every object synchronization page must be non-empty and bounded.");
    int pageBytes = Encoding.UTF8.GetByteCount(DynTypeSerializer.Serializer.Serialize(page));
    Assert(pageBytes <= 8_000_000, "Every representative object synchronization page must preserve transport headroom.");
    largestPageBytes = Math.Max(largestPageBytes, pageBytes);

    pagedObjectCount += page.Count;
    pageOffset += page.Count;
    pageCount++;
}

Assert(pagedObjectCount == largeObjectCount, "Paged synchronization must preserve all 50,000 objects.");
Assert(pageCount == 49, "50,000 objects should be transferred in 49 bounded network pages.");
Assert((bool)endObjectSyncMethod!.Invoke(null, [syncRequest])!, "The server must release the synchronization snapshot.");
Assert(!ServerStateManager.ObjectSyncSnapshots.ContainsKey(syncRequest.SenderId), "Released synchronization snapshots must not retain client state.");
ServerStateManager.ServerObjectManager.Clear();

string oversizedAttribute = new('x', 200_000);
for (int index = 0; index < 64; index++)
{
    ServerStateManager.ServerObjectManager.AddOrUpdateObject(new ArmaObject($"oversized-{index}", new Dictionary<string, object?>
    {
        ["ItemClass"] = "Land_HelipadEmpty_F",
        ["Position"] = new object[] { index, 0, 0 },
        ["Init"] = oversizedAttribute
    }));
}

var oversizedSyncRequest = new NetworkMessage { SenderId = 43 };
Assert((int)beginObjectSyncMethod!.Invoke(null, [oversizedSyncRequest])! == 64, "Oversized-page fixture should contain all objects.");
var adaptivelySizedPage = (List<ArmaObject>)getObjectSyncPageMethod!.Invoke(null, [oversizedSyncRequest, 0, objectSyncPageSize])!;
Assert(adaptivelySizedPage.Count < 64, "Object pages should shrink when their serialized payload is too large.");
Assert(Encoding.UTF8.GetByteCount(DynTypeSerializer.Serializer.Serialize(adaptivelySizedPage)) <= 8_000_000,
    "Adaptively sized object pages must retain transport headroom.");
Assert((bool)endObjectSyncMethod!.Invoke(null, [oversizedSyncRequest])!, "Oversized synchronization snapshots must be released.");
ServerStateManager.ServerObjectManager.Clear();

var largeConnectionSet = Enumerable.Range(0, largeObjectCount)
    .Select(index => new ArmaSyncConnection(
        $"large-object-{index:D5}",
        $"large-object-{(index + 1) % largeObjectCount:D5}",
        "Sync"
    ))
    .ToList();

int connectionPayloadBytes = Encoding.UTF8.GetByteCount(DynTypeSerializer.Serializer.Serialize(largeConnectionSet));
Assert(connectionPayloadBytes < tcpMessageLimitBytes, "The representative 50,000-connection response must fit the TCP message limit.");

var buildConnectionSyncBatchesMethod = typeof(ArmaMethods).GetMethod(
    "BuildConnectionSyncBatches",
    BindingFlags.NonPublic | BindingFlags.Static
);
Assert(buildConnectionSyncBatchesMethod is not null, "Initial synchronization connections must use bounded callbacks.");

var largeConnectionBatches = (IReadOnlyList<object?[]>)buildConnectionSyncBatchesMethod!.Invoke(null, [largeConnectionSet])!;
Assert(largeConnectionBatches.Count == 391, "50,000 synchronization connections should use 391 callbacks rather than 50,000 callbacks.");
Assert(largeConnectionBatches.Sum(batch => batch.Length) == largeObjectCount, "Connection callback batches must preserve all 50,000 connections.");

var parseConnectionBatchMethod = typeof(ArmaMethods).GetMethod("ParseConnectionBatch", BindingFlags.NonPublic | BindingFlags.Static);
Assert(parseConnectionBatchMethod is not null, "Host connection upload should expose its batch parser seam.");
var parsedConnectionBatch = (List<ArmaSyncConnection>)parseConnectionBatchMethod!.Invoke(null, [new object[]
{
    new object[] { "host-object-1", "host-object-2", "Sync" }
}])!;
Assert(parsedConnectionBatch.Count == 1 && parsedConnectionBatch[0].ToID == "host-object-2", "Host connection batches must preserve connection endpoints.");

ServerStateManager.SyncConnections.Clear();
var connectionIndexStopwatch = System.Diagnostics.Stopwatch.StartNew();
ServerNetworkMethods.CreateSyncConnectionsBatch(largeConnectionSet);
connectionIndexStopwatch.Stop();
Assert(ServerStateManager.SyncConnections.Count == largeObjectCount, "The server connection index must contain all 50,000 unique connections.");
ServerNetworkMethods.CreateSyncConnectionsBatch(largeConnectionSet);
Assert(ServerStateManager.SyncConnections.Count == largeObjectCount, "The server connection index must reject duplicate connections.");
Assert(ServerNetworkMethods.RemoveSyncConnection(largeConnectionSet[0]), "Indexed connection removal should succeed.");
Assert(ServerStateManager.SyncConnections.Count == largeObjectCount - 1, "Indexed connection removal must remove exactly one connection.");
ServerStateManager.SyncConnections.Clear();

Console.WriteLine($"50k sync probe: objectCallbacks={largeObjectSyncBatches.Count}, connectionCallbacks={largeConnectionBatches.Count}, pages={pageCount}, largestPageBytes={largestPageBytes}, largestCallbackBytes={largestObjectCallbackBytes}, callbackBytes={totalObjectCallbackBytes}, callbackAllocatedBytes={callbackSerializationAllocatedBytes}, objectBytes={unpagedObjectPayloadBytes}, connectionBytes={connectionPayloadBytes}, batchPlanMs={largeBatchStopwatch.ElapsedMilliseconds}, callbackSerializeMs={callbackSerializationStopwatch.ElapsedMilliseconds}, connectionIndexMs={connectionIndexStopwatch.ElapsedMilliseconds}");

bool previousLoggerEnabled = Logger.Enabled;
bool previousLogToConsole = Logger.LogToConsole;
Logger.LogLevel previousLogLevel = Logger.CurrentLogLevel;
Logger.Enabled = true;
Logger.LogToConsole = false;
Logger.CurrentLogLevel = Logger.LogLevel.Info;

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
long filteredLogAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
var filteredLogStopwatch = System.Diagnostics.Stopwatch.StartNew();
for (int index = 0; index < 100_000; index++)
    Logger.Debug($"filtered debug event index={index}, method=CameraUpdate, payloadChars={index % 1024}");
filteredLogStopwatch.Stop();
long filteredLogAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - filteredLogAllocatedBefore;

Logger.Enabled = previousLoggerEnabled;
Logger.LogToConsole = previousLogToConsole;
Logger.CurrentLogLevel = previousLogLevel;

Console.WriteLine($"logging probe: filteredCalls=100000, allocatedBytes={filteredLogAllocatedBytes}, elapsedMs={filteredLogStopwatch.ElapsedMilliseconds}");
Assert(filteredLogAllocatedBytes < 1_000_000, "Filtered debug logging must not eagerly allocate formatted messages.");

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
long unhandledEventAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
var unhandledEventStopwatch = System.Diagnostics.Stopwatch.StartNew();
for (int index = 0; index < 100_000; index++)
    UIEvents.RaiseMouseMove(null, index, index);
unhandledEventStopwatch.Stop();
long unhandledEventAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - unhandledEventAllocatedBefore;

Console.WriteLine($"core event probe: unhandledCalls=100000, allocatedBytes={unhandledEventAllocatedBytes}, elapsedMs={unhandledEventStopwatch.ElapsedMilliseconds}");
Assert(unhandledEventAllocatedBytes < 1_000_000, "Unsubscribed core events must not allocate argument arrays.");

MethodBuilder.RegisterServerMethods(new ReflectionFallbackRpcMethods());
MethodInfo callServerMethod = typeof(MethodBuilder)
    .GetMethod("CallServerMethod", BindingFlags.NonPublic | BindingFlags.Static)!
    .MakeGenericMethod(typeof(int));
object[] fallbackArguments = Enumerable.Range(1, 16).Cast<object>().ToArray();
var fallbackRequest = new NetworkMessage { SenderId = 68_195 };
int fallbackResult = (int)callServerMethod.Invoke(null,
    [nameof(ReflectionFallbackRpcMethods.SumWithRequestContext), fallbackRequest, fallbackArguments])!;
Assert(fallbackResult == 68_331,
    "Reflection-fallback RPC methods must receive the hidden NetworkMessage before their public arguments.");

MethodInfo? createRemoteErrorPayloadMethod = typeof(MessageBuilder).GetMethod(
    "CreateRemoteErrorPayload",
    BindingFlags.NonPublic | BindingFlags.Static
);
MethodInfo? unpackResponsePayloadMethod = typeof(MessageBuilder).GetMethod(
    "UnpackResponsePayload",
    BindingFlags.NonPublic | BindingFlags.Static
);
Assert(createRemoteErrorPayloadMethod is not null && unpackResponsePayloadMethod is not null,
    "RPC responses must expose a structured remote-error decoding seam.");

const string remoteFailureMessage = "No object synchronization snapshot exists for client 68195.";
string remoteErrorPayload = (string)createRemoteErrorPayloadMethod!.Invoke(null,
    ["GetObjectSyncPage", new InvalidOperationException(remoteFailureMessage)])!;
MethodInfo unpackObjectPageResponseMethod = unpackResponsePayloadMethod!
    .MakeGenericMethod(typeof(List<ArmaObject>));

RemoteMethodException? decodedRemoteFailure = null;
try
{
    _ = unpackObjectPageResponseMethod.Invoke(null,
        [remoteErrorPayload, 1, "GetObjectSyncPage"]);
}
catch (TargetInvocationException ex) when (ex.InnerException is RemoteMethodException remoteException)
{
    decodedRemoteFailure = remoteException;
}

Assert(decodedRemoteFailure is not null,
    "A failed RPC response must throw RemoteMethodException instead of deserializing null.");
Assert(decodedRemoteFailure!.TargetId == 1 && decodedRemoteFailure.MethodName == "GetObjectSyncPage",
    "Remote RPC failures must identify their target and method.");
Assert(decodedRemoteFailure.RemoteMessage == remoteFailureMessage,
    "Remote RPC failures must preserve the actionable server error message.");
Assert(decodedRemoteFailure.Message.Contains("GetObjectSyncPage", StringComparison.Ordinal)
    && decodedRemoteFailure.Message.Contains("System.InvalidOperationException", StringComparison.Ordinal),
    "Remote RPC failure messages exposed to Arma must include method and exception context.");

string successfulPagePayload = DynTypeSerializer.Serializer.Serialize(new List<ArmaObject> { objectsToSync[0] });
var decodedSuccessfulPage = (List<ArmaObject>)unpackObjectPageResponseMethod.Invoke(null,
    [successfulPagePayload, 1, "GetObjectSyncPage"])!;
Assert(decodedSuccessfulPage.Count == 1 && decodedSuccessfulPage[0].Id == "object-0",
    "Successful RPC payloads must retain their existing wire format.");

const int failingClientId = 68_195;
KeyExchange.InitializeClientKeyExchange();
KeyExchange.InitializeServerKeyExchange(
    failingClientId,
    Convert.ToBase64String(KeyExchange.ClientPublicKey!)
);
KeyExchange.ComputeClientSharedSecret(KeyExchange.GetServerPublicKey(failingClientId)!);

var failingRequest = new NetworkMessage
{
    SenderId = failingClientId,
    TargetId = [Server.SERVER_ID],
    MessageType = MessageType.Custom,
    MessageId = 7,
    Payload = DynTypeSerializer.Serializer.Serialize(new RpcRequestFixture
    {
        MethodName = nameof(ReflectionFallbackRpcMethods.AlwaysFails),
        Args = []
    })
};

var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
listener.Start();
int loopbackPort = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
using var responseReader = new System.Net.Sockets.TcpClient();
await responseReader.ConnectAsync(System.Net.IPAddress.Loopback, loopbackPort);
using System.Net.Sockets.TcpClient responseWriter = await listener.AcceptTcpClientAsync();
listener.Stop();

MethodInfo handleCustomMessageMethod = typeof(MessageBuilder).GetMethod(
    "HandleCustomMessage",
    BindingFlags.NonPublic | BindingFlags.Static
)!;
await (Task)handleCustomMessageMethod.Invoke(null,
    [responseWriter.GetStream(), failingRequest, CancellationToken.None])!;

MethodInfo readTcpMessageMethod = typeof(MessageBuilder).GetMethod(
    "ReadTcpMessage",
    BindingFlags.NonPublic | BindingFlags.Static,
    binder: null,
    types: [typeof(System.Net.Sockets.NetworkStream), typeof(int?)],
    modifiers: null
)!;
var failedResponse = (NetworkMessage)readTcpMessageMethod.Invoke(null,
    [responseReader.GetStream(), null])!;

RemoteMethodException? transportedRemoteFailure = null;
try
{
    _ = unpackObjectPageResponseMethod.Invoke(null,
        [failedResponse.Payload, Server.SERVER_ID, nameof(ReflectionFallbackRpcMethods.AlwaysFails)]);
}
catch (TargetInvocationException ex) when (ex.InnerException is RemoteMethodException remoteException)
{
    transportedRemoteFailure = remoteException;
}
finally
{
    KeyExchange.RemoveServerKeyExchange(failingClientId);
}

Assert(transportedRemoteFailure?.RemoteMessage == ReflectionFallbackRpcMethods.FailureMessage,
    "Thrown RPC methods must transport their server error instead of a null response.");

Logger.CloseWriter();
Console.WriteLine("Smoke tests passed.");

public sealed class ReflectionFallbackRpcMethods
{
    public const string FailureMessage = "object synchronization page failed on the server";

    public static int AlwaysFails(NetworkMessage request)
    {
        throw new InvalidOperationException(FailureMessage);
    }

    public static int SumWithRequestContext(
        NetworkMessage request,
        int value01,
        int value02,
        int value03,
        int value04,
        int value05,
        int value06,
        int value07,
        int value08,
        int value09,
        int value10,
        int value11,
        int value12,
        int value13,
        int value14,
        int value15,
        int value16)
    {
        return request.SenderId
            + value01 + value02 + value03 + value04
            + value05 + value06 + value07 + value08
            + value09 + value10 + value11 + value12
            + value13 + value14 + value15 + value16;
    }
}

public sealed class RpcRequestFixture
{
    public string? MethodName { get; init; }
    public object?[] Args { get; init; } = [];
}
