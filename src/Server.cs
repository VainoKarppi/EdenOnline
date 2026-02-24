using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;
using System.Threading;
using System.Text.Json;
using System.Linq;

namespace ArmaExtension;

public static partial class EdenOnline
{
    public static class Server
    {
        public class ArmaClient
        {
            public string Id { get; init; }
            public string? Username { get; set; }
            public TcpClient TcpClient { get; init; }

            public ArmaClient(string id, string username, TcpClient tcpClient)
            {
                Id = id;
                Username = username;
                TcpClient = tcpClient;
            }
        }

        private static int _clientIdCounter = 1;
        private static TcpListener? _listener;
        private static readonly ConcurrentDictionary<string, TcpClient> Clients = new();
        private static readonly ConcurrentDictionary<TcpClient, ArmaClient> ClientIds = new();
        private const int Port = 5000;
        public static bool IsRunning => _listener != null;
        private static string? ServerHash;
        private static string? ServerPassword;

        public static void Start(string serverHash = "", string? password = null, bool dedicatedServer = false)
        {
            if (Client.ClientListener != null) throw new InvalidOperationException("Client is already running.");
            if (_listener != null) throw new InvalidOperationException("Server is already running.");

            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start();
            Console.WriteLine($"TCP Server started on port {Port}.");

            ThreadPool.QueueUserWorkItem(AcceptClientsLoop);

            if (string.IsNullOrEmpty(serverHash)) ServerHash = serverHash;

            // If not a dedicated server, also start the client to connect to self
            if (password != null) ServerPassword = password;
        }

        public static void Stop()
        {
            if (_listener == null) return;

            // Notify all clients with a shutdown message
            foreach (var kvp in Clients)
            {
                SendMessage(kvp.Value, MessageType.ServerShutdown);
            }

            _listener?.Stop();
            Console.WriteLine("TCP Server stopped.");
            
            Clients.Clear();
            ClientIds.Clear();

            _listener = null;
        }

        private static void AcceptClientsLoop(object? _)
        {
            while (true)
            {
                try
                {
                    var client = _listener!.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleClientHandshake, client);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Accept client exception: {ex}");
                }
            }
        }

        private static object[] ReadMessage(TcpClient client, out MessageType messageType)
        {
            var stream = client.GetStream();
            byte[] buffer = new byte[8192];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0) throw new Exception("Empty message");

            // First byte = message type
            messageType = (MessageType)buffer[0];

            // Remaining bytes = payload
            string paramJson = Encoding.UTF8.GetString(buffer, 1, bytesRead - 1);

