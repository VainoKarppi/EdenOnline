using System.Reflection;
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

var serializerType = typeof(ArmaMethods).Assembly.GetType("ArmaExtension.Serializer");
Assert(serializerType is not null, "Serializer type should exist.");

var deserializeMethod = serializerType?.GetMethod("DeserializeArmaArray", BindingFlags.NonPublic | BindingFlags.Static);
Assert(deserializeMethod is not null, "DeserializeArmaArray should exist.");

var values = (object?[])deserializeMethod!.Invoke(null, [setMissionAttributeMethod!, new string[] { "\"Briefing\"", "\"Scenario\"", "[1,2,3]" }, null])!;
Assert(values[2] is object[] array && array.Length == 3, "Array payloads should deserialize as object[] for object parameters.");

Console.WriteLine("Smoke tests passed.");
