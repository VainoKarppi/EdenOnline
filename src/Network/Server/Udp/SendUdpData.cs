using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DynTypeSerializer;
using static DynTypeNetwork.Settings.Logging;


namespace DynTypeNetwork;





public static partial class Server
{

    public static async Task SendUdpMessageAsync(int targetId, string methodName, params object?[] args)
    {
        if (!IsUdpServerRunning()) throw new InvalidOperationException("UDP server not running.");
        if (targetId == SERVER_ID) throw new InvalidOperationException("Cannot send UDP message to server itself.");

        // Validate method once
        var methodExists = MethodBuilder.GetAvailableClientMethods()
            .Any(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));

        if (!methodExists) throw new InvalidOperationException($"Method '{methodName}' is not registered in client methods.");

        var payload = new MethodRequest { MethodName = methodName, Args = args };

        async Task SendToClient(Connection client) {
            try {
                if (client?.UdpEndpoint == null)
                    throw new InvalidOperationException($"Client {client?.Id} has no registered UDP endpoint.");

                var msg = new NetworkMessage
                {
                    SenderId = targetId,
                    TargetId = [client.Id],
                    MessageType = MessageType.Custom,
                    Payload = Serializer.Serialize(payload)
                };

                OnUdpMessageSent?.Invoke(msg);

                var packet = MessageBuilder.CreateUdpMessage(msg);

                await _udpListener!.SendAsync(packet, packet.Length, client.UdpEndpoint);
            } catch (Exception ex) {
                if (LogItem(LogLevel.Info)) Console.WriteLine($"[SERVER UDP] Failed to send to client {client.Id}: {ex.Message}");
            }
        }

        // Make sure client is connected to server before sending message, when using specified target
        if (targetId > 1) {
            if (!Clients.TryGetValue(targetId, out var client) || client == null) throw new Exception($"Client not found with ID: {targetId}");

            await SendToClient(client);
            return;
        }


        List<Task> tasks = [];
        // Broadcast
        foreach (var client in Clients.Values) {
            if (client?.UdpEndpoint == null) continue;

            tasks.Add(SendToClient(client));
        }

        await Task.WhenAll(tasks);
    }
}