            // Deserialize as object[]
            return Serializer.DeserializeParameters(paramJson);
        }
        private static void HandleClientHandshake(object? obj)
        {
            if (obj is not TcpClient client) return;

            try
            {
                    
                // 1️⃣ Read handshake message and get the message type
                MessageType messageType;
                object[] handshakeParams = ReadMessage(client, out messageType);

                if (messageType != MessageType.ClientHandshake)
                {
                    Console.WriteLine($"Unexpected handshake message type: {messageType}. Disconnecting client.");
                    client.Close();
                    return;
                }

                Console.WriteLine($"Received handshake parameters: {string.Join(", ", handshakeParams)}");

                if (handshakeParams.Length < 3)
                {
                    Console.WriteLine("Invalid handshake parameters. Disconnecting client.");
                    client.Close();
                    return;
                }

                // 2️⃣ Extract parameters
                int clientId = (int)handshakeParams[0];
                int requestId = (int)handshakeParams[1];
                string userName = (string)handshakeParams[2];
                string clientHash = (string)handshakeParams[3];

                Console.WriteLine($"Handshake received from user: {userName}, modsHash: {clientHash}, requestId: {requestId}");

                // 3️⃣ Verify client mods / version
                if (!VerifyClientHandshake(clientHash))
                {
                    Console.WriteLine("Handshake verification failed. Disconnecting client.");
                    client.Close();
                    return;
                }

                // 4️⃣ Assign server IDs
                int serverClientId = _clientIdCounter++;
                string guidClientId = GetGuid(); // internal GUID for tracking

                // 5️⃣ Track the client
                Clients[guidClientId] = client;
                ClientIds[client] = new ArmaClient(guidClientId, userName, client);

                // 6️⃣ Gather other connected clients
                string[] otherClients = [.. ClientIds.Values
                    .Where(c => c.Id != guidClientId)
                    .Select(c => c.Id)];

                // 7️⃣ Send handshake response back to client: [requestId, "SUCCESS", serverClientId, otherClients]
                object[] response = [
                    requestId,
                    "SUCCESS",
                    serverClientId,
                    otherClients
                ];
                SendMessage(client, MessageType.ClientHandshake, response);

                Console.WriteLine($"Client handshake successful. Assigned GUID: {guidClientId}, server ID: {serverClientId}");

                // 8️⃣ Start listening for messages from this client
                ThreadPool.QueueUserWorkItem(HandleClientMessages, client);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Handshake exception: {ex}");
                client.Close();
            }
        }

        private static bool TryGetRequestId(object[]? parameters, out int requestId)
        {
            requestId = -1;
            if (parameters == null || parameters.Length == 0) return false;
            if (parameters[0] is JsonElement je && je.ValueKind == JsonValueKind.Number)
            {
                requestId = je.GetInt32();
                return true;
            }
            return false;
        }

        private static void HandleClientMessages(object? obj)
        {
            if (obj is not TcpClient client) return;
            if (!ClientIds.TryGetValue(client, out ArmaClient? armaClient)) return;

            var stream = client.GetStream();
            byte[] buffer = new byte[8192];

            try
            {
                while (client.Connected)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    // Parse JSON payload (skip first byte = MessageType)
                    MessageType messageType = (MessageType)buffer[0];
                    string json = Encoding.UTF8.GetString(buffer, 1, bytesRead - 1);
                    object[]? parameters = null;

                    if (!string.IsNullOrEmpty(json))
                    {
                        parameters = JsonSerializer.Deserialize<object[]>(json);
                    }

                    // Determine if message is a request
                    int? requestId = null;
                    if (parameters != null && parameters.Length > 0 && parameters[0] is JsonElement je && je.ValueKind == JsonValueKind.Number)
                    {
                        requestId = je.GetInt32();
                    }

                    bool isRequest = requestId.HasValue;

                    if (isRequest)
                    {
                        // Handle as request
                        Console.WriteLine($"Request {requestId!.Value} from {armaClient?.Id ?? "unknown"} received.");
                        // Respond later using requestId

                    }
                    else
                    {
                        // Handle as normal broadcast message
                        Console.WriteLine($"Message from {armaClient?.Id ?? "unknown"} received.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Client {armaClient?.Id ?? "unknown"} exception: {ex}");
            }
            finally
            {
                client.Close();
                Clients.TryRemove(armaClient?.Id ?? "unknown", out _);
                ClientIds.TryRemove(client, out _);
            }
        }

        private static void BroadcastCommand(TcpClient sender, MessageType type, object[]? parameters = null)
        {
            foreach (var kvp in Clients)
            {
                var client = kvp.Value;

                // Skip the sender
                if (Client.IsLocalServer()) continue;

                SendMessage(client, type, parameters);
            }
        }

        private static bool VerifyClientHandshake(string clientHash)
        {
            return string.IsNullOrEmpty(ServerHash) || clientHash == ServerHash;
        }

        private static void SendMessage(TcpClient client, MessageType type, object[]? parameters = null)
        {
            if (client == null || !client.Connected) return;

            try
            {
                // 1️⃣ Always send an object array, even if empty
                object[] finalParams = parameters ?? Array.Empty<object>();

                // 2️⃣ Serialize using a custom, AOT-safe serializer
                string paramData = Serializer.SerializeParameters(finalParams);

                Console.WriteLine($"Sending message to client: {type}, parameters: {paramData}");

                byte[] payload = Encoding.UTF8.GetBytes(paramData);

                // 3️⃣ Construct message: [MessageType][payload]
                byte[] msg = new byte[1 + payload.Length];
                msg[0] = (byte)type;
                Buffer.BlockCopy(payload, 0, msg, 1, payload.Length);

                // 4️⃣ Send to client
                client.GetStream().Write(msg, 0, msg.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message to client: {ex}");
            }
        }
        




        public static string GetGuid() => Guid.NewGuid().ToString("N").ToUpperInvariant();
    }
}
