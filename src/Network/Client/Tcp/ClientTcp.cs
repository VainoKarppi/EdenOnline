using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DynTypeSerializer;
using static DynTypeNetwork.Settings.Logging;


namespace DynTypeNetwork;



public static partial class Client
{
    private static TcpClient? _tcpClient;
    private static NetworkStream? _tcpStream;
    
    
    public static bool IsTcpConnected() => _tcpClient != null && _tcpClient.Connected;


    // ── Connect TCP ──────────────────────────
    private static async Task<int> ConnectTcp(string host, int port, string? customHash = null)
    {
        try {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(host, port);
            _tcpStream = _tcpClient.GetStream();
            StartTcpReceiveLoop(_tcpStream);

            // Request handshake and encryption
            await RequestHandshakeFromServer(customHash);

            // Wait until Authentication has completed
            await WaitForAuthenticationAsync();

            // Allow API user to request custom data from server, before connect success (eg. other clients etc)
            OnClientConnected?.Invoke(ClientID);

            return ClientID;
        } catch (Exception ex) {
            // TODO get real HandshakeFailureReason
            await InvokeEventAsync(() => OnHandshakeFailed?.Invoke(HandshakeFailureReason.Unknown, ex.Message));
            throw; // Pass to "front end"
        }
    }
    
    private static async Task RequestHandshakeFromServer(string? customHash = null)
    {
        KeyExchange.InitializeClientKeyExchange();

        //string assemblyHash = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";

        string buildId = Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId.ToString();

        // Combine with customHash if provided
        var availableMethods = MethodBuilder.GetAvailableClientMethods();
        string methodsHash = MethodBuilder.ComputeMethodsHash(availableMethods);

        HandshakeMessage handshake = new() {
            Hash = $"{buildId}|{customHash ?? ""}|{methodsHash}",
            AvailableMethods = availableMethods,
            ClientPublicKey = Convert.ToBase64String(KeyExchange.ClientPublicKey!)
        };
        
        HandshakeMessage? response = await RequestDataInternalAsync(Server.SERVER_ID, MessageType.Handshake, handshake);
        if (response == null) throw new Exception("Handshake failed (Connection lost)");

        if (!response.Success) throw new Exception(response.Message ?? "Handshake failed (Unknown reason)");
        if (string.IsNullOrEmpty(response.ServerPublicKey)) throw new Exception("Handshake failed (Missing server public key)");

        KeyExchange.ComputeClientSharedSecret(response.ServerPublicKey);

        ClientID = response.ClientId;
        Clients.AddRange(response.OtherConnectedClients);

        MethodBuilder.RegisterFromHandshake(response.AvailableMethods, isServer: false);
    }
    private static async Task WaitForAuthenticationAsync()
    {
        if (Authentication.Enabled && !Authentication.ClientAuthenticated == null) {
            if (LogItem(LogLevel.Debug)) Console.WriteLine("[CLIENT] Sending server message saying we are ready to recieve authentication check request...");

            // Send server a message saying that we are ready to receive the
            await SendDataInternalAsync(Server.SERVER_ID, MessageType.AuthenticationReady);

            while (Authentication.Enabled && Authentication.ClientAuthenticated == null) {
                await Task.Delay(100);
            }

            if (Authentication.ClientAuthenticated == false) throw new Exception($"Authentication failed. {Authentication.ClientAuthenticationError ?? "Unknown error"}");
        }
    }
    


    private static void StartTcpReceiveLoop(NetworkStream stream)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    // Read ONE full message from stream using the proper helper
                    NetworkMessage? msg = MessageBuilder.ReadTcpMessage(stream);

                    if (msg == null) {
                        // Connection lost or stream closed
                        await HandleServerShutdown(DisconnectReason.ConnectionError);
                        break;
                    }

                    if (msg.MessageType == MessageType.AuthenticationResponse) {
                        object[]? data = MessageBuilder.UnpackPayload<object[]>(msg.Payload);
                        if (data == null || data.Length != 2) continue;

                        bool success = (bool)data[0];
                        string? error = (string?)data[1];

                        Authentication.ClientAuthenticationError = error;
                        Authentication.ClientAuthenticated = success;

                        if (!success) {
                            await InvokeEventAsync(() => OnHandshakeFailed?.Invoke(HandshakeFailureReason.AuthenticationFailed, error ?? "Unknown"));
                            await ResetConnectionStatusAsync();
                        }

                        continue;
                    }

