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

var dragSessions = new ObjectDragSessionManager();
var dragStart = new ObjectDragStart("object-dragged", "019d-drag-a");
Assert(dragSessions.TryStart(2, dragStart) == ObjectDragStartResult.Accepted,
    "The first drag start for an object must acquire its session.");
Assert(dragSessions.TryAdvance(2, new ObjectDragUpdate(
    "object-dragged", "019d-drag-a", 1,
    [10.0, 20.0, 0.0], [0.0, 0.0, 45.0])),
    "The active drag owner must be able to advance the sequence.");
Assert(!dragSessions.TryAdvance(2, new ObjectDragUpdate(
    "object-dragged", "019d-drag-a", 1,
    [11.0, 20.0, 0.0], [0.0, 0.0, 45.0])),
    "Duplicate UDP sequence numbers must be discarded.");
var dragEnd = new ObjectDragEnd(
    "object-dragged", "019d-drag-a", 2,
    [12.0, 20.0, 0.0], [0.0, 0.0, 45.0]);
Assert(dragSessions.TryEnd(2, dragEnd), "The active owner must be able to end its drag.");
Assert(!dragSessions.TryAdvance(2, new ObjectDragUpdate(
    "object-dragged", "019d-drag-a", 2,
    [999.0, 999.0, 999.0], [0.0, 0.0, 0.0])),
    "UDP updates received after END_DRAG must be discarded.");
Assert(dragSessions.TryStart(4, new ObjectDragStart("object-dragged", "019d-stale-contender")) == ObjectDragStartResult.Rejected,
    "A delayed competing START_DRAG from an ended generation must not reopen the object lock.");
Assert(dragSessions.TryStart(3, new ObjectDragStart("object-dragged", "019d-drag-b") { Generation = 1 }) == ObjectDragStartResult.Accepted,
    "An object must become draggable again under a new Drag ID after END_DRAG.");
Assert(!dragSessions.TryAdvance(2, new ObjectDragUpdate(
    "object-dragged", "019d-drag-a", 3,
    [999.0, 999.0, 999.0], [0.0, 0.0, 0.0])),
    "Late UDP from an ended drag must not affect a newer drag of the same object.");

var acquisitionSessions = new ObjectDragSessionManager();
var acquisitionStart = new ObjectDragStart("acquired-object", "019d-acquisition");
Assert(acquisitionSessions.TryBeginAcquisition(2, acquisitionStart, [3, 4]) == ObjectDragStartResult.Accepted,
    "A local drag proposal must reserve the object while peer acknowledgements are collected.");
acquisitionSessions.AcknowledgeAcquisition(3, new ObjectDragStartAcknowledgement(
    "acquired-object", "019d-acquisition", accepted: true));
acquisitionSessions.AcknowledgeAcquisition(4, new ObjectDragStartAcknowledgement(
    "acquired-object", "019d-acquisition", accepted: true));
Assert(await acquisitionSessions.WaitForAcquisitionAsync(
        "acquired-object", "019d-acquisition", TimeSpan.FromMilliseconds(100)),
    "A drag must become active only after every expected peer accepted its proposal.");

var rejectedAcquisitionSessions = new ObjectDragSessionManager();
var rejectedAcquisitionStart = new ObjectDragStart("contested-object", "019d-rejected");
Assert(rejectedAcquisitionSessions.TryBeginAcquisition(2, rejectedAcquisitionStart, [3]) == ObjectDragStartResult.Accepted,
    "A competing drag starts as a proposal until peers vote.");
rejectedAcquisitionSessions.AcknowledgeAcquisition(3, new ObjectDragStartAcknowledgement(
    "contested-object", "019d-rejected", accepted: false));
Assert(!await rejectedAcquisitionSessions.WaitForAcquisitionAsync(
        "contested-object", "019d-rejected", TimeSpan.FromMilliseconds(100)),
    "Any peer rejection must prevent a local proposal from being reported as acquired.");

var supersededAcquisitionSessions = new ObjectDragSessionManager();
var supersededStart = new ObjectDragStart("superseded-object", "019d-superseded");
supersededAcquisitionSessions.TryBeginAcquisition(2, supersededStart, [3]);
Task<bool> supersededAcquisition = supersededAcquisitionSessions.WaitForAcquisitionAsync(
    "superseded-object", "019d-superseded", TimeSpan.FromSeconds(1));
