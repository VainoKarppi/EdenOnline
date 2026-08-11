using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DynTypeSerializer;

namespace DynTypeNetwork;





public static partial class Client
{
    // ── Send messages ─────────────────────────
    // Does not return a response, use SendTcpMessageAsync for sending messages that require a response
    public static Task SendTcpMessageAsync(int targetId, string methodName, params object?[] args)
        => SendTcpMessageAsync([targetId], methodName, args);

    public static async Task SendTcpMessageAsync(int[] targetIds, string methodName, params object?[] args) {
        if (_tcpStream == null) throw new InvalidOperationException("TCP not initialized.");
        if (targetIds == null || targetIds.Length == 0)
            throw new ArgumentException("At least one target ID is required.", nameof(targetIds));

        int[] normalizedTargetIds = NetworkMessage.NormalizeTargetIds(targetIds);
        if (normalizedTargetIds.Contains(ClientID)) throw new InvalidOperationException("Cannot send TCP message to self.");

        NetworkMessage msg = new()
        {
            SenderId = ClientID,
            TargetId = normalizedTargetIds,
            MessageType = MessageType.Custom
        };

        if (normalizedTargetIds.Contains(0))
        {
            var serverMethod = MethodBuilder.GetAvailableServerMethods().FirstOrDefault(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));
            var clientMethod = MethodBuilder.GetAvailableClientMethods().FirstOrDefault(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));

            if (serverMethod == null || clientMethod == null) throw new InvalidOperationException($"Method '{methodName}' must be registered in both server and client methods.");
        }
        else if (normalizedTargetIds.Contains(Server.SERVER_ID))
        {
            var method = MethodBuilder.GetAvailableServerMethods().FirstOrDefault(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));
            if (method == null) throw new InvalidOperationException($"Method '{methodName}' not registered in server methods.");
        }
        else
        {
            var method = MethodBuilder.GetAvailableClientMethods().FirstOrDefault(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));
            if (method == null) throw new InvalidOperationException($"Method '{methodName}' not registered in client methods.");
        }

        foreach (var targetId in normalizedTargetIds.Where(t => t > 1 && t != Server.SERVER_ID))
        {
            if (!Clients.Contains(targetId))
                throw new InvalidOperationException($"Cannot send TCP message to client {targetId} because it is not connected to the server.");
        }

        var payload = new MethodRequest { MethodName = methodName, Args = args };
        var packet = MessageBuilder.CreatePacket(msg, payload);

        if (OnTcpMessageSent != null) {
            _ = Task.Run(() => OnTcpMessageSent.Invoke(msg));
        }

        await _tcpStream.WriteAsync(packet);
    }

    private static async Task SendMessageAsync(int targetId, MessageType type, object? data)
    {
        if (!IsTcpConnected()) throw new Exception("Not connected to server");

        NetworkMessage message = new()
        {
            SenderId = ClientID,
            TargetId = [targetId],
            MessageType = type
        };
        var packet = MessageBuilder.CreatePacket(message, data);

        await _tcpClient!.GetStream().WriteAsync(packet);
    }
}