                    if (msg.MessageType == MessageType.AuthenticationRequest) {
                        if (!Authentication.HasClientAuthentication) {
                            if (LogItem(LogLevel.Info)) Console.WriteLine("[CLIENT] Server requested authentication, but no client authentication method has been registered.");

                            throw new InvalidOperationException("Server requested authentication, but no client authentication method has been registered.");
                        }
                        object[]? authenticationData = await Authentication.GetClientAuthenticationAsync();

                        if (LogItem(LogLevel.Debug)) Console.WriteLine($"[CLIENT] Authentication data: {string.Join(", ", authenticationData ?? [])}");

                        NetworkMessage response = new()
                        {
                            SenderId = ClientID,
                            TargetId = [Server.SERVER_ID],
                            MessageId = msg.MessageId,
                            MessageType = MessageType.AuthenticationResponse
                        };

                        byte[] packet = MessageBuilder.CreatePacket(response, authenticationData);

                        await stream.WriteAsync(packet, token);

                        continue;
                    }

                    if (msg.MessageType == MessageType.Handshake) {
                        Responses[msg.MessageId] = msg;
                        continue;
                    }

                    if (msg.MessageType == MessageType.Response) {
                        Responses[msg.MessageId] = msg;
                        continue;
                    }

                    if (msg.MessageType == MessageType.ClientConnected) {
                        if (msg.Payload == null) continue;
                        int? newClient = MessageBuilder.UnpackPayload<int>(msg.Payload);
                        if (newClient == null) continue;

                        Clients.Add(newClient.Value);

                        _ = Task.Run(() => OnOtherClientConnected?.Invoke(newClient.Value));
                        
                        continue;
                    }

                    if (msg.MessageType == MessageType.ClientDisconnected) {
                        object[]? data = MessageBuilder.UnpackPayload<object[]>(msg.Payload);
                        if (data == null || data.Length != 2) continue;

                        int client_id = (int)data[0];
                        bool success = (bool)data[1];

                        // TODO Read and send the actual reason from the data
                        DisconnectReason reason = data.Length > 2 ? (DisconnectReason?)data[2] ?? DisconnectReason.Unknown : DisconnectReason.Unknown;
                        
                        
                        Clients.Remove(client_id);
                        _ = Task.Run(() => OnOtherClientDisconnected?.Invoke(client_id, success, reason));

                        continue;
                    }

                    if (msg.MessageType == MessageType.ServerShutdown) {
                        await HandleServerShutdown(DisconnectReason.ServerShutdown);
                        break;
                    }

                    if (msg.MessageType == MessageType.Custom) {
                        // Invoke event 
                        _ = Task.Run(() => OnTcpMessageReceived?.Invoke(msg));

                        await MessageBuilder.HandleCustomMessage(stream, msg, token);
                        continue;
                    }

                    
                }
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
                if (LogItem(LogLevel.Info)) Console.WriteLine("[CLIENT] TCP receive loop cancelled.");
            }
            catch (Exception ex) when (ex is ObjectDisposedException || ex is IOException)
            {
                
                if (_cts.Token.IsCancellationRequested)
                {
                    // Client disconnected on purpose
                    if (LogItem(LogLevel.Info)) Console.WriteLine("[CLIENT] Disconnected by client request (intentional disconnect).");
                } else {
                    // Connection was forcibly closed
                    if (LogItem(LogLevel.Info)) Console.WriteLine($"[CLIENT] Connection lost: {ex.Message}");
                    await HandleServerShutdown(DisconnectReason.ConnectionLost);
                }

            }
            catch (Exception ex)
            {
                if (LogItem(LogLevel.Info)) Console.WriteLine($"[CLIENT] Receive loop exception: {ex}");
                await HandleServerShutdown(DisconnectReason.ConnectionError);
            }
        });
    }
}