Assert(supersededAcquisitionSessions.TryStart(3, new ObjectDragStart(
        "superseded-object", "019d-new-generation") { Generation = 1 }) == ObjectDragStartResult.Replaced,
    "A newer persisted object generation must supersede an older pending proposal.");
Assert(!await supersededAcquisition,
    "A superseded proposal must wake its waiter and fail acquisition immediately.");

var disconnectSessions = new ObjectDragSessionManager();
disconnectSessions.TryStart(7, new ObjectDragStart("disconnect-a", "drag-a"));
disconnectSessions.TryStart(7, new ObjectDragStart("disconnect-b", "drag-b"));
disconnectSessions.TryStart(8, new ObjectDragStart("still-connected", "drag-c"));
IReadOnlyList<ObjectDragSession> releasedDrags = disconnectSessions.ReleaseOwner(7);
Assert(releasedDrags.Count == 2
    && !disconnectSessions.TryGetActive("disconnect-a", out _)
    && !disconnectSessions.TryGetActive("disconnect-b", out _)
    && disconnectSessions.TryGetActive("still-connected", out _),
    "Disconnecting a client must release exactly that client's remote drag locks.");
Assert(disconnectSessions.TryStart(7, new ObjectDragStart("disconnect-a", "drag-a")) == ObjectDragStartResult.Rejected,
    "A delayed START_DRAG from a disconnected owner must not recreate its released lock.");
Assert(disconnectSessions.TryStart(9, new ObjectDragStart("disconnect-a", "fresh-drag")) == ObjectDragStartResult.Accepted,
    "Disconnect cancellation must not advance a generation that later joiners cannot observe.");

var retryableEndSessions = new ObjectDragSessionManager();
retryableEndSessions.TryStart(2, new ObjectDragStart("retryable-end", "ending-drag"));
var retryableEnd = new ObjectDragEnd(
    "retryable-end", "ending-drag", 1,
    [1.0, 2.0, 3.0], [0.0, 0.0, 90.0]);
Assert(retryableEndSessions.TryPrepareEnd(2, retryableEnd),
    "END must enter a retryable prepared state before persistence and broadcast.");
Assert(retryableEndSessions.TryGetActive("retryable-end", out ObjectDragSession? preparedEnd)
    && preparedEnd!.IsEnding,
    "A prepared END must retain its session until both external writes succeed.");
Assert(retryableEndSessions.TryPrepareEnd(2, retryableEnd),
    "Retrying the same END after a transport failure must remain valid.");
var laterRetryEnd = new ObjectDragEnd(
    "retryable-end", "ending-drag", 1,
    [1.0, 2.0, 3.0], [0.0, 0.0, 90.0], generation: 0, nextGeneration: 99);
Assert(retryableEndSessions.TryPrepareEnd(2, laterRetryEnd)
    && retryableEndSessions.TryGetActive("retryable-end", out preparedEnd)
    && ReferenceEquals(preparedEnd!.PreparedEnd, retryableEnd),
    "END retries must retain the first prepared generation and payload.");
Assert(retryableEndSessions.TryMarkEndPersistenceAttempted(2, retryableEnd)
    && preparedEnd!.EndPersistenceAttempted,
    "Once persistence may have happened, cleanup must never roll peers back with cancellation.");
Assert(retryableEndSessions.TryMarkEndPersisted(2, retryableEnd)
    && preparedEnd!.EndPersisted,
    "A confirmed server write must be retained so abort cleanup never rolls peers back.");
Assert(!retryableEndSessions.TryAdvance(2, new ObjectDragUpdate(
    "retryable-end", "ending-drag", 2,
    [9.0, 9.0, 9.0], [0.0, 0.0, 0.0])),
    "UDP updates must stop once END preparation begins.");
Assert(retryableEndSessions.TryEnd(2, retryableEnd)
    && !retryableEndSessions.TryGetActive("retryable-end", out _),
    "A prepared END must be committed only after persistence and broadcast succeed.");

