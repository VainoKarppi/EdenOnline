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

namespace DynTypeNetwork;






public static partial class Client
{
    private readonly static List<int> Clients = [];
    public static int ClientID;
    private static CancellationTokenSource _cts = new();

    public static async Task<int> ConnectAsync(string host, int port, bool startUdp = false, string? customHash = null)
    {
        host = await ResolveIPv4Async(host);
        
        int userId = await ConnectTcp(host, port, customHash);
        if (startUdp) await ConnectUdp(host, port);

        return userId;
    }

    private static async Task<string> ResolveIPv4Async(string host)
    {
        if (IPAddress.TryParse(host, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork) {
            return host;
        }

        var addresses = await Dns.GetHostAddressesAsync(host);

        var ipv4 = addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork);

        if (ipv4 == null) throw new SocketException((int)SocketError.AddressFamilyNotSupported);

        return ipv4.ToString();
    }

    public static List<int> GetOtherClients() {
        if (!IsTcpConnected()) throw new  Exception("Not connected to server");

        return Clients;
    }

    private static async Task InvokeEventAsync(Func<Task?>? eventHandler, int timeoutMs = 100)
    {
        if (eventHandler == null)
            return;

        var timer = TimeSpan.FromMilliseconds(timeoutMs);

        var tasks = eventHandler
            .GetInvocationList()
            .Cast<Func<Task?>>()
            .Select(handler => handler())
            .Where(task => task != null)
            .Cast<Task>()
            .ToArray();

        if (tasks.Length == 0)
            return;

        await Task.WhenAny(
            Task.WhenAll(tasks),
            Task.Delay(timeoutMs)
        );
    }
    
    private static async Task HandleServerShutdown(DisconnectReason reason)
    {
        // Invoke the shutdown event asynchronously and wait up to 100 ms for it to complete.
        // The event handler may continue running in the background if it exceeds the timeout.
        await InvokeEventAsync(() => OnServerShutdown?.Invoke(reason), 100);

        // Clean up connections
        await DisconnectAsync();
    }

    internal static async Task ResetConnectionStatusAsync()
    {
        try
        {
            _cts?.Cancel();

            ClientID = 0;

            Clients.Clear();

            _tcpStream?.Dispose();
            _tcpStream = null;
            _tcpClient?.Close();
            _tcpClient = null;

            _udpClient?.Dispose();
            _udpClient = null;
            _udpEndpoint = null;
        } catch {}
    }

    public static async Task DisconnectAsync(DisconnectReason reason = DisconnectReason.ClientDisconnect)
    {
        try
        {
            if (IsTcpConnected()) await SendMessageAsync(Server.SERVER_ID, MessageType.ClientDisconnected, null);

            await ResetConnectionStatusAsync();

            OnClientDisconnected?.Invoke(true, reason);
        } catch {}
    }
}