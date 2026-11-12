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
