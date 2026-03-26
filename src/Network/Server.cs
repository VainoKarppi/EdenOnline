using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Linq;
using System.Collections.Generic;

using static ArmaExtension.Logger;

using EdenOnline.Network;
using EdenOnline.Models;
using System.Threading.Tasks;

namespace EdenOnline;


public static class Server
{

    private static int _clientIdCounter = 1;
    private static TcpListener? _listener;
    private static UdpRelayServer? _udpServer;
    private static readonly List<Connection> Clients = [];

    public static bool IsRunning => _listener != null;
    private static string? ServerHash;
    public static string? ServerWorld;
    private static string ServerPassword { get; set; } = "";

    private const int ServerID = 1;

    public static void StartUDP()
    {
        // UDP relay started by UdpRelayServer elsewhere
    }

    public static void Start(int port, string serverHash = "", string serverWorld = "", string? password = null, bool dedicatedServer = false)
    {
        if (Client.ClientListener != null) throw new InvalidOperationException("Client is already running.");
        if (_listener != null) throw new InvalidOperationException("Server is already running.");


        if (!string.IsNullOrEmpty(serverHash)) ServerHash = serverHash;
        if (!string.IsNullOrWhiteSpace(password)) ServerPassword = password;
        if (!string.IsNullOrEmpty(serverWorld)) ServerWorld = serverWorld;

        var test = new Connection();
        test.Username = "SERVER_TEST";
        test.Id = Interlocked.Increment(ref _clientIdCounter);

        Clients.Add(test);

        _udpServer = new UdpRelayServer(port);
        _udpServer.Start();

        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        Log($"[SERVER] TCP & UDP Server started on port {port}, with hash: {serverHash}, password protected: {!string.IsNullOrWhiteSpace(password)}, dedicated: {dedicatedServer}");

        ThreadPool.QueueUserWorkItem(AcceptClientsLoop);
    }

    public static void Stop()
    {
        Log("[SERVER] Stopping server...");
        if (!IsRunning) return;
        
        _udpServer?.Dispose();
        _udpServer = null;

        // Notify all clients with a shutdown message
        foreach (var client in Clients) NetworkHelper.SendMessage(client, MessageType.ServerShutdown, ServerID);
        
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
            catch (SocketException se) when (se.SocketErrorCode == SocketError.Interrupted)
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

    private static void RemoveConnection(Connection client)
    {
        try
        {
            Clients.Remove(client);
            client?.Close();
        } catch {}
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

            Debug("[SERVER] Starting time sync...");
            SyncClientTime(client);
            Debug("[SERVER] Time sync success");

            ThreadPool.QueueUserWorkItem(WaitForHandshake, client);
            
            // TODO if mods and hash match, but world is wrong, send message to client to change world, and wait for response

            ThreadPool.QueueUserWorkItem(HandleClientMessages, client);

            // TODO Make sure user is connected to UDP server
        }
        catch (Exception ex)
        {
            Error($"[SERVER] HandleClientConnection exception: {ex}");
            RemoveConnection(client);
        }
    }

    private static void WaitForHandshake(object? obj)
    {
        if (obj is not Connection client) return;

        const int handshakeTimeoutMs = 2000; // 5 seconds
        const int checkIntervalMs = 10;      // how often to check
        int elapsedMs = 0;

        while (!client.HandshakeDone && elapsedMs < handshakeTimeoutMs)
        {
            Thread.Sleep(checkIntervalMs);
            elapsedMs += checkIntervalMs;
        }

        if (!client.HandshakeDone)
        {
            // Handshake did not complete in time
            NetworkHelper.SendMessage(client, MessageType.HandshakeTimeout, ServerID);
            RemoveConnection(client);
            Log($"[SERVER] Handshake timeout for client {client.Client.RemoteEndPoint}");
        }
    }

