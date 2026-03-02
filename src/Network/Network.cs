using System;
using System.Collections.Concurrent;
using System.Net.Sockets;

using static ArmaExtension.Logger;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace EdenOnline.Network;

// ============================================================================
// MESSAGE TYPES & ENUMS
// ============================================================================

public enum MessageType : byte
{
    Handshake,
    HandshakeTimeout,
    ServerShutdown,
    ObjectSync,
    ObjectCreate,
    ObjectRemove,
    ObjectUpdate,
    ClientDisconnect,
    Ping,
    Custom
}


// ============================================================================
// MESSAGE CLASSES
// ============================================================================

public class NetworkMessage
{
    public int MessageId { get; set; } = -1;
    public MessageType MessageType { get; set; }
    public int SenderId { get; set; } = -1;
    public Type DataType { get; set; } = typeof(object);
    public string? Data { get; set; }
}

public class HandshakeMessage
{
    public string Status { get; set; } = "";
    public string Username { get; set; } = "";
    public string World { get; set; } = "";
    public string Hash { get; set; } = "";
    public int ClientId { get; set; }
    public string[] OtherClients { get; set; } = []; // Todo return IDs and Names instead of just names
}


// ============================================================================
// NETWORK HELPER
// ============================================================================

public class Connection : TcpClient
{
    public bool IsServer { get; set; } = false;
    public int Id { get; set; } = -1;
    public string Username { get; set; } = "Unknown";
    public string Hash { get; set; } = "";
    public bool HandshakeDone { get; set; } = false;
}

public static class NetworkHelper
{

    /// <summary>
    /// List of pending requests waiting for response, keyed by requestId. The value is the callback to invoke when response is received.
    /// </summary>
    /// <returns></returns>
    public static readonly List<int> Requests = [];
    public static readonly ConcurrentDictionary<int, string?> Responses = new();
    private static int _requestId;
    public static int GenerateRequestId() => Interlocked.Increment(ref _requestId);

    /// <summary>
    /// Send a network message over TCP
    /// </summary>
    public static void SendMessage(Connection client, MessageType messageType, int senderId, object? data = null, int responseId = -1)
    {
        if (client == null || !client.Connected) return;
        try
        {
            byte[] messageBytes = NetworkSerializer.PackMessage(responseId, messageType, senderId, data);
            client.GetStream().Write(messageBytes, 0, messageBytes.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NetworkHelper] SendMessage exception: {ex}");
        }
    }

    public static void SendResponseMessage(Connection client, MessageType messageType, int senderId, int messageId = -1, object? data = null)
    {
        if (client == null || !client.Connected) return;
        try
        {
            byte[] messageBytes = NetworkSerializer.PackMessage(messageId, messageType, senderId, data);
            client.GetStream().Write(messageBytes, 0, messageBytes.Length);
            Console.WriteLine($"Sent network message: msgId:{messageId}, msgType:{messageType}, sender:{senderId}, data:{data}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NetworkHelper] SendResponseMessage exception: {ex}");
        }
    }

    /// <summary>
    /// Send a simple message without data
    /// </summary>
    public static void SendMessage(Connection client, int messageId, MessageType messageType, int senderId)
    {
        if (client == null || !client.Connected) return;
        try
        {
            byte[] messageBytes = NetworkSerializer.PackMessage(messageId, messageType, senderId);
            client.GetStream().Write(messageBytes, 0, messageBytes.Length);
            Console.WriteLine($"Sent network message: msgId:{messageId}, msgType:{messageType}, sender:{senderId}, data:{null}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NetworkHelper] SendMessage exception: {ex}");
        }
    }

    /// <summary>
    /// Read a complete message from TCP
    /// </summary>
    public static NetworkMessage? ReadMessage(Connection client)
    {
        if (client == null || !client.Connected) return null;
        try
        {
            var stream = client.GetStream();

            // Read 4-byte length prefix
            byte[] lenBuf = new byte[4];
            stream.ReadExactly(lenBuf);
            int payloadLen = BitConverter.ToInt32(lenBuf);

            // Read payload
            byte[] payload = new byte[payloadLen];
            stream.ReadExactly(payload);

            // Reconstruct full message
            byte[] fullMessage = new byte[4 + payloadLen];
            Buffer.BlockCopy(lenBuf, 0, fullMessage, 0, 4);
            Buffer.BlockCopy(payload, 0, fullMessage, 4, payloadLen);

            // Unpack using NetworkSerializer
            var (msgId, msgType, senderId, dataType, data) = NetworkSerializer.UnpackMessage(fullMessage);

            Console.WriteLine($"Received network message: msgId:{msgId}, msgType:{msgType}, sender:{senderId}, type:{dataType}, data:{data}");

            return new NetworkMessage
            {
                MessageId = msgId,
                MessageType = msgType,
                SenderId = senderId,
                DataType = dataType,
                Data = data
            };
        }
        catch (Exception ex)
        {
            if (client == null || !client.Connected) return null;
            Console.WriteLine($"[NetworkHelper] ReadMessage exception: {ex}");
            return null;
        }
    }



    public static async Task<T?> SendRequestAsync<T>(Connection client, MessageType type, object? data, int timeoutMs = 10000)
    {
        if (client == null || !client.Connected)
            throw new Exception("Client not connected!");

        int requestId = GenerateRequestId();

        // Register request
        Requests.Add(requestId);

        // TODO fix sender id when server
        byte[] message = NetworkSerializer.PackMessage(requestId, type, client.Id, data);

        try
        {
            Debug($"[NetworkHelper] Sent Request: type={type}, requestId={requestId}, clientId={client.Id}");
            await client.GetStream().WriteAsync(message);
        }
        catch (Exception ex)
        {
            Requests.Remove(requestId);
            throw new Exception($"Failed to send request: {ex}");
        }

        // Wait asynchronously for the response
        try {
            string? receivedData = await WaitWithTimeout(requestId, timeoutMs);
            if (receivedData == null) return default;

            T? deserialized = NetworkSerializer.DeserializeData<T>(receivedData);

            return deserialized;
        } finally {
            // Cleanup regardless of success or failure
            Requests.Remove(requestId);
            Responses.TryRemove(requestId, out _);
        }
    }
    public static T? SendRequest<T>(Connection client, MessageType type, object? data, int timeoutMs = 10000) {
        try {
            var response = SendRequestAsync<T>(client, type, data, timeoutMs)
                .GetAwaiter()
                .GetResult();

            return response;
        }
        catch (Exception ex) {
            Error($"SendRequest sync error: {ex}");
            throw;
        }
    }

    private static async Task<string?> WaitWithTimeout(int requestId, int timeoutMs)
    {
        try
        {
            Console.WriteLine($"Waiting for response to request {requestId}");

            using var cts = new CancellationTokenSource(timeoutMs);

            // Polling loop, but asynchronously
            string? response;
            while (!Responses.TryGetValue(requestId, out response))
            {
                if (cts.Token.IsCancellationRequested)
                    throw new TimeoutException($"Request {requestId} timed out after {timeoutMs} ms");

                await Task.Delay(1, cts.Token); // async wait
            }

            Requests.Remove(requestId);
            Responses.TryRemove(requestId, out _);

            return response;
        }
        catch (TimeoutException)
        {
            Console.WriteLine($"[TIMEOUT] Request {requestId} timed out after {timeoutMs} ms");
            Requests.Remove(requestId);
            Responses.TryRemove(requestId, out _);
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Request {requestId} failed: {ex}");
            Requests.Remove(requestId);
            Responses.TryRemove(requestId, out _);
            throw;
        }
    }
}
