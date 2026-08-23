
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DynTypeSerializer;
using static DynTypeNetwork.Settings.Logging;

namespace DynTypeNetwork;

public class NetworkMessage
{
    public int SenderId { get; set; }
    public int[] TargetId { get; set; } = [];
    public MessageType MessageType { get; set; }

    public long Timestamp { get; set; } = DateTime.UtcNow.Ticks;

    public ushort MessageId { get; set; } = 0;

    /// <summary>
    /// Authentication token used to verify the identity of the sender for sensitive operations.
    /// </summary>
    public string? AuthenticationToken { get; set; }

    public string? Payload { get; set; }

    public static int[] NormalizeTargetIds(int[]? targetIds)
    {
        if (targetIds == null || targetIds.Length == 0)
            return [0];

        if (targetIds.Any(t => t == 0))
        {
            if (targetIds.Length > 1)
                throw new InvalidOperationException("Target ID 0 must be used alone.");
            return [0];
        }

        if (targetIds.Any(t => t > 0) && targetIds.Any(t => t < 0))
            throw new InvalidOperationException("Target IDs cannot mix positive and negative values.");

        return targetIds.Distinct().ToArray();
    }

    public bool TargetsEveryone => TargetId.Length == 0 || TargetId.Contains(0);

    public bool TargetsServer => TargetId.Contains(Server.SERVER_ID) || TargetId.Contains(0);

    public bool ShouldBroadcast => TargetsEveryone || TargetId.Any(t => t < 0);

    public bool IncludesTarget(int targetId)
    {
        int[] normalized = NormalizeTargetIds(TargetId);

        if (normalized.Contains(0))
            return true;

        if (normalized.Any(t => t < 0))
            return !normalized.Any(t => Math.Abs(t) == targetId);

        return normalized.Contains(targetId);
    }
}

internal sealed class MethodRequest
{
    public string? MethodName { get; init; }
    public object?[] Args { get; init; } = [];
}

internal sealed class MethodResponse<T>
{
    public bool Success { get; set; }
    public T? Result { get; set; }
}

internal sealed class RemoteMethodErrorPayload
{
    internal const string ProtocolPrefix = "DynTypeNetwork.RemoteMethodError.v1:";

    public string MethodName { get; set; } = "<unknown>";
    public string ExceptionType { get; set; } = nameof(Exception);
    public string Message { get; set; } = "Remote method failed.";
}

public enum MessageType
{
    ServerShutdown,
    Handshake,
    ClientConnected,
    ClientDisconnected,
    Request,
    Response,
    ResponseBroadcast,
    Custom,
    UdpRegister,
    AuthenticationReady,
    AuthenticationRequest,
    AuthenticationResponse
}

internal class HandshakeMessage
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string Hash { get; set; } = "";
    public int ClientId { get; set; }
    public List<int> OtherConnectedClients { get; set; } = [];
    public MethodBuilder.RpcMethodInfo[] AvailableMethods { get; set; } = [];
    public string? ClientPublicKey { get; set; }
    public string? ServerPublicKey { get; set; }
}

