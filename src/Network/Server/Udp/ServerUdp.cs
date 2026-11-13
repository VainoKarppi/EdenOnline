using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DynTypeSerializer;
using Microsoft.Extensions.Logging;

namespace DynTypeNetwork;

public static partial class Server
{
    private static UdpClient? _udpListener;

    public static bool IsUdpServerRunning()
    {
        var listener = _udpListener;
        if (listener == null) return false;

        try
        {
            var socket = listener.Client;
            return socket != null && socket.IsBound;
        }
        catch (ObjectDisposedException) { return false; }
        catch (SocketException) { return false; }
    }

    private static void StartUdp(int port)
    {
        _udpListener = new UdpClient(port);
        StartUdpServerReceiveLoop(port);
        Log.Server.Info("[SERVER] UDP Server started");
    }

    /// <summary>
    /// Returns a usable listener, rebuilding one that was closed or never bound. A disposed
    /// socket cannot be inspected without throwing, so without this the receive loop would
    /// spin on a dead socket forever and the server would silently stop receiving UDP.
    /// </summary>
    private static UdpClient? EnsureUdpListener(int port)
    {
        var listener = _udpListener;

        if (listener != null)
        {
            try
            {
                var socket = listener.Client;
                if (socket != null && socket.IsBound) return listener;
            }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
        }

        try { listener?.Dispose(); } catch { }

        try
        {
            _udpListener = new UdpClient(port);
            Log.Server.Warning("[SERVER UDP] UDP listener recreated.");
            return _udpListener;
        }
        catch (Exception ex)
        {
            Log.Server.Error($"[SERVER UDP] Failed to recreate UDP listener: {ex.Message}");
            _udpListener = null;
            return null;
        }
    }

    private static void StartUdpServerReceiveLoop(int port)
    {
        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var listener = EnsureUdpListener(port);
                    if (listener == null)
                    {
                        await Task.Delay(1000, _cts.Token);
                        continue;
                    }

                    var result = await listener.ReceiveAsync(_cts.Token);

                    int? senderId = Clients.Values.FirstOrDefault(c => c.UdpEndpoint != null && c.UdpEndpoint.Equals(result.RemoteEndPoint))?.Id;
                    NetworkMessage msg = MessageBuilder.ReadUdpMessage(result.Buffer, NetworkSide.Server, includeData: true, senderId: senderId);

                    if (!Clients.TryGetValue(msg.SenderId, out var senderClient)) continue;

                    // A client's source port changes when its NAT mapping expires or its socket is
                    // recreated (network change, sleep/resume). Keep the stored endpoint current,
                    // otherwise replies and relays keep going to an address that no longer reaches it.
                    if (senderClient.UdpEndpoint == null || !senderClient.UdpEndpoint.Equals(result.RemoteEndPoint))
                    {
                        // Only accept the change if the packet really decrypts with this client's
                        // key, so one client cannot redirect another client's traffic by spoofing SenderId.
                        bool verified;
                        try
                        {
                            verified = MessageBuilder.ReadUdpMessage(result.Buffer, NetworkSide.Server, includeData: false, senderId: msg.SenderId) != null;
                        }
                        catch (Exception)
                        {
                            verified = false;
                        }

                        if (!verified)
                        {
                            Log.Server.Warning($"[SERVER UDP] Ignoring endpoint change for client {msg.SenderId} (sender could not be verified).");
                            continue;
                        }

                        Log.Server.Info($"[SERVER UDP] Client {senderClient.Id} UDP endpoint updated: {senderClient.UdpEndpoint?.ToString() ?? "none"} -> {result.RemoteEndPoint}");
                        senderClient.UdpEndpoint = result.RemoteEndPoint;
                    }

                    if (msg.MessageType == MessageType.UdpRegister) continue;

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
                                Log.Server.Info($"[SERVER UDP] Method execution failed: {ex}");
                            }
                        }, _cts.Token);
                    }

                    if (msg.TargetId.Any(t => t > 0))
                    {
                        foreach (var targetId in msg.TargetId.Where(t => t > 0))
                        {
                            if (!Clients.TryGetValue(targetId, out var targetClient) || targetClient.UdpEndpoint == null)
                            {
                                Log.Server.Info($"[SERVER UDP] Cannot forward, target {targetId} not available.");
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

                            var forwardedPacket = MessageBuilder.CreateUdpMessage(forwardedMsg, NetworkSide.Server);
                            await listener.SendAsync(forwardedPacket.AsMemory(), targetClient.UdpEndpoint, _cts.Token);
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
                    Log.Server.Info($"[SERVER UDP] Receive loop error: {ex.Message}");

                    // Back off so a persistent socket error cannot turn into a hot loop that
                    // starves the rest of the process (including Arma's callback delivery).
                    try { await Task.Delay(50, _cts.Token); }
                    catch (OperationCanceledException) { break; }
                }
            }

            Log.Server.Info("[SERVER UDP] Receive loop stopped.");
        }, _cts.Token);
    }

    private static async Task BroadcastUdp(Connection sender, NetworkMessage message)
    {
        foreach (var client in Clients.Values.ToArray())
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
            var packet = MessageBuilder.CreateUdpMessage(udpMsg, NetworkSide.Server, isServerBroadcast: true);

            try
            {
                var listener = _udpListener;
                if (listener == null) return;

                await listener.SendAsync(packet.AsMemory(), client.UdpEndpoint, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Server.Info($"[SERVER UDP] Failed to broadcast to client {client.Id}: {ex.Message}");
            }
        }
    }
}
