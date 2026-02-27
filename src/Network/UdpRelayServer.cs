using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static ArmaExtension.Logger;

namespace EdenOnline;

public sealed class UdpRelayServer : IDisposable
{
    private readonly UdpClient _udp;
    private readonly ConcurrentDictionary<IPEndPoint, ClientState> _clients = new();
    private readonly CancellationTokenSource _cts = new();

    private const int MaxPacketSize = 1400;          // Prevent amplification attacks
    private const int MaxMessagesPerSecond = 50;     // Rate limit per client
    private static readonly TimeSpan ClientTimeout = TimeSpan.FromMinutes(2);

    private class ClientState
    {
        public DateTime LastSeen = DateTime.UtcNow;
        public int MessageCount;
        public DateTime WindowStart = DateTime.UtcNow;
    }

    public UdpRelayServer(int port)
    {
        _udp = new UdpClient(port);
    }

    public void Start()
    {
        _ = ReceiveLoop();
        _ = CleanupLoop();
    }

    private async Task ReceiveLoop()
    {
        while (!_cts.IsCancellationRequested) {
            UdpReceiveResult result;

            try { result = await _udp.ReceiveAsync(_cts.Token); }
            catch (OperationCanceledException) { break; }

            var endpoint = result.RemoteEndPoint;
            var data = result.Buffer;

            if (!ValidatePacket(endpoint, data)) continue;

            RegisterClient(endpoint);

            RelayMessage(endpoint, data);
        }
    }

    private bool ValidatePacket(IPEndPoint endpoint, byte[] data)
    {
        if (data.Length == 0 || data.Length > MaxPacketSize)
            return false;

        var state = _clients.GetOrAdd(endpoint, _ => new ClientState());

        var now = DateTime.UtcNow;
        if ((now - state.WindowStart).TotalSeconds >= 1)
        {
            state.WindowStart = now;
            state.MessageCount = 0;
        }

        if (++state.MessageCount > MaxMessagesPerSecond)
        {
            Log($"[SERVER] Rate limit exceeded: {endpoint}");
            return false;
        }

        // ✔ Optional: message validation hook
        // Example: ensure UTF8 text only
        if (!IsValidUtf8(data))
            return false;

        return true;
    }

    private static bool IsValidUtf8(byte[] data)
    {
        try
        {
            Encoding.UTF8.GetString(data);
            return true;
        }
        catch { return false; }
    }

    private void RegisterClient(IPEndPoint endpoint)
    {
        var state = _clients.GetOrAdd(endpoint, _ => new ClientState());
        state.LastSeen = DateTime.UtcNow;
    }

    private void RelayMessage(IPEndPoint sender, byte[] data)
    {
        foreach (var client in _clients.Keys)
        {
            if (client.Equals(sender))
                continue;

            _udp.SendAsync(data, data.Length, client);
        }
    }

    private async Task CleanupLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), _cts.Token);

            var cutoff = DateTime.UtcNow - ClientTimeout;

            foreach (var (endpoint, state) in _clients)
            {
                if (state.LastSeen < cutoff)
                {
                    _clients.TryRemove(endpoint, out _);
                    Log($"[SERVER] Client removed (timeout): {endpoint}");
                }
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _udp.Dispose();
    }
}
