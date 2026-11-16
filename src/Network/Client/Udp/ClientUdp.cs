using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DynTypeSerializer;
using Microsoft.Extensions.Logging;


namespace DynTypeNetwork;


public static partial class Client
{
    private static UdpClient? _udpClient;
    private static IPEndPoint? _udpEndpoint;
    private static readonly object _udpSocketLock = new();

    // The UDP endpoint is registered once at connect time, but NAT mappings expire and
    // sockets can be recreated (network change, sleep/resume, transient socket errors).
    // Re-sending the registration periodically keeps the NAT mapping alive and lets the
    // server recover an endpoint that went stale after the local port changed.
    private const int UdpRegisterIntervalMs = 3000;
    private const int UdpErrorRetryDelayMs = 1000;

    public static bool IsUdpConnected() => _udpEndpoint != null && IsSocketUsable(_udpClient);

    // ── Connect UDP ──────────────────────────
    private static async Task ConnectUdp(string host, int port)
    {
        _udpEndpoint = new IPEndPoint(IPAddress.Parse(host), port);
        _udpClient = CreateUdpSocket();

        // Both loops share the connection lifetime token, so a disconnect stops them.
        var token = _cts.Token;

        _ = Task.Run(() => UdpReceiveLoopAsync(token), token);
        _ = Task.Run(() => UdpRegisterLoopAsync(token), token);

        await SendUdpRegisterAsync();
    }

    private static UdpClient CreateUdpSocket()
    {
        // Bind to an ephemeral local port; the server learns it from the registration packet.
        var socket = new UdpClient(AddressFamily.InterNetwork);
        socket.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        return socket;
    }

    /// <summary>
    /// Replaces a dead UDP socket with a fresh one and re-registers the new local port with
    /// the server. A failed socket never recovers on its own, so without this the client
    /// would silently stop sending and receiving UDP for the rest of the session.
    /// </summary>
    /// <param name="expected">Socket that failed. It is only replaced if it is still current.</param>
    private static UdpClient? TryRecreateUdpSocket(UdpClient? expected)
    {
        // Do not resurrect the socket after the client has been disconnected on purpose.
        if (_udpEndpoint == null || !IsTcpConnected()) return null;

        lock (_udpSocketLock)
        {
            if (expected != null && !ReferenceEquals(_udpClient, expected))
                return _udpClient;

            try { expected?.Dispose(); } catch { }

            try
            {
                _udpClient = CreateUdpSocket();
            }
            catch (Exception ex)
            {
                Log.Client.Error($"[CLIENT UDP] Failed to recreate UDP socket: {ex.Message}");
                _udpClient = null;
                return null;
            }
        }

        _ = SendUdpRegisterAsync();
        return _udpClient;
    }

    /// <summary>
    /// True when the socket can still send. A disposed UdpClient reports a null socket
    /// instead of throwing, so both cases have to be handled.
    /// </summary>
    private static bool IsSocketUsable(UdpClient? client)
    {
        if (client == null) return false;

        try
        {
            var socket = client.Client;
            return socket != null && socket.IsBound;
        }
        catch (ObjectDisposedException) { return false; }
    }

    private static async Task UdpReceiveLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var client = _udpClient;
            if (!IsSocketUsable(client))
            {
                client = TryRecreateUdpSocket(client);
            }

            if (client == null)
            {
                await DelayAsync(UdpErrorRetryDelayMs, token);
                continue;
            }

            try
            {
                var result = await client.ReceiveAsync(token);
                ProcessUdpPacket(result.Buffer, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested) break;

                Log.Client.Warning($"[CLIENT UDP] Receive failed ({ex.GetType().Name}: {ex.Message}). Recreating UDP socket...");

                if (TryRecreateUdpSocket(client) == null)
                    await DelayAsync(UdpErrorRetryDelayMs, token);
            }
        }
    }

    private static void ProcessUdpPacket(byte[] buffer, CancellationToken token)
    {
        NetworkMessage msg;
        try
        {
            msg = MessageBuilder.ReadUdpMessage(buffer, NetworkSide.Client, includeData: true);
        }
        catch (Exception ex)
        {
            Log.Client.Debug($"[CLIENT UDP] Invalid packet: {ex.Message}");
            return; // ignore bad packets
        }

        // TODO Block message, if it appeared to this client, without it being ment for it
        if (msg.MessageType != MessageType.Custom /*|| msg.TargetId != ClientID*/)
            return;

        _ = Task.Run(() => OnUdpMessageReceived?.Invoke(msg), token);

        _ = Task.Run(() =>
        {
            try
            {
                var request = MessageBuilder.UnpackPayload<MethodRequest>(msg.Payload);
                if (request == null) throw new Exception("Unable to unpack payload");

                MethodBuilder.CallClientMethod<object>(request.MethodName!, msg, request.Args!);
            }
            catch (Exception ex)
            {
                Log.Client.Debug($"[CLIENT UDP] Method execution failed: {ex}");
            }
        }, token);
    }

    private static async Task UdpRegisterLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await SendUdpRegisterAsync();
            }
            catch (Exception ex)
            {
                Log.Client.Debug($"[CLIENT UDP] Registration failed: {ex.Message}");
            }

            if (!await DelayAsync(UdpRegisterIntervalMs, token)) break;
        }
    }

    private static async Task SendUdpRegisterAsync()
    {
        var client = _udpClient;
        var endpoint = _udpEndpoint;

        if (client == null || endpoint == null || ClientID <= 0) return;

        NetworkMessage registerMsg = new()
        {
            SenderId = ClientID,
            TargetId = [Server.SERVER_ID],
            MessageType = MessageType.UdpRegister
        };

        var packet = MessageBuilder.CreateUdpMessage(registerMsg, NetworkSide.Client);

        try
        {
            await client.SendAsync(packet, packet.Length, endpoint);
        }
        catch (Exception ex)
        {
            // The receive loop owns socket replacement; a failed heartbeat is not fatal.
            Log.Client.Debug($"[CLIENT UDP] Registration send failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends a pre-built UDP packet, rebuilding the socket once if it died underneath us.
    /// </summary>
    private static async Task SendUdpPacketAsync(byte[] packet)
    {
        var endpoint = _udpEndpoint ?? throw new InvalidOperationException("UDP client not connected.");

        for (int attempt = 0; ; attempt++)
        {
            var client = IsSocketUsable(_udpClient)
                ? _udpClient!
                : TryRecreateUdpSocket(_udpClient) ?? throw new InvalidOperationException("UDP client not connected.");

            try
            {
                await client.SendAsync(packet, packet.Length, endpoint);
                return;
            }
            catch (Exception ex) when (attempt == 0 && (ex is ObjectDisposedException || ex is SocketException || ex is NullReferenceException))
            {
                Log.Client.Warning($"[CLIENT UDP] Send failed ({ex.GetType().Name}: {ex.Message}). Recreating UDP socket and retrying.");

                if (TryRecreateUdpSocket(client) == null) throw;
            }
        }
    }

    private static async Task<bool> DelayAsync(int milliseconds, CancellationToken token)
    {
        try
        {
            await Task.Delay(milliseconds, token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