var abandonedEndSessions = new ObjectDragSessionManager();
abandonedEndSessions.TryStart(2, new ObjectDragStart("abandoned-end", "abandoned-drag"));
var abandonedEnd = new ObjectDragEnd(
    "abandoned-end", "abandoned-drag", 1,
    [4.0, 5.0, 6.0], [0.0, 0.0, 180.0]);
Assert(abandonedEndSessions.TryPrepareEnd(2, abandonedEnd),
    "A failed END test must first enter the prepared state.");
Assert(abandonedEndSessions.TryExpire("abandoned-end", "abandoned-drag")
    && abandonedEndSessions.TryGetActive("abandoned-end", out ObjectDragSession? timedOutEnd)
    && timedOutEnd!.IsTimedOut,
    "An inactive drag must stop blocking new reservations without losing its reliable END identity.");
Assert(abandonedEndSessions.TryEnd(2, abandonedEnd),
    "A reliable END received after inactivity cleanup must remain authoritative.");

var revisionGuardObjects = new ClientStateManager();
revisionGuardObjects.AddOrUpdateObject(new ArmaObject("revision-guard", new Dictionary<string, object?>
{
    ["Position"] = new object[] { 20.0, 0.0, 0.0 }
}) { Timestamp = 20 });
Assert(!revisionGuardObjects.AddOrUpdateObjectIfRevisionCurrent(new ArmaObject(
    "revision-guard",
    new Dictionary<string, object?> { ["Position"] = new object[] { 10.0, 0.0, 0.0 } }) { Timestamp = 10 }),
    "A delayed prepared END must not overwrite a newer persisted object generation.");
Assert(revisionGuardObjects.TryGetObject("revision-guard", out ArmaObject? guardedObject)
    && guardedObject!.Timestamp == 20,
    "Rejecting a stale END must retain the newer server revision.");
Assert(revisionGuardObjects.AddOrUpdateObjectIfRevisionCurrent(new ArmaObject(
    "revision-guard",
    new Dictionary<string, object?> { ["Rotation"] = new object[] { 0.0, 0.0, 90.0 } }) { Timestamp = 20 }),
    "Retrying the same prepared END revision must remain idempotent.");
revisionGuardObjects.RemoveObject("revision-guard");
Assert(!revisionGuardObjects.AddOrUpdateObjectIfRevisionCurrent(new ArmaObject(
    "revision-guard",
    new Dictionary<string, object?> { ["Position"] = new object[] { 30.0, 0.0, 0.0 } }) { Timestamp = 30 })
    && !revisionGuardObjects.TryGetObject("revision-guard", out _),
    "A delayed prepared END must not resurrect an object that was deleted.");

var expiredStartSessions = new ObjectDragSessionManager();
expiredStartSessions.TryStart(2, new ObjectDragStart("expired-start", "orphaned-drag"));
Assert(expiredStartSessions.TryExpire("expired-start", "orphaned-drag"),
    "A partially delivered START must become replaceable after the inactivity timeout.");
Assert(expiredStartSessions.TryStart(3, new ObjectDragStart("expired-start", "replacement-drag")) == ObjectDragStartResult.Replaced,
    "An expired START must not lock the object forever.");
Assert(expiredStartSessions.TryStart(2, new ObjectDragStart("expired-start", "orphaned-drag")) == ObjectDragStartResult.Rejected,
    "Replacing an expired session must still reject delayed packets from its old Drag ID.");

var joiningClientSessions = new ObjectDragSessionManager();
joiningClientSessions.ObserveGeneration("joined-object", 42);
Assert(joiningClientSessions.TryStart(9, new ObjectDragStart("joined-object", "stale-start")) == ObjectDragStartResult.Rejected,
    "A joining client must reject drag starts older than the synchronized object revision.");
Assert(joiningClientSessions.TryStart(9, new ObjectDragStart("joined-object", "current-start") { Generation = 42 }) == ObjectDragStartResult.Accepted,
    "A joining client must accept a drag based on its synchronized object revision.");

var setMissionAttributeMethod = typeof(ArmaMethods).GetMethod("SetMissionAttribute", BindingFlags.Public | BindingFlags.Static);
Assert(setMissionAttributeMethod is not null, "SetMissionAttribute should exist.");

