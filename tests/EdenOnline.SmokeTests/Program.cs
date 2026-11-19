using System.Reflection;
using System.Runtime.InteropServices;
using DynTypeSerializer;
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

// Strings sent to Arma are parsed with parseSimpleArray, so a quote inside a value must be
// doubled the same way SQF escapes it. Attribute values such as Init or ammoBox contain quotes.
var printArrayMethod = serializerType?.GetMethod("PrintArray", BindingFlags.NonPublic | BindingFlags.Static);
Assert(printArrayMethod is not null, "PrintArray should exist.");

string printedArray = (string)printArrayMethod!.Invoke(null, [new object?[] { "hint \"hello\"" }])!;
Assert(printedArray == "[\"hint \"\"hello\"\"\"]", $"Quotes in strings must be doubled for SQF, got: {printedArray}");

// The reverse direction: SQF escapes a quote inside a string by doubling it, and attribute values
// such as Init or ammoBox contain quotes. The parser must turn that into legal JSON instead of
// failing on the whole payload (which silently dropped every copied vehicle with a custom loadout).
var createObjectMethod = typeof(ArmaMethods).GetMethod("CreateObject", BindingFlags.Public | BindingFlags.Static);
Assert(createObjectMethod is not null, "CreateObject should exist.");

var createArgs = (object?[])deserializeMethod!.Invoke(null, [createObjectMethod!, new string[] {
    "\"COPY1\"",
    "\"Object\"",
    "[[\"ItemClass\",\"B_soldier_F\"],[\"Init\",\"hint \"\"hello\"\";\"],[\"ammoBox\",\"[[[[\"\"arifle_MX_F\"\"],[1]]],false]\"]]"
}, null])!;

var metadata = createArgs[2] as Dictionary<string, object?>;
Assert(metadata is not null && metadata.Count == 3, $"The attribute map must survive values with quotes, got: {createArgs[2]?.GetType().Name}");
Assert((string?)metadata!["Init"] == "hint \"hello\";", $"Quotes inside a value must be unescaped, got: {metadata["Init"]}");
Assert((string?)metadata["ammoBox"] == "[[[[\"arifle_MX_F\"],[1]]],false]", $"ammoBox must round-trip, got: {metadata["ammoBox"]}");

// A removal batch arrives as [[id, type], ...] in the single argument of ArmaMethods.RemoveObjects.
var removeObjectsMethod = typeof(ArmaMethods).GetMethod("RemoveObjects", BindingFlags.Public | BindingFlags.Static);
Assert(removeObjectsMethod is not null, "RemoveObjects should exist.");

var removeArgs = (object?[])deserializeMethod!.Invoke(null, [removeObjectsMethod!, new string[] { "[[\"A1\",\"Object\"],[\"A2\",\"Marker\"]]" }, null])!;
object?[][]? removeBatch = removeArgs[0] as object?[][];
Assert(removeBatch is not null && removeBatch.Length == 2, $"RemoveObjects should receive [id, type] pairs, got: {removeArgs[0]}");
Assert((string?)removeBatch![0][0] == "A1" && (string?)removeBatch[1][1] == "Marker", "RemoveObjects pairs should keep id and type.");

ServerStateManager.ServerObjectManager.AddOrUpdateObject(new ArmaObject("A1") { Type = "Object" });
ServerStateManager.ServerObjectManager.AddOrUpdateObject(new ArmaObject("A2") { Type = "Marker" });
ServerNetworkMethods.RemoveObjects([new ArmaObject("A1"), new ArmaObject("A2")]);
Assert(!ServerStateManager.ServerObjectManager.Objects.ContainsKey("A1") && !ServerStateManager.ServerObjectManager.Objects.ContainsKey("A2"),
    "RemoveObjects should drop every entity of the batch from the server state.");

// Remote methods map one wire argument to one parameter, so the batch travels as ONE argument that
// is the ArmaObject[] (see ArmaMethods.RemoveObjects). The dispatcher must bind the whole array to
// the single ArmaObject[] parameter instead of treating every object as a separate argument.
DynTypeNetwork.MethodBuilder.RegisterServerMethods(new ServerNetworkMethods());

ServerStateManager.ServerObjectManager.AddOrUpdateObject(new ArmaObject("C1") { Type = "Object" });
ServerStateManager.ServerObjectManager.AddOrUpdateObject(new ArmaObject("C2") { Type = "Marker" });

var callServerMethod = typeof(DynTypeNetwork.MethodBuilder)
    .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
    .First(method => method.Name == "CallServerMethod" && method.IsGenericMethodDefinition);

callServerMethod.MakeGenericMethod(typeof(object)).Invoke(null,
[
    "RemoveObjects",
    new DynTypeNetwork.NetworkMessage(),
    new object?[] { new ArmaObject[] { new ArmaObject("C1") { Type = "Object" }, new ArmaObject("C2") { Type = "Marker" } } }
]);

Assert(!ServerStateManager.ServerObjectManager.Objects.ContainsKey("C1") && !ServerStateManager.ServerObjectManager.Objects.ContainsKey("C2"),
    "A batch sent as a single array argument should be bound to RemoveObjects(ArmaObject[]).");

// The batch reaches Arma as one ObjectRemoved message with [[id, type], ...] as its only argument.
string? removalMethod = null;
object?[]? removalData = null;

Events.OnSendToArma += (method, data) => {
    removalMethod = method;
    removalData = data;
};

ClientNetworkMethods.RemoveObjects([new ArmaObject("B1") { Type = "Object" }, new ArmaObject("B2") { Type = "Marker" }]);

