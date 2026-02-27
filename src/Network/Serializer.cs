
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using EdenOnline.Network;

namespace EdenOnline;


public static class NetworkSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver() // Native AOT-safe
    };

    // Serialize any object to UTF8 bytes
    public static byte[] SerializeToBytes<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);

    // Deserialize bytes dynamically into Dictionary<string, object?>
    public static Dictionary<string, object?> DeserializeToDictionary(byte[] data)
    {
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(data, Options)
            ?? new Dictionary<string, object?>();
    }

    // Pack message with ResponseId, ResponseMethod (enum as int), SenderId, TypeName, and data
    public static byte[] PackMessage(int responseId, MessageType responseMethod, int senderId, object? data = null)
    {
        string typeName;

        if (data != null)
        {
            Type dataType = data.GetType();
            if (dataType.IsGenericType && dataType.GetGenericTypeDefinition() == typeof(List<>))
            {
                // Send as "List<ServerObject>" instead of full CLR generic type
                typeName = $"List<{dataType.GetGenericArguments()[0].FullName}>";
            }
            else
            {
                typeName = dataType.FullName!;
            }
        }
        else
        {
            typeName = "System.Object";
        }

        object?[] payload = [
            responseId,
            (int)responseMethod, // enum as integer
            senderId,
            typeName,
            data
        ];

        byte[] payloadBytes = SerializeToBytes(payload);

        // Encrypt payload if encryption is enabled
        // TODO Fix UnpackMessage decrypt before enabling this feature!
        //payloadBytes = Encryption.EncryptPayload(payloadBytes);

        byte[] message = new byte[4 + payloadBytes.Length];

        BitConverter.GetBytes(payloadBytes.Length).CopyTo(message, 0);
        Buffer.BlockCopy(payloadBytes, 0, message, 4, payloadBytes.Length);

        return message;
    }

    // Unpack message dynamically
    public static (int ResponseId, MessageType ResponseMethod, int SenderId, Type IncomingDataType, object? Data) 
        UnpackMessage(byte[] buffer)
    {
        if (buffer.Length < 4) throw new ArgumentException("Invalid buffer length");

        int payloadLength = BitConverter.ToInt32(buffer, 0);
        if (payloadLength != buffer.Length - 4) throw new ArgumentException("Payload length mismatch");

        byte[] payloadBytes = buffer[4..];

        // TODO: If encryption is enabled, decrypt the payload before deserialization
        //payloadBytes = Encryption.DecryptPayload(payloadBytes, sharedSecret);

        object?[]? payload = JsonSerializer.Deserialize<object?[]>(payloadBytes, Options);
        if (payload == null || payload.Length != 5) throw new Exception("Invalid message format");

        int responseId = payload[0] switch
        {
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetInt32(),
            int i => i,
            _ => throw new InvalidCastException("Invalid responseId type")
        };

        MessageType responseMethod = payload[1] switch
        {
            JsonElement je when je.ValueKind == JsonValueKind.Number => (MessageType)je.GetInt32(),
            int i => (MessageType)i,
            _ => throw new InvalidCastException("Invalid responseMethod type")
        };

        int senderId = payload[2] switch
        {
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetInt32(),
            int i => i,
            _ => throw new InvalidCastException("Invalid senderId type")
        };

        // Resolve type dynamically from FullName or generic List<T> notation
        string typeName = payload[3]?.ToString() ?? "System.Object";
        Type incomingType = typeof(object);

        // Handle List<T> notation produced by PackMessage: e.g. "List<Namespace.TypeName>"
        if (typeName.StartsWith("List<") && typeName.EndsWith(">"))
        {
            string innerName = typeName[5..^1];
            Type? innerType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(innerName, false))
                .FirstOrDefault(t => t != null);

            if (innerType != null)
            {
                incomingType = typeof(List<>).MakeGenericType(innerType);
            }
            else
            {
                incomingType = typeof(object);
            }
        }
        else
        {
            incomingType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(typeName, false))
                .FirstOrDefault(t => t != null) ?? typeof(object);
        }

        object? data = payload[4];
        if (data is JsonElement jeData)
        {
            if (jeData.ValueKind == JsonValueKind.Object)
            {
                // If we could resolve a concrete incoming type, try to deserialize into it.
                if (incomingType != typeof(object) && !incomingType.IsGenericType)
                {
                    data = JsonSerializer.Deserialize(jeData.GetRawText(), incomingType, Options);
                }
                else
                {
                    data = ParseJsonElementToDictionary(jeData);
                }
            }
            else if (jeData.ValueKind == JsonValueKind.Array)
            {
                // If incoming type is a generic List<>, deserialize into that specific list type.
                if (incomingType.IsGenericType && incomingType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    data = JsonSerializer.Deserialize(jeData.GetRawText(), incomingType, Options);
                }
                else
                {
                    data = JsonSerializer.Deserialize<object?[]>(jeData.GetRawText(), Options);
                }
            }
            else
            {
                data = ParseJsonElement(jeData);
            }
        }

        return (responseId, responseMethod, senderId, incomingType, data);
    }

    // Manually parse JsonElement to Dictionary to avoid AOT issues
    private static Dictionary<string, object?> ParseJsonElementToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object?>();
        if (element.ValueKind != JsonValueKind.Object) return dict;

        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = ParseJsonElement(prop.Value);
        }

        return dict;
    }

    // Recursively parse JsonElement values
    private static object? ParseJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var i) ? i : (element.TryGetInt64(out var l) ? (object)l : element.GetDouble()),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ParseJsonElement).ToArray(),
            JsonValueKind.Object => ParseJsonElementToDictionary(element),
            _ => element.GetRawText()
        };
    }

    public static T? Reconstruct<T>(object? data) where T : class
    {
        if (data is null) return null;

        // Direct cast if already the right type
        if (data is T t) return t;

        // Dictionary -> JSON deserialization
        if (data is Dictionary<string, object?> dict)
        {
            var bytes = SerializeToBytes(dict);
            return JsonSerializer.Deserialize<T>(bytes, Options);
        }

        // Array -> List<U> deserialization
        if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(List<>) && data is object[] arr)
        {
            var bytes = SerializeToBytes(arr);
            return JsonSerializer.Deserialize<T>(bytes, Options);
        }

        return null;
    }


}