    private static void SyncClientTime(Connection client)
    {
        try
        {
            var stream = client.GetStream();

            for (int i = 0; i < 10; i++)
            {
                // Read request (1 byte expected)
                byte[] requestBuffer = new byte[1];
                int read = 0;
                while (read < 1)
                {
                    int r = stream.Read(requestBuffer, read, 1 - read);
                    if (r == 0)
                        return; // client disconnected
                    read += r;
                }

                // Check if request is the time request (0x01)
                if (requestBuffer[0] != 0x01)
                    continue; // not a time request, skip

                // Get current server Unix time in milliseconds
                long serverUnixTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                byte[] response = BitConverter.GetBytes(serverUnixTime);

                // Send response back to client
                stream.Write(response, 0, response.Length);
                stream.Flush();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error syncing time for client: {ex.Message}");
        }
    }

    private static void HandleClientHandshake(Connection client, NetworkMessage message)
    {
        if (message == null || message.Data == null || message.MessageType != MessageType.Handshake)
        {
            Warning($"[SERVER] Unexpected handshake type {message?.MessageType}: Returned: {message?.MessageType}");
            RemoveConnection(client);
            return;
        }

        HandshakeMessage? data = NetworkSerializer.DeserializeData<HandshakeMessage>(message.Data);
        if (data == null)
        {
            Warning("[SERVER] Empty handshake");
            RemoveConnection(client);
            return;
        }

        if (data.World != ServerWorld)
        {
            HandshakeMessage responseFail = new() {
                Status = "Invalid world"
            };
            NetworkHelper.SendResponseMessage(client, MessageType.Handshake, ServerID, message.MessageId, responseFail);
            RemoveConnection(client);
            return;
        }

        client.Username = data.Username;
        client.Hash = data.Hash;


        if (!VerifyClientPassword(data.PasswordHash))
        {
            HandshakeMessage responseFail = new() {
                Status = "Wrong password"
            };
            NetworkHelper.SendResponseMessage(client, MessageType.Handshake, ServerID, message.MessageId, responseFail);
            RemoveConnection(client);
            return;
        }

        if (!VerifyClientHandshake(client.Hash))
        {
            HandshakeMessage responseFail = new() {
                Status = "Hash mismatch"
            };
            NetworkHelper.SendResponseMessage(client, MessageType.Handshake, ServerID, message.MessageId, responseFail);
            RemoveConnection(client);
            return;
        }
        
        object[] otherClients = Clients
            //.Where(c => c.Id != client.Id)
            .Select(c => new object[] { c.Id, c.Username })
            .ToArray();


        HandshakeMessage response = new() {
            Status = "SUCCESS",
            ClientId = client.Id,
            OtherClients = otherClients
        };
        NetworkHelper.SendResponseMessage(client, MessageType.Handshake, ServerID, message.MessageId, response);

        client.HandshakeDone = true;
        Log($"[SERVER] Handshake success for {client.Username} => {client.Id}");
    }

    private static void SendServerObjectsSync(Connection client, NetworkMessage message)
    {
        //NetworkMessage? message = NetworkHelper.ReadMessage(client);
        if (message == null || message.MessageType != MessageType.ObjectSync)
        {
            Warning($"[SERVER] Unexpected ObjectSync type {message?.MessageType}: Returned: {message?.MessageType}");
            RemoveConnection(client);
            return;
        }

        List<ArmaObject> objects = ObjectManager.GetAllObjects();
        
        NetworkHelper.SendResponseMessage(client, MessageType.ObjectSync, ServerID, message.MessageId, objects);
    }

    private static void HandleObjectUpdate(NetworkMessage message)
    {
        if (message.Data == null)
            throw new ArgumentNullException(nameof(message.Data), "Message data is null");

        // Deserialize the ServerObject from the message
        ArmaObject? update = NetworkSerializer.DeserializeData<ArmaObject>(message.Data);
        if (update == null)
            throw new ArgumentNullException(nameof(update), "Invalid ServerObject data");

        switch (message.MessageType)
        {
            case MessageType.ObjectCreate:
                // Add new object (or overwrite if it exists)
                ObjectManager.AddObject(update);
                break;

            case MessageType.ObjectUpdate:
                // Update only provided fields
                ObjectManager.UpdateObject(update.Id, existing =>
                {
                    if (update.Attributes != null && update.Attributes.Count > 0) existing.Attributes = update.Attributes;
                });
                break;

            case MessageType.ObjectRemove:
                // Remove object by Id
                ObjectManager.RemoveObject(update.Id);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(message.MessageType), "Unsupported object update type");
        }

        if (message.MessageType != MessageType.ObjectRemove) update.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Notify all clients
        foreach (var client in Clients)
        {
            //if (message.SenderId == client.Id) continue; // Dont send back to client
            NetworkHelper.SendMessage(client, message.MessageType, message.SenderId, update);
        }
    }

    private static void HandleClientMessages(object? obj)
    {
        if (obj is not Connection client) return;

        Console.WriteLine($"[SERVER] Started client loop for: {client.Username} ({client.Id})");
        // Add to client list after successful handshake
        Clients.Add(client);
        client.NoDelay = true;

        try
        {
            while (client.Connected)
            {
                NetworkMessage? message = NetworkHelper.ReadMessage(client);
                if (message == null) continue; // print?

                if (message.MessageType == MessageType.Handshake) {
                    if (!client.HandshakeDone) HandleClientHandshake(client, message);
                    continue;
                }


                // basic handling for a few types
                switch (message.MessageType)
                {
                    case MessageType.ObjectSync: 
                        SendServerObjectsSync(client, message);
                        break;

                    case MessageType.ClientDisconnect:
                        //TODO notify other clients
                        RemoveConnection(client);
                        break;
                    
                    case MessageType.ObjectCreate:
                    case MessageType.ObjectRemove:
                    case MessageType.ObjectUpdate:
                        HandleObjectUpdate(message);
                        break;
                    
                    case MessageType.CameraUpdate:
                        ForwardClientMessage(message);
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
            RemoveConnection(client);
        }
    }

    private static void ForwardClientMessage(NetworkMessage message)
    {
        foreach (Connection? client in Clients) {
            //if (message.SenderId == client.Id) continue; // Dont send back to client
            NetworkHelper.SendMessage(client, message.MessageType, message.SenderId, message.Data);
        }
    }

    private static bool VerifyClientPassword(string passwordHash)
    {
        return NetworkHelper.HashPassword(ServerPassword) == passwordHash;
    }
    private static bool VerifyClientHandshake(string clientHash)
    {
        return string.IsNullOrEmpty(ServerHash) || clientHash == ServerHash;
    }





    public static string GetGuid() => Guid.NewGuid().ToString("N").ToUpperInvariant();
}