var serializerType = typeof(ArmaMethods).Assembly.GetType("EdenOnline.Serializer");
Assert(serializerType is not null, "Serializer type should exist.");

var deserializeMethod = serializerType?.GetMethod("DeserializeArmaArray", BindingFlags.NonPublic | BindingFlags.Static);
Assert(deserializeMethod is not null, "DeserializeArmaArray should exist.");

var values = (object?[])deserializeMethod!.Invoke(null, [setMissionAttributeMethod!, new string[] { "\"Briefing\"", "\"Scenario\"", "[1,2,3]" }, null])!;
Assert(values[2] is object[] array && array.Length == 3, "Array payloads should deserialize as object[] for object parameters.");

var objectsToSync = Enumerable.Range(0, 250)
    .Select(index => new ArmaObject($"object-{index}", new Dictionary<string, object?>
    {
        ["ItemClass"] = "Land_HelipadEmpty_F",
        ["Position"] = new object[] { index, 0, 0 }
    }))
    .ToList();
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

var beginInitialSyncMethod = typeof(ServerStateManager).GetMethod("BeginInitialSync", BindingFlags.Public | BindingFlags.Static);
var initialSyncReadyProperty = typeof(ServerStateManager).GetProperty("IsInitialSyncReady", BindingFlags.Public | BindingFlags.Static);
var completeInitialSyncMethod = typeof(ServerNetworkMethods).GetMethod("CompleteInitialSync", BindingFlags.Public | BindingFlags.Static);
Assert(beginInitialSyncMethod is not null && initialSyncReadyProperty is not null && completeInitialSyncMethod is not null,
    "Host startup must expose an explicit initial-sync readiness contract.");

const int hostClientId = 2;
beginInitialSyncMethod!.Invoke(null, [hostClientId]);
Assert(!(bool)initialSyncReadyProperty!.GetValue(null)!, "Starting host initialization must close the server readiness gate.");

ServerStateManager.ServerObjectManager.Clear();
ServerStateManager.SyncConnections.Clear();
ServerStateManager.ServerObjectManager.AddOrUpdateObject(parsedObjectBatch[0]);
var readinessConnection = new ArmaSyncConnection("host-object-1", "host-object-2", "Sync");
ServerStateManager.SyncConnections.TryAdd(
    (readinessConnection.FromID, readinessConnection.ToID, readinessConnection.Type),
    readinessConnection
);

bool unauthorizedCompletionRejected = false;
try
{
    _ = completeInitialSyncMethod!.Invoke(null, [new NetworkMessage { SenderId = hostClientId + 1 }, 1, 1]);
}
catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
{
    unauthorizedCompletionRejected = true;
}
Assert(unauthorizedCompletionRejected, "Only the host may open the initial-sync readiness gate.");

bool incompleteSnapshotRejected = false;
try
{
    _ = completeInitialSyncMethod!.Invoke(null, [new NetworkMessage { SenderId = hostClientId }, 2, 1]);
}
catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
{
    incompleteSnapshotRejected = true;
}
Assert(incompleteSnapshotRejected, "The host must not become ready when uploaded counts do not match.");
Assert(!(bool)initialSyncReadyProperty.GetValue(null)!, "A rejected completion must leave the readiness gate closed.");

Assert((bool)completeInitialSyncMethod!.Invoke(null, [new NetworkMessage { SenderId = hostClientId }, 1, 1])!,
    "A complete host snapshot should open the readiness gate.");
Assert((bool)initialSyncReadyProperty.GetValue(null)!, "A verified host snapshot must make the server ready for joins.");

var registerServerAuthenticationMethod = typeof(ArmaMethods).GetMethod(
    "RegisterServerAuthentication",
    BindingFlags.NonPublic | BindingFlags.Static
);
Assert(registerServerAuthenticationMethod is not null, "Host authentication must be configurable for the readiness check.");
object[] expectedModHashes = ["mod-a", "mod-b"];
registerServerAuthenticationMethod!.Invoke(null, ["Altis", "2.22", expectedModHashes, "secret"]);
object[] validAuthentication = ["Altis", "2.22", new object[] { "mod-b", "mod-a" }, "secret"];

