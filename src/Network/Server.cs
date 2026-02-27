using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Linq;
using System.Collections.Generic;

using static ArmaExtension.Logger;

using EdenOnline.Network;
using EdenOnline.Models;

namespace EdenOnline;


public static class Server
{

    private static int _clientIdCounter = 1;
    private static TcpListener? _listener;
    private static UdpRelayServer? _udpServer;
    private static readonly List<Connection> Clients = [];
    private const int Port = 5000;
    public static bool IsRunning => _listener != null;
    private static string? ServerHash;
    private static string? ServerPassword;

    private const int ServerID = 1;

    public static void StartUDP()
    {
        // UDP relay started by UdpRelayServer elsewhere
    }

    public static void Start(string serverHash = "", string? password = null, bool dedicatedServer = false)
    {
        if (Client.ClientListener != null) throw new InvalidOperationException("Client is already running.");
        if (_listener != null) throw new InvalidOperationException("Server is already running.");

        if (string.IsNullOrEmpty(serverHash)) ServerHash = serverHash;

        // If not a dedicated server, also start the client to connect to self
        if (!string.IsNullOrWhiteSpace(password)) ServerPassword = password;

        _udpServer = new UdpRelayServer(Port);
        _udpServer.Start();

        _listener = new TcpListener(IPAddress.Any, Port);
        _listener.Start();
        Log($"[SERVER] TCP & UDP Server started on port {Port}, with hash: {serverHash}, password protected: {!string.IsNullOrWhiteSpace(password)}, dedicated: {dedicatedServer}");

        ThreadPool.QueueUserWorkItem(AcceptClientsLoop);
    }

    public static void Stop()
    {
        Log("[SERVER] Stopping server...");
        if (!IsRunning) return;
        
        _udpServer?.Dispose();
        _udpServer = null;

        // Notify all clients with a shutdown message
        foreach (var client in Clients)
        {
            NetworkHelper.SendMessage(client, MessageType.ServerShutdown, ServerID, Array.Empty<object>(), 0);
        }


        _listener?.Stop();
        Log("[SERVER] TCP & UDP Servers stopped.");
        
        Clients.Clear();

        _listener = null;
    }

    private static void AcceptClientsLoop(object? _)
    {
        while (IsRunning)
        {
            try
            {
                TcpClient tcpClient = _listener!.AcceptTcpClient();

                // wrap it
                var client = new Connection
                {
                    Client = tcpClient.Client
                };

                ThreadPool.QueueUserWorkItem(HandleClientConnection, client);
            }
            catch (ObjectDisposedException)
            {
                // Listener was stopped, exit gracefully
                break;
            }
            catch (SocketException se) when (se.SocketErrorCode == System.Net.Sockets.SocketError.Interrupted)
            {
                // Listener stopped; ignore
                break;
            }
            catch (Exception ex)
            {
                Error($"[SERVER] Accept client exception: {ex}");
            }
        }
    }

    
    private static void HandleClientConnection(object? obj)
    {
        if (obj is not Connection client) return;

        try
        {   
            Log($"[SERVER] New client connection from {client.Client.RemoteEndPoint}");
            // Get client ID
            client.Id = Interlocked.Increment(ref _clientIdCounter);

            // Wait until client requests key exchange, then perform it and then send server key back to client
            Encryption.PerformServerKeyExchange(client);
            
            // Start handshake, check password/hash and if successful, add to client list
            HandleClientHandshake(client);
            
            // TODO if mods and hash match, but world is wrong, send message to client to change world, and wait for response

            // After handshake, send initial object sync
            SendServerObjectsSync(client);

            // TODO add timeout checks and disconnect if client is idle for too long or doesn't complete handshake in time
            // TODO Make sure user is connected to UDP server


            // Start message handling loop for this client
            ThreadPool.QueueUserWorkItem(HandleClientMessages, client);
        }
        catch (Exception ex)
        {
            Error($"[SERVER] HandleClientConnection exception: {ex}");
            try { client.Close(); } catch { }
        }
    }

    private static void HandleClientHandshake(Connection client)
    {
        NetworkMessage? message = NetworkHelper.ReadMessage(client);

        if (message == null || message.MessageType != MessageType.ClientHandshake)
        {
            Warning($"[SERVER] Unexpected handshake type {message?.MessageType ?? MessageType.Custom}");
            client.Close();
            return;
        }

        if (message.Data == null || !(message.Data is HandshakeMessage handshakeMessage))
        {
            Warning("[SERVER] Empty handshake");
            client.Close();
            return;
        }

        client.Username = handshakeMessage.Username;
        client.Hash = handshakeMessage.Hash;

        if (!VerifyClientHandshake(client.Hash))
        {
            NetworkHelper.SendMessage(client, MessageType.ClientHandshake, ServerID, new object[] { "FAIL", "Hash mismatch" }, message.ResponseId);
            client.Close();
            return;
        }

        object[] OtherClients = Clients.Select(c => new ArmaClient { Id = c.Id, Username = c.Username }).ToArray();

        object[] response = ["SUCCESS", client.Id, OtherClients];
        NetworkHelper.SendMessage(client, MessageType.ClientHandshake, ServerID, response, message.ResponseId);

        Log($"[SERVER] Handshake success for {client.Username} => {client.Id}");
    }

    private static void SendServerObjectsSync(Connection client)
    {
        // TODO
    }


    private static void HandleClientMessages(object? obj)
    {
        if (obj is not Connection client) return;

        // Add to client list after successful handshake
        Clients.Add(client);

        try
        {
            while (client.Connected)
            {
                NetworkMessage? message = NetworkHelper.ReadMessage(client);

                if (message == null) continue;

                // basic handling for a few types
                switch (message.MessageType)
                {
                    case MessageType.ObjectSync:
                        // client requesting sync — send current objects
                        SendServerObjectsSync(client);
                        break;
                    case MessageType.ClientDisconnect:
                        Log($"[SERVER] Client {client.Id} requested disconnect");
                        client.Close();
                        break;
                    default:
                        Log($"[SERVER] Unhandled {message.MessageType} from {client.Id}");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Error($"[SERVER] Client {client.Id} exception: {ex}");
        }
        finally
        {
            Log($"[SERVER] Client {client.Id} disconnected.");
            try { client.Close(); } catch { }
            Clients.Remove(client);
        }
    }


    private static bool VerifyClientHandshake(string clientHash)
    {
        return string.IsNullOrEmpty(ServerHash) || clientHash == ServerHash;
    }





    public static string GetGuid() => Guid.NewGuid().ToString("N").ToUpperInvariant();
}
