using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DynTypeSerializer;
using static DynTypeNetwork.Settings.Logging;

namespace DynTypeNetwork;

public static partial class Server
{
    private static UdpClient? _udpListener;

    public static bool IsUdpServerRunning() =>
        _udpListener != null && _udpListener.Client.IsBound;

    private static void StartUdp(int port)
    {
        _udpListener = new UdpClient(port);
        StartUdpServerReceiveLoop(port);
        if (LogItem(LogLevel.Info)) Console.WriteLine("[SERVER] UDP Server started");
    }

    private static void StartUdpServerReceiveLoop(int port)
    {
        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    if (_udpListener == null || !_udpListener.Client.IsBound)
                    {
                        try { _udpListener?.Dispose(); } catch { }
                        _udpListener = new UdpClient(port);
                    }

                    var result = await _udpListener.ReceiveAsync(_cts.Token);

                    int? senderId = Clients.Values.FirstOrDefault(c => c.UdpEndpoint != null && c.UdpEndpoint.Equals(result.RemoteEndPoint))?.Id;
                    NetworkMessage msg = MessageBuilder.ReadUdpMessage(result.Buffer, includeData: true, senderId: senderId);

                    if (!Clients.TryGetValue(msg.SenderId, out var senderClient)) continue;

                    if (msg.MessageType == MessageType.UdpRegister)
                    {
                        if (senderClient.UdpEndpoint == null || !senderClient.UdpEndpoint.Equals(result.RemoteEndPoint))
                        {
                            senderClient.UdpEndpoint = result.RemoteEndPoint;
                        }
                        continue;
                    }

                    if (msg.MessageType != MessageType.Custom) continue;

                    if (msg.TargetsServer || msg.TargetsEveryone)
                    {
                        _ = Task.Run(() => OnUdpMessageReceived?.Invoke(msg));

                        _ = Task.Run(() =>
                        {
                            try
                            {
                                MethodRequest? request = MessageBuilder.UnpackPayload<MethodRequest>(msg.Payload);
                                if (request == null) throw new Exception("Unable to unpack payload");

                                MethodBuilder.CallServerMethod<object>(request.MethodName!, msg, request.Args!);
                            }
                            catch (Exception ex)
                            {
                                if (LogItem(LogLevel.Info)) Console.WriteLine($"[SERVER UDP] Method execution failed: {ex}");
                            }
                        }, _cts.Token);
                    }

                    if (msg.TargetId.Any(t => t > 0))
                    {
                        foreach (var targetId in msg.TargetId.Where(t => t > 0))
                        {
                            if (!Clients.TryGetValue(targetId, out var targetClient) || targetClient.UdpEndpoint == null)
                            {
                                if (LogItem(LogLevel.Info)) Console.WriteLine($"[SERVER UDP] Cannot forward, target {targetId} not available.");
                                continue;
                            }

                            var forwardedMsg = new NetworkMessage
                            {
                                SenderId = msg.SenderId,
                                TargetId = [targetId],
                                MessageType = msg.MessageType,
                                MessageId = msg.MessageId,
                                Payload = msg.Payload
                            };

                            var forwardedPacket = MessageBuilder.CreateUdpMessage(forwardedMsg);
                            await _udpListener.SendAsync(forwardedPacket.AsMemory(), targetClient.UdpEndpoint, _cts.Token);
                        }
                        continue;
                    }

                    if (msg.TargetId.Any(t => t < 0) || msg.TargetsEveryone)
                        await BroadcastUdp(senderClient, msg);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (LogItem(LogLevel.Info)) Console.WriteLine($"[SERVER UDP] Receive loop error: {ex.Message}");
                }
            }

            if (LogItem(LogLevel.Info)) Console.WriteLine("[SERVER UDP] Receive loop stopped.");
        }, _cts.Token);
    }

    private static async Task BroadcastUdp(Connection sender, NetworkMessage message)
    {
        _cts ??= new CancellationTokenSource();

        foreach (var client in Clients.Values)
        {
            if (!client.Connected || client.Id == sender.Id || client.UdpEndpoint == null) continue;

            if (message.TargetId.Any(t => t < 0) && message.TargetId.Any(t => Math.Abs(t) == client.Id)) continue;

            NetworkMessage udpMsg = new()
            {
                SenderId = message.SenderId,
                TargetId = [client.Id],
                MessageType = MessageType.Custom,
                Payload = message.Payload
            };
            var packet = MessageBuilder.CreateUdpMessage(udpMsg, isServerBroadcast: true);

            await _udpListener!.SendAsync(packet.AsMemory(), client.UdpEndpoint, _cts.Token);
        }
    }
}