beginInitialSyncMethod.Invoke(null, [hostClientId]);
Client.ClientID = hostClientId;
(bool remoteJoinWhileLoading, string? remoteJoinError) = await Authentication.ServerValidateAsync(hostClientId + 1, validAuthentication);
Assert(!remoteJoinWhileLoading && remoteJoinError?.Contains("still loading", StringComparison.OrdinalIgnoreCase) == true,
    "Remote clients must receive an actionable rejection while the host snapshot is incomplete.");
(bool hostJoinWhileLoading, string? hostJoinError) = await Authentication.ServerValidateAsync(hostClientId, validAuthentication);
Assert(hostJoinWhileLoading && hostJoinError is null, "The local host must be admitted while publishing initial state.");

Assert((bool)completeInitialSyncMethod.Invoke(null, [new NetworkMessage { SenderId = hostClientId }, 1, 1])!,
    "The host should be able to open the admission gate after authentication.");
(bool remoteJoinAfterReady, string? remoteJoinAfterReadyError) = await Authentication.ServerValidateAsync(hostClientId + 1, validAuthentication);
Assert(remoteJoinAfterReady && remoteJoinAfterReadyError is null,
    "Remote clients must be admitted after the verified host snapshot is ready.");
Client.ClientID = 0;
ServerStateManager.Reset();

// Exercise the real handshake/authentication ordering, not only the validator.
// Rejected clients must never enter the server's broadcast-visible collection.
var admissionPortProbe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
admissionPortProbe.Start();
int admissionPort = ((System.Net.IPEndPoint)admissionPortProbe.LocalEndpoint).Port;
admissionPortProbe.Stop();

Authentication.Enabled = true;
Authentication.ServerDropOnFail = true;
Authentication.SetClientAuthentication(() => Task.FromResult<object[]?>([]));
Authentication.SetServerValidator((int _, object[]? _) => Task.FromResult(false));
await Server.StartAsync(admissionPort);

bool rejectedClientFailedToConnect = false;
try
{
    await Client.ConnectAsync("127.0.0.1", admissionPort);
}
catch
{
    rejectedClientFailedToConnect = true;
}

await Task.Delay(50);
Assert(rejectedClientFailedToConnect, "A rejected client connection must fail.");
Assert(Server.Clients.IsEmpty, "A rejected client must never become visible to server broadcasts.");

Authentication.SetServerValidator((int _, object[]? _) => Task.FromResult(true));
int admittedClientId = await Client.ConnectAsync("127.0.0.1", admissionPort);
for (int attempt = 0; attempt < 100 && !Server.Clients.ContainsKey(admittedClientId); attempt++)
    await Task.Delay(10);
Assert(Server.Clients.TryGetValue(admittedClientId, out Server.Connection? admittedClient)
    && admittedClient.Authenticated,
    "A successful client must become visible only after authentication.");

await Client.DisconnectAsync();
await Server.StopAsync();
Authentication.ClientAuthenticated = null;

MethodInfo? writeTcpPacketMethod = typeof(MessageBuilder).GetMethod(
    "WriteTcpPacketAsync",
    BindingFlags.NonPublic | BindingFlags.Static
);
MethodInfo? createRawPacketMethod = typeof(MessageBuilder).GetMethod(
    "CreatePacket",
    BindingFlags.NonPublic | BindingFlags.Static,
    binder: null,
    types: [typeof(NetworkMessage)],
    modifiers: null
);
MethodInfo? readSerializedPacketMethod = typeof(MessageBuilder).GetMethod(
    "ReadTcpMessage",
    BindingFlags.NonPublic | BindingFlags.Static,
    binder: null,
    types: [typeof(System.Net.Sockets.NetworkStream), typeof(int?)],
    modifiers: null
);
Assert(writeTcpPacketMethod is not null && createRawPacketMethod is not null && readSerializedPacketMethod is not null,
    "TCP transport must expose one serialized packet writer per stream.");

