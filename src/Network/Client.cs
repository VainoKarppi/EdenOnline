using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using ArmaExtension;
using EdenOnline;
using EdenOnline.Models;
using EdenOnline.Network;
using static ArmaExtension.Logger;

namespace EdenOnline;


public static class Client
{
    public static Connection? ClientListener { get; set; }
    private static NetworkStream? _stream;
    private static Thread? _receiveThread;

    private static readonly ConcurrentDictionary<int, Action<NetworkMessage>> PendingRequests = new();

    public static Action<MessageType, object?>? OnMessageReceived;

    public static bool Connect(string host, int port, string userName, string clientHash)
    {
        try
        {
            if (ClientListener != null && ClientListener.Connected) throw new InvalidOperationException("Client is already connected.");
            Log($"[CLIENT] Attempting to connect to server at {host}:{port} with username: {userName}");

            ClientListener = new Connection();
            ClientListener.Connect(host, port);
            _stream = ClientListener.GetStream();

            Encryption.PerformClientKeyExchange(ClientListener);

            _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            _receiveThread.Start();

            ClientListener.Username = userName;
            ClientListener.Hash = clientHash;

            RequestHandshake();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connect exception: {ex}");
            return false;
        }
    }

    public static void RequestHandshake()
    {
        if (ClientListener == null || !ClientListener.Connected) throw new InvalidOperationException("Client is not connected.");
        
        Log($"[CLIENT] Starting client handshake hash: {ClientListener.Hash}, username: {ClientListener.Username}");

        HandshakeMessage handshakeData = new()
        {
            Username = ClientListener.Username,
            Hash = ClientListener.Hash,
            ClientId = -1,
            OtherClients = []
        };

        NetworkHelper.SendRequest<HandshakeMessage>(ClientListener, MessageType.ClientHandshake, handshakeData, response => {
            if (response == null)
            {
                Console.WriteLine("Handshake failed or malformed response.");
                Disconnect();
                return;
            }

            if (response.Status == "SUCCESS")
            {
                ClientListener.Id = response.ClientId;
                Log($"[CLIENT] Handshake success. ClientId={ClientListener.Id}");
                RequestServerSync();
            }
            else
            {
                Console.WriteLine($"Handshake rejected: {response.Status}");
                Disconnect();
            }
        });
    }

    public static void RequestServerSync()
    {
        if (ClientListener == null || !ClientListener.Connected) throw new InvalidOperationException("Client is not connected.");

        NetworkHelper.SendRequest<List<ServerObject>>(ClientListener, MessageType.ObjectSync, null,
            response =>
            {
                if (response == null || response.Count < 1)
                {
                    Console.WriteLine("Object sync failed.");
                    return;
                }

                Console.WriteLine($"[CLIENT] Processing {response.Count} objects from sync");
                foreach (var obj in response)
                {
                    Console.WriteLine($"[CLIENT] Received object from server sync: {obj.Classname} (ID: {obj.Id})");
                    Extension.SendToArma("ObjectSync", new object[] { obj });
                }
            }
        );
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
                // Read message
                NetworkMessage? message = NetworkHelper.ReadMessage(ClientListener);
                if (message == null) break;

                Console.WriteLine($"[CLIENT] Received message of type {message.MessageType}, responseId: {message.ResponseId}");

                // Handle pending request responses
                if (message.ResponseId >= 0 && PendingRequests.TryRemove(message.ResponseId, out var callback))
                {
                    callback?.Invoke(message);
                    continue;
                }

                // Fire unsolicited messages
                OnMessageReceived?.Invoke(message.MessageType, message.Data);
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