public static class MessageBuilder
{
    #region TCP
    internal static byte[] CreateTcpMessage(NetworkMessage msg, bool isServerBroadcast = false)
    {
        msg.Timestamp = DateTime.UtcNow.Ticks;

        byte[] payloadBytes = string.IsNullOrEmpty(msg.Payload) ? [] : Encoding.UTF8.GetBytes(msg.Payload);

        int payloadLength = payloadBytes.Length;

        msg.TargetId = NetworkMessage.NormalizeTargetIds(msg.TargetId);

        if (LogItem(LogLevel.Debug))
            Console.WriteLine($"{(msg.SenderId == Server.SERVER_ID ? "[SERVER]" : "[CLIENT]")} (CreateMessage) SenderId:{msg.SenderId}, TargetId:{string.Join(",", msg.TargetId)}, MessageId:{msg.MessageId}: {msg.Payload}");

        var buffer = new List<byte>();

        // --- HEADER ---
        buffer.AddRange(BitConverter.GetBytes(msg.SenderId));            // 4
        buffer.AddRange(BitConverter.GetBytes(msg.TargetId.Length));   // 4
        foreach (var targetId in msg.TargetId)
            buffer.AddRange(BitConverter.GetBytes(targetId));          // 4 each
        buffer.AddRange(BitConverter.GetBytes((ushort)msg.MessageType)); // 2
        buffer.AddRange(BitConverter.GetBytes(msg.MessageId));           // 2
        buffer.AddRange(BitConverter.GetBytes(msg.Timestamp));           // 8
        buffer.AddRange(BitConverter.GetBytes(payloadLength));           // 4

        // --- PAYLOAD ---
        if (payloadLength > 0) buffer.AddRange(payloadBytes);

        byte[] messageBytes = buffer.ToArray();

        // If called from server always use SERVER_ID instead of msg.SenderId to get the correct shared secret for encryption
        int senderId = isServerBroadcast ? Server.SERVER_ID : msg.SenderId;

        byte[] envelope = MessageCrypto.CreateTcpEnvelope(messageBytes, msg.MessageType, senderId, msg.TargetId);

        byte[] lengthPrefix = BitConverter.GetBytes(envelope.Length);
        return [.. lengthPrefix, ..envelope];
    }

    private static NetworkMessage ReadTcpMessage(byte[] data, bool includeData = false, int? connectionId = null)
    {
        byte[] messageBytes = MessageCrypto.DecodeTcpEnvelope(data, connectionId);
        return ParseTcpMessage(messageBytes, includeData);
    }

    private static NetworkMessage ParseTcpMessage(byte[] data, bool includeData = false)
    {
        if (data.Length < 4 + 4 + 2 + 2 + 8 + 4)
            throw new ArgumentException("Packet too short.", nameof(data));

        int offset = 0;

        int senderId = BitConverter.ToInt32(data, offset); offset += 4;
        int targetCount = BitConverter.ToInt32(data, offset); offset += 4;
        int[] targetIds = new int[targetCount];
        for (int i = 0; i < targetCount; i++)
        {
            targetIds[i] = BitConverter.ToInt32(data, offset);
            offset += 4;
        }
        MessageType messageType = (MessageType)BitConverter.ToInt16(data, offset); offset += 2;
        ushort messageId = BitConverter.ToUInt16(data, offset); offset += 2;
        long timestamp = BitConverter.ToInt64(data, offset); offset += 8;
        int payloadLength = BitConverter.ToInt32(data, offset); offset += 4;

        string? payload = null;

        if (includeData && payloadLength > 0)
        {
            payload = Encoding.UTF8.GetString(data, offset, payloadLength);
        }

        if (LogItem(LogLevel.Debug)) Console.WriteLine($"{(targetIds.Contains(Server.SERVER_ID) ? "[SERVER]" : "[CLIENT]")} (ReadMessage) MessageType:{messageType}, SenderId:{senderId}, TargetId:{string.Join(",", targetIds)}, MessageId:{messageId}: {payload}");

        return new NetworkMessage
        {
            SenderId = senderId,
            TargetId = targetIds,
            MessageType = messageType,
            MessageId = messageId,
            Timestamp = timestamp,
            Payload = payload
        };
    }
    internal static NetworkMessage? ReadTcpMessage(NetworkStream stream, int? connectionId = null) {
        // --- READ LENGTH PREFIX (4 bytes) ---
        byte[] lenBuf = new byte[4];
        stream.ReadExactly(lenBuf);
        int messageLength = BitConverter.ToInt32(lenBuf);

        // sanity check
        if (messageLength <= 0 || messageLength > 10_000_000)
            throw new InvalidDataException($"Invalid message length: {messageLength}");

        // --- READ FULL MESSAGE ---
        byte[] messageBytes = new byte[messageLength];
        stream.ReadExactly(messageBytes);

        // --- DESERIALIZE ---
        NetworkMessage msg = ReadTcpMessage(messageBytes, includeData: true, connectionId);

        return msg;
    }
    #endregion

    

    
    #region UDP
    private static readonly uint[] Crc32Table = CreateCrc32Table();

