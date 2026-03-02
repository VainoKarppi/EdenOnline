
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        // NativeAOT-safe
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),

        // Converters to handle dynamic dictionaries and lists
        Converters =
        {
            // Convert Dictionary<string, object?> recursively
            new DictionaryStringObjectConverter(),

            // Convert List<T> dynamically
            new ListDynamicConverterFactory()
        }
    };

    private const int CompressThreshold = 200;

    // Serialize any object to UTF8 bytes
    public static byte[] SerializeToBytes<T>(T value, bool compress = false)
    {
        byte[] utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(value, Options);

        if (!compress || utf8Bytes.Length < CompressThreshold) // optional threshold for compression
            return utf8Bytes;

        using var output = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(utf8Bytes, 0, utf8Bytes.Length);
        }

        byte[] compressed = output.ToArray();
        byte[] result = new byte[compressed.Length + 1];
        result[0] = 1; // 1 = compressed
        Array.Copy(compressed, 0, result, 1, compressed.Length);

        return result;
    }


    // Pack message with ResponseId, ResponseMethod (enum as int), SenderId, TypeName, and data
    public static byte[] PackMessage(int messageId, MessageType responseMethod, int senderId, object? data = null)
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
            messageId,
            (int)responseMethod, // enum as integer
            senderId,
            typeName,
            data
        ];

        byte[] payloadBytes = SerializeToBytes(payload, compress: false);

        // Encrypt payload if encryption is enabled
        // TODO Fix UnpackMessage decrypt before enabling this feature!
        //payloadBytes = Encryption.EncryptPayload(payloadBytes);

        byte[] message = new byte[4 + payloadBytes.Length];

        BitConverter.GetBytes(payloadBytes.Length).CopyTo(message, 0);
        Buffer.BlockCopy(payloadBytes, 0, message, 4, payloadBytes.Length);

        return message;
    }

    // Unpack message dynamically
    public static (int ResponseId, MessageType ResponseMethod, int SenderId, Type IncomingDataType, string? Data)
        UnpackMessage(byte[] buffer)
    {
        if (buffer.Length < 4) throw new ArgumentException("Invalid buffer length");

        int payloadLength = BitConverter.ToInt32(buffer, 0);
        if (payloadLength != buffer.Length - 4) throw new ArgumentException("Payload length mismatch");

        byte[] payloadBytes = buffer[4..];

        // Check compression flag
        if (payloadBytes.Length > 0 && payloadBytes[0] == 1)
        {
            // Compressed payload
            using var compressedStream = new MemoryStream(payloadBytes, 1, payloadBytes.Length - 1);
            using var gzip = new System.IO.Compression.GZipStream(compressedStream, System.IO.Compression.CompressionMode.Decompress);
            using var decompressed = new MemoryStream();
            gzip.CopyTo(decompressed);
            payloadBytes = decompressed.ToArray();
        }

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

        return (responseId, responseMethod, senderId, incomingType, data?.ToString());
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

    public static T? DeserializeData<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        return JsonSerializer.Deserialize<T>(json, Options);
    }

    
    public sealed class ListDynamicConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
            => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(List<>);

        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
        {
            Type itemType = type.GetGenericArguments()[0];
            Type converterType = typeof(ListDynamicConverter<>).MakeGenericType(itemType);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }

        private sealed class ListDynamicConverter<TItem> : JsonConverter<List<TItem>>
        {
            public override List<TItem> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                using var doc = JsonDocument.ParseValue(ref reader);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    throw new JsonException("Expected JSON array for List<T>");

                var list = new List<TItem>();
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var item = JsonSerializer.Deserialize<TItem>(element.GetRawText(), options)!;
                    list.Add(item);
                }

                return list;
            }

            public override void Write(Utf8JsonWriter writer, List<TItem> value, JsonSerializerOptions options)
            {
                writer.WriteStartArray();
                foreach (var item in value)
                    JsonSerializer.Serialize(writer, item, options);
                writer.WriteEndArray();
            }
        }
    }

    public sealed class DictionaryStringObjectConverter : JsonConverter<Dictionary<string, object?>>
    {
        public override Dictionary<string, object?> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            return ReadObject(doc.RootElement);
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<string, object?> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            foreach (var kvp in value)
            {
                writer.WritePropertyName(kvp.Key);
                JsonSerializer.Serialize(writer, kvp.Value, options);
            }

            writer.WriteEndObject();
        }

        private static Dictionary<string, object?> ReadObject(JsonElement element)
        {
            var dict = new Dictionary<string, object?>();

            foreach (var prop in element.EnumerateObject())
                dict[prop.Name] = ReadValue(prop.Value);

            return dict;
        }

        private static object? ReadValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Object => ReadObject(element),
                JsonValueKind.Array => ReadArray(element),
                _ => null
            };
        }

        private static List<object?> ReadArray(JsonElement element)
        {
            var list = new List<object?>();

            foreach (var item in element.EnumerateArray())
                list.Add(ReadValue(item));

            return list;
        }
    }

}