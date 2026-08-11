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

    public static Task SendUdpMessageAsync(int targetId, string methodName, params object?[] args)
        => SendUdpMessageAsync([targetId], methodName, args);

    public static async Task SendUdpMessageAsync(int[] targetIds, string methodName, params object?[] args)
    {
        if (_udpClient == null || _udpEndpoint == null) throw new InvalidOperationException("UDP client not connected.");
        if (targetIds == null || targetIds.Length == 0)
            throw new ArgumentException("At least one target ID is required.", nameof(targetIds));

        int[] normalizedTargetIds = NetworkMessage.NormalizeTargetIds(targetIds);
        if (normalizedTargetIds.Contains(ClientID)) throw new InvalidOperationException("Cannot send UDP message to self.");

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
                throw new InvalidOperationException($"Cannot send UDP message to client {targetId} because it is not connected to the server.");
        }

        var payload = new MethodRequest { MethodName = methodName, Args = args };

        NetworkMessage msg = new()
        {
            SenderId = ClientID,
            TargetId = normalizedTargetIds,
            MessageType = MessageType.Custom,
            Payload = DynTypeSerializer.Serializer.Serialize(payload)
        };

        var packet = MessageBuilder.CreateUdpMessage(msg);

        if (OnUdpMessageSent != null) {
            _ = Task.Run(() => OnUdpMessageSent.Invoke(msg));
        }

        await _udpClient.SendAsync(packet, packet.Length, _udpEndpoint);
    }
}