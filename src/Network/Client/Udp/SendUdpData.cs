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

    public static async Task SendUdpMessageAsync(int targetId, string methodName, params object?[] args)
    {
        if (_udpClient == null || _udpEndpoint == null) throw new InvalidOperationException("UDP client not connected.");
        if (targetId == ClientID) throw new InvalidOperationException("Cannot send UDP message to self.");


        // Validate method, and make sure it is registered in both server and client methods if sending to server (targetId == 0)
        if (targetId == 0)
        {
            var serverMethod = MethodBuilder.GetAvailableServerMethods().FirstOrDefault(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));
            var clientMethod = MethodBuilder.GetAvailableClientMethods().FirstOrDefault(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));

            if (serverMethod == null || clientMethod == null) throw new InvalidOperationException($"Method '{methodName}' must be registered in both server and client methods.");
        } else {
            var methods = targetId == Server.SERVER_ID
                ? MethodBuilder.GetAvailableServerMethods()
                : MethodBuilder.GetAvailableClientMethods();

            var method = methods.FirstOrDefault(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));

            if (method == null) {
                var scope = targetId == Server.SERVER_ID ? "server" : "client";

                throw new InvalidOperationException($"Method '{methodName}' not registered in {scope} methods.");
            }
        }
        

        // Make sure client is connected to server before sending message, when using specified target
        if (targetId > 1 && !Clients.Contains(targetId)) {
            throw new InvalidOperationException($"Cannot send UDP message to client {targetId} because it is not connected to the server.");
        }

        var payload = new MethodRequest { MethodName = methodName, Args = args };
        
        NetworkMessage msg = new()
        {
            SenderId = ClientID,
            TargetId = targetId,
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