var serializedWriterListener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
serializedWriterListener.Start();
int serializedWriterPort = ((System.Net.IPEndPoint)serializedWriterListener.LocalEndpoint).Port;
using var serializedPacketReader = new System.Net.Sockets.TcpClient();
await serializedPacketReader.ConnectAsync(System.Net.IPAddress.Loopback, serializedWriterPort);
using System.Net.Sockets.TcpClient serializedPacketWriter = await serializedWriterListener.AcceptTcpClientAsync();
serializedWriterListener.Stop();

const int concurrentPacketCount = 128;
byte[][] concurrentPackets = Enumerable.Range(1, concurrentPacketCount)
    .Select(index => (byte[])createRawPacketMethod!.Invoke(null, [new NetworkMessage
    {
        SenderId = 2,
        TargetId = [Server.SERVER_ID],
        MessageType = MessageType.Handshake,
        MessageId = (ushort)index,
        Payload = new string((char)('A' + index % 26), 32_768)
    }])!)
    .ToArray();

Task<HashSet<ushort>> packetReaderTask = Task.Run(() =>
{
    var receivedIds = new HashSet<ushort>();
    for (int index = 0; index < concurrentPacketCount; index++)
    {
        var received = (NetworkMessage)readSerializedPacketMethod!.Invoke(null,
            [serializedPacketReader.GetStream(), null])!;
        receivedIds.Add(received.MessageId);
        Assert(received.Payload?.Length == 32_768, "Concurrent TCP writes must preserve complete packet payloads.");
    }
    return receivedIds;
});

Task[] concurrentWrites = concurrentPackets.Select(async packet =>
{
    object? pendingWrite = writeTcpPacketMethod!.Invoke(null,
        [serializedPacketWriter.GetStream(), new ReadOnlyMemory<byte>(packet), CancellationToken.None]);
    Assert(pendingWrite is ValueTask, "Serialized TCP writer must return a ValueTask.");
    await ((ValueTask)pendingWrite!);
}).ToArray();

await Task.WhenAll(concurrentWrites);
HashSet<ushort> concurrentPacketIds = await packetReaderTask.WaitAsync(TimeSpan.FromSeconds(10));
Assert(concurrentPacketIds.Count == concurrentPacketCount,
    "Concurrent TCP producers must deliver every framed packet exactly once.");

const int oversizedTcpPayloadLength = 12_000_000;
byte[] oversizedTcpPacket = (byte[])createRawPacketMethod!.Invoke(null, [new NetworkMessage
{
    SenderId = 2,
    TargetId = [Server.SERVER_ID],
    MessageType = MessageType.Handshake,
    MessageId = 65000,
    Payload = new string('Z', oversizedTcpPayloadLength)
}])!;

Task<NetworkMessage> oversizedPacketReaderTask = Task.Run(() =>
    (NetworkMessage)readSerializedPacketMethod!.Invoke(null,
        [serializedPacketReader.GetStream(), null])!);
object? oversizedPendingWrite = writeTcpPacketMethod!.Invoke(null,
    [serializedPacketWriter.GetStream(), new ReadOnlyMemory<byte>(oversizedTcpPacket), CancellationToken.None]);
Assert(oversizedPendingWrite is ValueTask, "Oversized TCP writes must use the normal serialized packet writer.");
await ((ValueTask)oversizedPendingWrite!);
NetworkMessage oversizedTcpRoundTrip = await oversizedPacketReaderTask.WaitAsync(TimeSpan.FromSeconds(10));
string? oversizedTcpPayload = oversizedTcpRoundTrip.Payload;
Assert(oversizedTcpRoundTrip.MessageId == 65000
    && oversizedTcpPayload?.Length == oversizedTcpPayloadLength
    && oversizedTcpPayload![0] == 'Z'
    && oversizedTcpPayload![^1] == 'Z',
    "The TCP layer must transparently fragment and reconstruct an oversized logical message.");

byte[] postFragmentPacket = (byte[])createRawPacketMethod.Invoke(null, [new NetworkMessage
{
    SenderId = 2,
    TargetId = [Server.SERVER_ID],
    MessageType = MessageType.Handshake,
    MessageId = 65001,
    Payload = "after-fragments"
}])!;
Task<NetworkMessage> postFragmentReaderTask = Task.Run(() =>
    (NetworkMessage)readSerializedPacketMethod!.Invoke(null,
        [serializedPacketReader.GetStream(), null])!);