var removalTimeout = DateTime.UtcNow.AddSeconds(5);
while (removalMethod is null && DateTime.UtcNow < removalTimeout)
    Thread.Sleep(10);

Assert(removalMethod == "ObjectRemoved", $"Removal should be forwarded as ObjectRemoved, got: {removalMethod}");
Assert(removalData is [object?[][] removalPairs] && removalPairs.Length == 2, "ObjectRemoved should carry one list of id/type pairs.");
Assert((string)printArrayMethod.Invoke(null, [removalData])! == "[[[\"B1\",\"Object\"],[\"B2\",\"Marker\"]]]",
    "ObjectRemoved should reach Arma as a single list argument.");

// Fire-and-forget requests (async key -1) must invoke async-only methods (e.g. Disconnect) without
// waiting for their result and without sending a response back to Arma.
MethodSystem.RegisterMethods(typeof(FireAndForgetProbeMethods));

var handleExecuteExtensionMethod = typeof(MethodSystem)
    .GetMethod("HandleExecuteExtensionMethod", BindingFlags.NonPublic | BindingFlags.Static);
Assert(handleExecuteExtensionMethod is not null, "HandleExecuteExtensionMethod should exist.");

(int ReturnCode, string Output) ExecuteExtensionMethod(string method)
{
    nint buffer = Marshal.AllocHGlobal(4096);
    try
    {
        int returnCode = (int)handleExecuteExtensionMethod!.Invoke(null, [buffer, 4096, method, Array.Empty<string>()])!;
        return (returnCode, Marshal.PtrToStringAnsi(buffer) ?? string.Empty);
    }
    finally
    {
        Marshal.FreeHGlobal(buffer);
    }
}

var (fireAndForgetCode, fireAndForgetOutput) = ExecuteExtensionMethod($"{nameof(FireAndForgetProbeMethods.FireAndForgetProbe)}|-1");
Assert(fireAndForgetCode == 0, $"Fire-and-forget request should succeed, got {fireAndForgetCode}: {fireAndForgetOutput}");
Assert(fireAndForgetOutput.StartsWith("[\"SUCCESS\""), $"Fire-and-forget request should not return the task result, got: {fireAndForgetOutput}");

ExecuteExtensionMethod($"{nameof(FireAndForgetProbeMethods.FireAndForgetProbe)}|-1");

var timeout = DateTime.UtcNow.AddSeconds(5);
while (FireAndForgetProbeMethods.CallCount < 2 && DateTime.UtcNow < timeout)
    Thread.Sleep(10);

Assert(FireAndForgetProbeMethods.CallCount == 2, $"Fire-and-forget methods should run, ran {FireAndForgetProbeMethods.CallCount} of 2.");

// Async methods that return a value still require an async key when the result is needed.
var (syncCode, syncOutput) = ExecuteExtensionMethod(nameof(FireAndForgetProbeMethods.FireAndForgetProbe));
Assert(syncCode == 1, $"Async-only methods must fail without an async key, got {syncCode}.");
Assert(syncOutput.Contains("can only be called using async key"), $"Unexpected error: {syncOutput}");

// DynTypeNetwork logs are split per side, so a listen server (client and server in one
// process) can tell them apart and can silence either side independently.
string networkLogDir = Path.Combine(Path.GetTempPath(), "EdenOnline_SmokeTests_NetworkLogs");
string clientLogPath = Path.Combine(networkLogDir, "DynTypeNetwork_Client.log");
string serverLogPath = Path.Combine(networkLogDir, "DynTypeNetwork_Server.log");

// The file logger holds its writer open for the process lifetime, so start from a clean folder.
try { Directory.Delete(networkLogDir, recursive: true); } catch { }

DynTypeNetwork.Log.ConfigureClient(EdenOnline.Logging.LogFactory.CreateFileLoggerFactory(networkLogDir, "DynTypeNetwork_Client.log"));
DynTypeNetwork.Log.ConfigureServer(EdenOnline.Logging.LogFactory.CreateFileLoggerFactory(networkLogDir, "DynTypeNetwork_Server.log"));

string ReadLog(string path)
{
    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
}

try
{
    DynTypeNetwork.Log.Client.Info("client-side entry");
    DynTypeNetwork.Log.Server.Info("server-side entry");

    string clientLog = ReadLog(clientLogPath);
    string serverLog = ReadLog(serverLogPath);

    Assert(clientLog.Contains("client-side entry") && !clientLog.Contains("server-side entry"), "Client log should only contain client-side entries.");
    Assert(serverLog.Contains("server-side entry") && !serverLog.Contains("client-side entry"), "Server log should only contain server-side entries.");

    DynTypeNetwork.Log.ClientEnabled = false;
    DynTypeNetwork.Log.Client.Info("client entry while disabled");
    DynTypeNetwork.Log.Server.Info("server entry while client disabled");

    Assert(!ReadLog(clientLogPath).Contains("client entry while disabled"), "Disabling client logging should stop client log writes.");
    Assert(ReadLog(serverLogPath).Contains("server entry while client disabled"), "Server logging should keep working while client logging is disabled.");
}
finally
{
    DynTypeNetwork.Log.ClientEnabled = true;
}

Console.WriteLine("Smoke tests passed.");

public static class FireAndForgetProbeMethods
{
    public static int CallCount;

    public static async Task<bool> FireAndForgetProbe()
    {
        await Task.Delay(50);
        Interlocked.Increment(ref CallCount);
        return true;
    }
}
