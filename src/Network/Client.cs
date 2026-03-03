using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ArmaExtension;
using EdenOnline.Models;
using EdenOnline.Network;
using static ArmaExtension.Logger;

namespace EdenOnline;


public static class Client
{
    public static Connection? ClientListener { get; set; }
    private static NetworkStream? _stream;
    private static Thread? _receiveThread;

    private static long _serverTimeOffsetMilliseconds = 0;

    public static Action<MessageType, object?>? OnMessageReceived;
    

    public static async Task<int> Connect(string host, int port, string userName, string worldName, string clientHash)
    {
        try
        {
            if (ClientListener != null && ClientListener.Connected) throw new InvalidOperationException("Client is already connected.");
            Log($"[CLIENT] Attempting to connect to server at {host}:{port} with username: {userName}");

            ClientListener = new Connection();
            ClientListener.Connect(host, port);
            _stream = ClientListener.GetStream();

            Encryption.PerformClientKeyExchange(ClientListener);

            SyncServerTime();

            _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            _receiveThread.Start();

            ClientListener.Username = userName;
            ClientListener.Hash = clientHash;

            int? userId = await RequestHandshake(worldName);
            if (userId == null) {
                Disconnect();
                throw new Exception("Unable to receive clientID");
            }

            Console.WriteLine($"UUUSEER ID: {userId}");

            return (int)userId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connect exception: {ex}");
            Disconnect();
            throw; // Throw error to arma
        }
    }

    // TODO send client time once to server, and calculate delta instead. Long --> short (saving bandwidth)
    private static void SyncServerTime()
    {
        if (ClientListener == null || !ClientListener.Connected) throw new InvalidOperationException("Client is not connected.");

        const int attempts = 10;
        List<long> offsets = [];

        var stream = ClientListener.GetStream();

        for (int i = 0; i < attempts; i++)
        {
            try
            {
                // Record local send time
                long t0 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // Send sync request (example: you may need a real request packet)
                byte[] request = [0x01]; // TODO make as real request. Or not??
                stream.Write(request, 0, request.Length);

                // Read server response (example assumes server sends UnixTime as 8-byte long)
                byte[] response = new byte[8];
                int read = 0;
                while (read < 8)
                    read += stream.Read(response, read, 8 - read);

                long serverTime = BitConverter.ToInt64(response, 0);

                // Record local receive time
                long t1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // Round-trip time estimate
                long rtt = t1 - t0;

                // Estimate offset: serverTime - (t0 + rtt/2)
                long offset = serverTime - (t0 + rtt / 2);
                offsets.Add(offset);

                Thread.Sleep(20);
            }
            catch
            {
                // ignore failed attempt
            }
        }

        if (offsets.Count > 0)
        {
            // Use median or average to reduce outliers
            _serverTimeOffsetMilliseconds = (long)offsets.Average();
            Console.WriteLine($"Server time offset: {_serverTimeOffsetMilliseconds} ms");
        }
    }

    private static DateTimeOffset GetServerTime()
    {
        return DateTimeOffset.UtcNow.AddMilliseconds(_serverTimeOffsetMilliseconds);
    }

    public static async Task<int> RequestHandshake(string worldName)
    {
        if (ClientListener == null || !ClientListener.Connected) throw new InvalidOperationException("Client is not connected.");
        
        Log($"[CLIENT] Starting client handshake hash: {ClientListener.Hash}, username: {ClientListener.Username}");

        HandshakeMessage handshakeData = new()
        {
            Username = ClientListener.Username,
            Hash = ClientListener.Hash,
            World = worldName,
            ClientId = -1,
            OtherClients = []
        };

        HandshakeMessage? response = await NetworkHelper.SendRequestAsync<HandshakeMessage>(ClientListener, MessageType.Handshake, handshakeData);

        if (response == null) throw new Exception("Unable to get response from server");

        if (response.Status != "SUCCESS") throw new Exception($"Handshake rejected: {response.Status}");

        ClientListener.Id = response.ClientId;
        Log($"[CLIENT] Handshake success. ClientId={ClientListener.Id}");
        await RequestObjectSync();

        return response.ClientId;
    }

    public static async Task RequestObjectSync()
    {
        if (ClientListener == null || !ClientListener.Connected) throw new InvalidOperationException("Client is not connected.");

        List<ArmaObject>? response = await NetworkHelper.SendRequestAsync<List<ArmaObject>?>(ClientListener, MessageType.ObjectSync, null);

        if (response == null)
        {
            Console.WriteLine("Object sync failed.");
            return;
        }

        Console.WriteLine($"[CLIENT] Processing {response.Count} objects from sync");
        Extension.SendToArma("ObjectSyncCount", [response.Count]); // First send object count to track progress
        foreach (var obj in response)
        {
            Console.WriteLine($"[CLIENT] Received object from server sync: {obj.Id}");
            Extension.SendToArma("ObjectSync", [obj.Id, obj.Attributes]);
        }

        // TODO request for objects where timestamp later than x
    }

    public static void Disconnect()
    {
        try
        {
            if (ClientListener != null && ClientListener.Connected)
            {
                NetworkHelper.SendMessage(ClientListener, -1, MessageType.ClientDisconnect, -1);
            }
        }
        catch { }
        finally
        {
            try { ClientListener?.Close(); } catch { }
            ClientListener = null;
            _stream = null;
            try { _receiveThread?.Join(500); } catch { }
            _receiveThread = null;
        }
    }

    public static bool IsConnected => ClientListener != null && ClientListener.Connected;

    private static void ReceiveLoop()
    {
        if (ClientListener == null) return;

        try
        {
            while (ClientListener != null && ClientListener.Connected)
            {
                NetworkMessage? message = NetworkHelper.ReadMessage(ClientListener);
                if (message == null) break;
                
                if (message.MessageType == MessageType.ServerShutdown) return;

                // Check if is response to specific request
                if (message.MessageId > 0)
                {
                    NetworkHelper.Responses[message.MessageId] = message.Data;
                    continue;
                }

                switch (message.MessageType)
                {
                    case MessageType.ObjectCreate:
                        if (message.Data == null) continue;

                        ArmaObject? obj = NetworkSerializer.DeserializeData<ArmaObject>(message.Data);
                        if (obj == null) continue;
                        Extension.SendToArma("ObjectCreated", [obj.Id, obj.Attributes]);
                        break;

                    case MessageType.ObjectUpdate:

                        break;

                    case MessageType.ObjectRemove:

                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(message.MessageType), "Unsupported object update type");
                }

                if (message.MessageType == MessageType.ServerShutdown) return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CLIENT] ReceiveLoop exception: {ex}");
        }
        finally
        {
            Disconnect();
        }
    }
}