object? postFragmentPendingWrite = writeTcpPacketMethod!.Invoke(null,
    [serializedPacketWriter.GetStream(), new ReadOnlyMemory<byte>(postFragmentPacket), CancellationToken.None]);
await ((ValueTask)postFragmentPendingWrite!);
NetworkMessage postFragmentRoundTrip = await postFragmentReaderTask.WaitAsync(TimeSpan.FromSeconds(10));
Assert(postFragmentRoundTrip.MessageId == 65001 && postFragmentRoundTrip.Payload == "after-fragments",
    "TCP fragment reassembly must leave the stream aligned for the next logical message.");

const int largeObjectCount = 50_000;
const int tcpMessageLimitBytes = 10_000_000;

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

var managedCallbackOverrideField = typeof(Extension).GetField(
    "_managedCallbackOverride",
    BindingFlags.NonPublic | BindingFlags.Static
);
Assert(managedCallbackOverrideField is not null,
    "SendToArma must expose a managed callback seam for its public transport behavior tests.");

var deliveredArmaCallbacks = new System.Collections.Concurrent.ConcurrentQueue<(string Method, string Data)>();
managedCallbackOverrideField!.SetValue(null, new Func<string, string, int>((method, data) =>
{
    deliveredArmaCallbacks.Enqueue((method, data));
    return 99;
}));

for (int index = 0; index < 300; index++)
{
    string method = index % 2 == 0 ? "CoalescedPosition" : "CoalescedDirection";
    Assert(Extension.SendToArma(method, [index]), "Every logical SendToArma call must enter the background queue.");
}

Assert(SpinWait.SpinUntil(() => !deliveredArmaCallbacks.IsEmpty, TimeSpan.FromSeconds(2)),
    "The SendToArma worker must deliver calls without an explicit flush.");
Thread.Sleep(50);

Assert(deliveredArmaCallbacks.Count == 1,
    "Three hundred SendToArma calls from one batching window must use one Arma callback despite queue backpressure.");
Assert(deliveredArmaCallbacks.TryDequeue(out var deliveredArmaBatch)
    && deliveredArmaBatch.Method == "EOEX_BATCH",
    "Coalesced callbacks must use the generic SQF batch envelope.");
Assert(deliveredArmaBatch.Data.StartsWith("[[\"CoalescedPosition\",[0]]", StringComparison.Ordinal)
    && deliveredArmaBatch.Data.EndsWith("[\"CoalescedDirection\",[299]]]", StringComparison.Ordinal),
    "The generic callback batch must retain the first and last logical messages in order.");
Assert(deliveredArmaBatch.Data.Split("\"CoalescedPosition\"", StringSplitOptions.None).Length - 1 == 150
    && deliveredArmaBatch.Data.Split("\"CoalescedDirection\"", StringSplitOptions.None).Length - 1 == 150,
    "The generic callback batch must preserve every logical message across unrelated methods.");

Assert(Extension.SendToArma("SingleProbe", ["unchanged"]), "A single logical callback must enter the same queue.");
Assert(SpinWait.SpinUntil(() => !deliveredArmaCallbacks.IsEmpty, TimeSpan.FromSeconds(2)),
    "A single logical callback must flush after the batching window.");
Assert(deliveredArmaCallbacks.TryDequeue(out var deliveredSingleCallback)
    && deliveredSingleCallback.Method == "SingleProbe"
    && deliveredSingleCallback.Data == "[\"unchanged\"]",
    "A lone SendToArma call must retain the legacy callback method and payload.");

var stalledCallbackEntered = new ManualResetEventSlim(false);
var releaseStalledCallback = new ManualResetEventSlim(false);
var stalledWindowDeliveries = new System.Collections.Concurrent.ConcurrentQueue<string>();
managedCallbackOverrideField.SetValue(null, new Func<string, string, int>((method, _) =>
{
    if (method == "StalledCallback")
    {
        stalledCallbackEntered.Set();
        releaseStalledCallback.Wait(TimeSpan.FromSeconds(2));
    }
    stalledWindowDeliveries.Enqueue(method);
    return 99;
}));