    internal static byte[] CreateUdpMessage(NetworkMessage msg, bool isServerBroadcast = false)
    {
        msg.Timestamp = DateTime.UtcNow.Ticks;

        byte[] payloadBytes = string.IsNullOrEmpty(msg.Payload) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(msg.Payload);
        int payloadLength = payloadBytes.Length;

        msg.TargetId = NetworkMessage.NormalizeTargetIds(msg.TargetId);

        if (LogItem(LogLevel.Debug)) Console.WriteLine($"{(msg.SenderId == Server.SERVER_ID ? "[SERVER]" : "[CLIENT]")} (CreateMessage) SenderId:{msg.SenderId}, TargetId:{string.Join(",", msg.TargetId)}, MessageId:{msg.MessageId}: {msg.Payload}");

        byte[] messageBytes = new byte[4 + 4 + 4 + (msg.TargetId.Length * 4) + 2 + 2 + 8 + 4 + payloadLength]; // checksum + sender + target-count + targets + header + payload
        int offset = 4; // leave space for checksum

        // --- HEADER ---
        Buffer.BlockCopy(BitConverter.GetBytes(msg.SenderId), 0, messageBytes, offset, 4); offset += 4;
        Buffer.BlockCopy(BitConverter.GetBytes(msg.TargetId.Length), 0, messageBytes, offset, 4); offset += 4;
        foreach (var targetId in msg.TargetId)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(targetId), 0, messageBytes, offset, 4); offset += 4;
        }
        Buffer.BlockCopy(BitConverter.GetBytes((ushort)msg.MessageType), 0, messageBytes, offset, 2); offset += 2;
        Buffer.BlockCopy(BitConverter.GetBytes(msg.MessageId), 0, messageBytes, offset, 2); offset += 2;
        Buffer.BlockCopy(BitConverter.GetBytes(msg.Timestamp), 0, messageBytes, offset, 8); offset += 8;
        Buffer.BlockCopy(BitConverter.GetBytes(payloadLength), 0, messageBytes, offset, 4); offset += 4;

        // --- PAYLOAD ---
        if (payloadLength > 0)
        {
            Buffer.BlockCopy(payloadBytes, 0, messageBytes, offset, payloadLength);
            offset += payloadLength;
        }

        // --- CHECKSUM ---
        uint checksum = CalculateChecksum(messageBytes, 4, offset - 4); // skip first 4 bytes
        Buffer.BlockCopy(BitConverter.GetBytes(checksum), 0, messageBytes, 0, 4);

        // If called from server always use SERVER_ID instead of msg.SenderId to get the correct shared secret for encryption
        int senderId = isServerBroadcast ? Server.SERVER_ID : msg.SenderId;

        return MessageCrypto.CreateUdpEnvelope(messageBytes, msg.MessageType, senderId, msg.TargetId);
    }

    internal static NetworkMessage ReadUdpMessage(byte[] data, bool includeData = false, int? senderId = null)
    {
        byte[] packet = MessageCrypto.DecodeUdpEnvelope(data, senderId);

        if (packet.Length < 4 + 4 + 4 + 2 + 2 + 8 + 4)
            throw new ArgumentException("Packet too short.", nameof(packet));

        int offset = 0;

        uint receivedChecksum = BitConverter.ToUInt32(packet, offset); offset += 4;
        uint calculatedChecksum = CalculateChecksum(packet, 4, packet.Length - 4);
        if (receivedChecksum != calculatedChecksum)
            throw new InvalidOperationException("Checksum mismatch.");

        int sender = BitConverter.ToInt32(packet, offset); offset += 4;
        int targetCount = BitConverter.ToInt32(packet, offset); offset += 4;
        int[] targetIds = new int[targetCount];
        for (int i = 0; i < targetCount; i++)
        {
            targetIds[i] = BitConverter.ToInt32(packet, offset);
            offset += 4;
        }
        MessageType messageType = (MessageType)BitConverter.ToInt16(packet, offset); offset += 2;
        ushort messageId = BitConverter.ToUInt16(packet, offset); offset += 2;
        long timestamp = BitConverter.ToInt64(packet, offset); offset += 8;
        int payloadLength = BitConverter.ToInt32(packet, offset); offset += 4;

        string? payload = null;
        if (includeData && payloadLength > 0)
            payload = Encoding.UTF8.GetString(packet, offset, payloadLength);

        if (LogItem(LogLevel.Debug)) Console.WriteLine($"{(targetIds.Contains(Server.SERVER_ID) ? "[SERVER]" : "[CLIENT]")} (ReadMessage) SenderId:{sender}, TargetId:{string.Join(",", targetIds)}, MessageId:{messageId}: {payload}");

        return new NetworkMessage
        {
            SenderId = sender,
            TargetId = targetIds,
            MessageType = messageType,
            MessageId = messageId,
            Timestamp = timestamp,
            Payload = payload
        };
    }

    // --- CRC32 CHECKSUM ---
    private static uint CalculateChecksum(byte[] data, int offset, int length)
    {
        uint crc = 0xFFFFFFFF;
        for (int i = offset; i < offset + length; i++)
            crc = (crc >> 8) ^ Crc32Table[(crc ^ data[i]) & 0xFF];
        return ~crc;
    }

    private static uint[] CreateCrc32Table()
    {
        uint[] table = new uint[256];
        const uint poly = 0xEDB88320;
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ poly : crc >> 1;
            table[i] = crc;
        }
        return table;
    }

    #endregion

    #region Helper
    internal static T? UnpackPayload<T>(string? data)
    {
        if (string.IsNullOrEmpty(data)) return default;
        return Serializer.Deserialize<T>(data);
    }

    internal static string CreateRemoteErrorPayload(string methodName, Exception exception)
    {
        Exception remoteException = exception.GetBaseException();
        return RemoteMethodErrorPayload.ProtocolPrefix
            + Serializer.Serialize(new RemoteMethodErrorPayload
            {
                MethodName = string.IsNullOrWhiteSpace(methodName) ? "<unknown>" : methodName,
                ExceptionType = remoteException.GetType().FullName ?? remoteException.GetType().Name,
                Message = remoteException.Message
            });
    }

    internal static T? UnpackResponsePayload<T>(string? data, int targetId, string methodName)
    {
        if (TryUnpackRemoteError(data, out RemoteMethodErrorPayload? error) && error is not null)
        {
            string remoteMethodName = string.IsNullOrWhiteSpace(error.MethodName)
                ? methodName
                : error.MethodName;
            throw new RemoteMethodException(targetId, remoteMethodName, error.Message, error.ExceptionType);
        }

        return UnpackPayload<T>(data);
    }

    internal static string GetRequestMethodName(object? payload, MessageType fallbackType)
    {
        return payload is MethodRequest methodRequest
            ? methodRequest.MethodName ?? fallbackType.ToString()
            : fallbackType.ToString();
    }

    private static bool TryUnpackRemoteError(string? data, out RemoteMethodErrorPayload? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(data)
            || !data.StartsWith(RemoteMethodErrorPayload.ProtocolPrefix, StringComparison.Ordinal))
            return false;

        error = Serializer.Deserialize<RemoteMethodErrorPayload>(data[RemoteMethodErrorPayload.ProtocolPrefix.Length..]);
        return error is not null;
    }

    internal static byte[] CreatePacket(NetworkMessage msg)
    {
        return CreateTcpMessage(msg);
    }
    internal static byte[] CreatePacket<T>(NetworkMessage msg, T data)
    {
        msg.Payload = Serializer.Serialize(data);
        return CreateTcpMessage(msg);
    }

    internal static async Task<object?> HandleBroadcastMessageOnServer(NetworkMessage msg, CancellationToken token)
    {
        object? result = null;
        try {
            MethodRequest? request = UnpackPayload<MethodRequest>(msg.Payload);
            if (request == null) throw new Exception("Unable to UnpackPayload"); // SHOULD NEVER HAPEN??

            result = MethodBuilder.CallServerMethod<object>(request.MethodName!, msg, request.Args!);

            bool isVoidMethod = MethodBuilder.GetAvailableServerMethods().FirstOrDefault(m => m.Name.Equals(request.MethodName, StringComparison.OrdinalIgnoreCase))?.ReturnType == null;

            if (result == null && !isVoidMethod) throw new Exception("Result was expected, but not returned");
        } catch (Exception ex) {
            if (LogItem(LogLevel.Debug)) Console.WriteLine(ex);
        }

        return result;
    }

    internal static async Task HandleCustomMessage(NetworkStream stream, NetworkMessage msg, CancellationToken token)
    {
        object? result = null;
        string methodName = "<unknown>";
        Exception? failure = null;
        try {
            MethodRequest? request = UnpackPayload<MethodRequest>(msg.Payload);
            if (request == null) throw new Exception("Unable to UnpackPayload"); // SHOULD NEVER HAPEN??
            methodName = request.MethodName ?? throw new InvalidOperationException("Method name was not provided.");

            if (msg.TargetsServer) {
                if (msg.MessageId > 0) {
                    // Is request (send response back to client)
                    result = MethodBuilder.CallServerMethod<object>(methodName, msg, request.Args!);
                } else {
                    // Is fire and forget
                    _ = Task.Run(() => MethodBuilder.CallServerMethod<object>(methodName, msg, request.Args!), token);
                    return; // Dont send response
                }
            } else {
                if (msg.MessageId > 0) {
                    // Is request (send response back to client)
                    result = MethodBuilder.CallClientMethod<object>(methodName, msg, request.Args!);
                } else {
                    // Is fire and forget
                    // Already validated on sender (Synced Method lists)
                    _ = Task.Run(() => MethodBuilder.CallClientMethod<object>(methodName, msg, request.Args!), token);
                    return; // Dont send response
                }
            }

            MethodBuilder.RpcMethodInfo[] availableMethods = msg.TargetsServer
                ? MethodBuilder.GetAvailableServerMethods()
                : MethodBuilder.GetAvailableClientMethods();
            bool isVoidMethod = availableMethods
                .FirstOrDefault(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
                ?.ReturnType == null;
            if (result == null && !isVoidMethod) throw new Exception("Result was expected, but not returned");
        } catch (Exception ex) {
            failure = ex.GetBaseException();
            if (LogItem(LogLevel.Error))
                Console.WriteLine($"[NETWORK] Remote method '{methodName}' failed: {failure}");
        }


        NetworkMessage responseMessage = new()
        {
            SenderId = msg.TargetsServer ? Server.SERVER_ID : msg.SenderId,
            TargetId = [msg.SenderId],
            MessageId = msg.MessageId,
            MessageType = MessageType.Response,
        };

        
        // *Dynamically create MethodResponse<T>

        /*
        object responseWrapper;
        Type responseType = result?.GetType() ?? typeof(object);
        Type wrapperType = typeof(MethodResponse<>).MakeGenericType(responseType);
        responseWrapper = Activator.CreateInstance(wrapperType)!;

        wrapperType.GetProperty("Success")!.SetValue(responseWrapper, success);
        wrapperType.GetProperty("Result")!.SetValue(responseWrapper, result);
        if (!success) wrapperType.GetProperty("ErrorMessage")!.SetValue(responseWrapper, result?.ToString());

        if (LogItem(LogLevel.Debug)) Console.WriteLine($"{(msg.SenderId == Server.SERVER_ID ? "[SERVER]" : "[CLIENT]")} Sending response for method: SUCCESS:{success}, ({responseType}):{Serializer.Serialize(result)}");

        byte[] packet = CreateMessage(responseMessage, responseWrapper);
        await stream.WriteAsync(packet, token);
        */

        byte[] packet;
        if (failure is null) {
            if (LogItem(LogLevel.Debug)) Console.WriteLine($"{(responseMessage.SenderId == Server.SERVER_ID ? "[SERVER]" : "[CLIENT]")} Sending response for method: SUCCESS, ({(result == null ? "null" : result.GetType().Name)}):{Serializer.Serialize(result)}");
            packet = CreatePacket(responseMessage, result);
        } else {
            responseMessage.Payload = CreateRemoteErrorPayload(methodName, failure);
            packet = CreatePacket(responseMessage);
        }

        await stream.WriteAsync(packet, token);
    }
    internal static ushort GenerateRequestId(ref int requestId)
    {
        while (true)
        {
            int current = requestId;
            int next = current >= ushort.MaxValue ? 1 : current + 1;

            if (Interlocked.CompareExchange(ref requestId, next, current) == current)
                return (ushort)next;
        }
    }
    #endregion


}
