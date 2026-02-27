using System;
using System.Collections.Concurrent;
using System.Net.Sockets;

using static ArmaExtension.Logger;
using System.Threading;

namespace EdenOnline.Network;

// ============================================================================
// MESSAGE TYPES & ENUMS
// ============================================================================

public enum MessageType : byte
{
    ClientHandshake = 0,
    ServerHandshake = 1,
    ServerHandshakeComplete = 2,
    ServerShutdown = 3,
    ObjectSync = 4,
    ObjectUpdate = 5,
    ClientDisconnect = 6,
    Ping = 7,
    Custom = 254
}

public enum Target : byte
{
    Everyone = 0,
    Server = 1,
    Self = 2,
    Others = 3
}

// ============================================================================
// MESSAGE CLASSES
// ============================================================================

public class NetworkMessage
{
    public int ResponseId { get; set; } = -1;
    public bool IsRequest { get; set; } = false; // Used to identify if this message expects a response
    public MessageType MessageType { get; set; }
    public int SenderId { get; set; } = -1;
    public Type DataType { get; set; } = typeof(object);
    public object? Data { get; set; }
}

public class HandshakeMessage
{
    public string Status { get; set; } = "";
    public string Username { get; set; } = "";
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
}

public static class NetworkHelper
{

    /// <summary>
    /// List of pending requests waiting for response, keyed by requestId. The value is the callback to invoke when response is received.
    /// </summary>
    /// <returns></returns>
    private static readonly ConcurrentDictionary<int, Action<NetworkMessage>> PendingRequests = new();
    private static int _nextRequestId = 0;

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
            Debug($"[NetworkHelper] Sent message: type={messageType}, responseId={responseId}, size={messageBytes.Length}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NetworkHelper] SendMessage exception: {ex}");
        }
    }

    public static void SendResponseMessage(Connection client, MessageType messageType, int senderId, object? data = null, int requestId = -1)
    {
        if (client == null || !client.Connected) return;
        try
        {
            byte[] messageBytes = NetworkSerializer.PackMessage(requestId, messageType, senderId, data);
            client.GetStream().Write(messageBytes, 0, messageBytes.Length);
            Debug($"[NetworkHelper] Sent response message: type={messageType}, responseId={requestId}, size={messageBytes.Length}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NetworkHelper] SendResponseMessage exception: {ex}");
        }
    }

    /// <summary>
    /// Send a simple message without data
    /// </summary>
    public static void SendMessage(Connection client, int responseId, MessageType messageType, int senderId)
    {
        if (client == null || !client.Connected) return;
        try
        {
            byte[] messageBytes = NetworkSerializer.PackMessage(responseId, messageType, senderId);
            client.GetStream().Write(messageBytes, 0, messageBytes.Length);
            Debug($"[NetworkHelper] Sent message: type={messageType}, responseId={responseId}, size={messageBytes.Length}");
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
            var (respId, msgType, senderId, dataType, data) = NetworkSerializer.UnpackMessage(fullMessage);

            return new NetworkMessage
            {
                ResponseId = respId,
                MessageType = msgType,
                SenderId = senderId,
                DataType = dataType,
                Data = data
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NetworkHelper] ReadMessage exception: {ex}");
            return null;
        }
    }


    /// <summary>
    /// Send a typed request with automatic response conversion
    /// </summary>
    public static int SendRequest<TResponse>(Connection client, MessageType type, object? data, Action<TResponse?> callback)
    {
        if (client == null || !client.Connected) return -1;

        int requestId = Interlocked.Increment(ref _nextRequestId);

        // TODO Get senderId, and if called from server set as 1
        byte[] message = NetworkSerializer.PackMessage(requestId, type, 0, data);

        // store callback BEFORE sending
        PendingRequests[requestId] = msg =>
        {
            try
            {
                callback?.Invoke((TResponse?)msg.Data);
            }
            catch (Exception ex)
            {
                Error($"Callback error: {ex}");
            }
        };

        try
        {
            client.GetStream().Write(message, 0, message.Length);
        }
        catch (Exception ex)
        {
            Error($"Failed to send request: {ex}");
            PendingRequests.TryRemove(requestId, out _);
            return -1;
        }

        return requestId;
    }
}