Assert(Extension.SendToArma("StalledCallback", [0]), "The stalled callback probe must enter the outbound queue.");
Assert(stalledCallbackEntered.Wait(TimeSpan.FromSeconds(2)), "The callback probe must reach the managed Arma sink.");
Assert(Extension.SendToArma("EarlierWindow", [1]), "The first delayed-window message must enter the queue.");
Thread.Sleep(25);
Assert(Extension.SendToArma("LaterWindow", [2]), "The second delayed-window message must enter the queue.");
releaseStalledCallback.Set();
Assert(SpinWait.SpinUntil(() => stalledWindowDeliveries.Count == 3, TimeSpan.FromSeconds(2)),
    "Messages queued while delivery is stalled must resume automatically.");
Assert(stalledWindowDeliveries.ToArray().SequenceEqual(["StalledCallback", "EarlierWindow", "LaterWindow"]),
    "A stalled callback must not merge logical calls that were enqueued in different frame windows.");
managedCallbackOverrideField.SetValue(null, null);

string unpagedObjectPayload = DynTypeSerializer.Serializer.Serialize(largeObjectSet);
int unpagedObjectPayloadBytes = Encoding.UTF8.GetByteCount(unpagedObjectPayload);
Assert(unpagedObjectPayloadBytes > tcpMessageLimitBytes, "The 50,000-object fixture must exercise the TCP message-size failure mode.");

ServerStateManager.ServerObjectManager.Clear();
foreach (ArmaObject obj in largeObjectSet)
    ServerStateManager.ServerObjectManager.AddOrUpdateObject(obj);
List<ArmaObject> completeObjectSnapshot = ServerNetworkMethods.GetAllObjects();
Assert(completeObjectSnapshot.Count == largeObjectCount
    && completeObjectSnapshot.Any(obj => obj.Id == largeObjectSet[0].Id)
    && completeObjectSnapshot.Any(obj => obj.Id == largeObjectSet[^1].Id),
    "Object synchronization must expose one complete logical response and leave transport fragmentation behind the API.");
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

Console.WriteLine($"50k sync probe: objectBytes={unpagedObjectPayloadBytes}, connectionBytes={connectionPayloadBytes}, connectionIndexMs={connectionIndexStopwatch.ElapsedMilliseconds}");

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

const string remoteFailureMessage = "Object synchronization failed for client 68195.";
string remoteErrorPayload = (string)createRemoteErrorPayloadMethod!.Invoke(null,
    ["GetAllObjects", new InvalidOperationException(remoteFailureMessage)])!;
MethodInfo unpackObjectPageResponseMethod = unpackResponsePayloadMethod!
    .MakeGenericMethod(typeof(List<ArmaObject>));

RemoteMethodException? decodedRemoteFailure = null;
try
{
    _ = unpackObjectPageResponseMethod.Invoke(null,
        [remoteErrorPayload, 1, "GetAllObjects"]);
}
catch (TargetInvocationException ex) when (ex.InnerException is RemoteMethodException remoteException)
{
    decodedRemoteFailure = remoteException;
}

Assert(decodedRemoteFailure is not null,
    "A failed RPC response must throw RemoteMethodException instead of deserializing null.");
Assert(decodedRemoteFailure!.TargetId == 1 && decodedRemoteFailure.MethodName == "GetAllObjects",
    "Remote RPC failures must identify their target and method.");
Assert(decodedRemoteFailure.RemoteMessage == remoteFailureMessage,
    "Remote RPC failures must preserve the actionable server error message.");
Assert(decodedRemoteFailure.Message.Contains("GetAllObjects", StringComparison.Ordinal)
    && decodedRemoteFailure.Message.Contains("System.InvalidOperationException", StringComparison.Ordinal),
    "Remote RPC failure messages exposed to Arma must include method and exception context.");

string successfulPagePayload = DynTypeSerializer.Serializer.Serialize(new List<ArmaObject> { objectsToSync[0] });
var decodedSuccessfulPage = (List<ArmaObject>)unpackObjectPageResponseMethod.Invoke(null,
    [successfulPagePayload, 1, "GetAllObjects"])!;
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
    public const string FailureMessage = "object synchronization failed on the server";

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
