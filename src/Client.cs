using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;

namespace ArmaExtension;

public static partial class EdenOnline
{
    public static class Client
    {
        public static TcpClient? ClientListener { get; private set; }
        private static NetworkStream? _stream;
        private static Thread? _receiveThread;

        public static int? ClientId { get; private set; }

        private static string[] OtherClients { get; set; } = [];

        private static readonly ConcurrentDictionary<int, Action<object[]?>> PendingRequests = new();

        // TODO: Implement messages to client
        public static bool Connect(string host, int port, string userName, string clientHash)
        {
            try
            {
                if (ClientListener != null && ClientListener.Connected) throw new InvalidOperationException("Client is already connected.");

                ClientListener = new TcpClient();
                ClientListener.Connect(host, port);
                _stream = ClientListener.GetStream();

                // TODO: Implement Diffie-Hellman key exchange for secure communication
                

                Console.WriteLine($"Connected to server at {host}:{port}");

                _receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true
                };
                _receiveThread.Start();

                //TODO Handle sync objects, that were generated while client was connecting. Maybe request full sync after handshake?

                // Send handshake with username and client hash
                SendRequest(MessageType.ClientHandshake, [userName, clientHash], response =>
                {
                    Console.WriteLine($"Handshake response received: {string.Join(", ", response ?? [])}");
                    if (response == null || response.Length == 0) {
                        Console.WriteLine("Handshake response is null or empty.");
                        Disconnect();
                        return;
                    }

                    if (response[0] is string handshakeResponse) {
                        if (handshakeResponse == "SUCCESS")
                        {
                            Console.WriteLine("Handshake successful.");
                        }
                        else
                        {
                            Console.WriteLine($"Handshake failed: {handshakeResponse}");
                            Disconnect();
                        }
                    }

                    if (response[1] is int clientId && response[2] is object[] otherClients)
                    {
                        ClientId = clientId;
                        //TODO FIX OtherClients = otherClients;
                        Console.WriteLine($"Received ClientId: {ClientId}");
                        Console.WriteLine($"Other connected clients: {string.Join(", ", OtherClients)}");
                    } else {
                        Console.WriteLine("Unexpected handshake response format.");
                        Disconnect();
                    }
                });

                // Request object syncing after handshake
                if (ClientId != null) {
                    SendRequest(MessageType.RequestObjectSync, null, response =>
                    {
                        if (response == null) {
                            Console.WriteLine("Handshake response is null.");
                            Disconnect();
                            return;
                        }

                        //Extension.SendToArma("ObjectSync", response);
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connect exception: {ex}");
                return false;
            }
        }

        // Disconnect from server
        public static void Disconnect()
        {
            try
            {
                if (ClientListener != null && ClientListener.Connected && ClientId != null)
                {
                    // Notify server of disconnect
                    SendMessage(MessageType.ClientDisconnect);
                }

                ClientListener?.Close();
                ClientListener = null;
                _stream = null;

                _receiveThread?.Join(1000);
                _receiveThread = null;

                Console.WriteLine("Disconnected from server.");
            }
            catch { }
        }

        public static bool IsLocalServer()
        {
            return Server.IsRunning && ClientId != null;
        }

        public static void SendMessage(MessageType messageType, object[]? parameters = null)
        {
            if (ClientListener == null || _stream == null || !ClientListener.Connected) return;
            if (ClientId == null && messageType != MessageType.ClientHandshake) return;

            try
            {
                // Always include ClientId as first parameter
                object[] finalParams = parameters != null && parameters.Length > 0
                    ? new object[] { ClientId ?? -1 }.Concat(parameters).ToArray()
                    : new object[] { ClientId ?? -1 };

                string paramJson = Serializer.SerializeParameters(finalParams);
                Console.WriteLine($"Raw handshake data sent to server 1: {paramJson}");
                byte[] payload = Encoding.UTF8.GetBytes(paramJson);

                // Message = [MessageType][payload]
                byte[] msg = new byte[1 + payload.Length];
                msg[0] = (byte)messageType;
                Buffer.BlockCopy(payload, 0, msg, 1, payload.Length);

                _stream.Write(msg, 0, msg.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendMessage exception: {ex}");
            }
        }

        public static void SendRequest(MessageType messageType, object[]? parameters = null, Action<object[]?>? onResponse = null)
        {
            if (ClientListener == null || _stream == null || !ClientListener.Connected) return;

            try
            {
                // Ensure PendingRequests dictionary exists
                if (onResponse != null)
                {
                    // Generate unique request ID
                    int requestId;
                    var rnd = new Random();
                    do
                    {
                        requestId = rnd.Next(1, int.MaxValue);
                    } while (PendingRequests.ContainsKey(requestId));

                    // Prepend requestId to parameters
                    object[] finalParams;
                    if (parameters != null && parameters.Length > 0)
                    {
                        finalParams = new object[parameters.Length + 1];
                        finalParams[0] = requestId;
                        Array.Copy(parameters, 0, finalParams, 1, parameters.Length);
                    }
                    else
                    {
                        finalParams = [requestId];
                    }

                    PendingRequests[requestId] = onResponse;

                    // Send the message with requestId included
                    SendMessage(messageType, finalParams);
                }
                else
                {
                    // No response expected, just send normally
                    SendMessage(messageType, parameters);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendRequest exception: {ex}");
            }
        }

        // Receive thread: handles all incoming messages
        private static void ReceiveLoop()
        {
            if (_stream == null) return;

            byte[] buffer = new byte[8192];

            try
            {
                while (ClientListener != null && ClientListener.Connected)
                {
                    int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    // 1️⃣ First byte = message type
                    MessageType messageType = (MessageType)buffer[0];

                    // 2️⃣ Remaining bytes = payload
                    string paramData = bytesRead > 1
                        ? Encoding.UTF8.GetString(buffer, 1, bytesRead - 1)
                        : string.Empty;
                    
                    Console.WriteLine($"Received message of type {messageType} with raw data: {paramData}");

                    // 3️⃣ Deserialize parameters using custom AOT-safe method
                    object[] parameters = !string.IsNullOrEmpty(paramData)
                        ? Serializer.DeserializeParameters(paramData)
                        : Array.Empty<object>();

                    // 4️⃣ Check if message is a response to a pending request
                    if (parameters.Length > 0 && parameters[0] is int requestId)
                    {
                        if (PendingRequests.TryRemove(requestId, out var callback))
                        {
                            var responseParams = parameters.Length > 1 ? parameters[1..] : Array.Empty<object>();
                            callback?.Invoke(responseParams);
                            continue;
                        }
                    }

                    // 5️⃣ Otherwise, invoke general message handler
                    OnMessageReceived?.Invoke(messageType, parameters);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ReceiveLoop exception: {ex}");
            }
            finally
            {
                Disconnect();
            }
        }

        /// <summary>
        /// Parses a raw TCP message into MessageType and object[] parameters.
        /// </summary>
        /// <param name="data">Raw bytes from TCP stream</param>
        /// <param name="messageType">Output MessageType</param>
        /// <param name="parameters">Output object[] parameters</param>
        /// <returns>True if parse succeeded, false otherwise</returns>
        public static bool TryParseMessage(byte[] data, out MessageType messageType, out object[]? parameters)
        {
            messageType = default;
            parameters = null;

            if (data == null || data.Length < 1)
                return false; // invalid message

            try
            {
                // First byte = MessageType
                messageType = (MessageType)data[0];

                if (data.Length > 1)
                {
                    // Remaining bytes = JSON-encoded object[] parameters
                    string json = Encoding.UTF8.GetString(data, 1, data.Length - 1);

                    if (!string.IsNullOrEmpty(json))
                    {
                        try
                        {
                            parameters = JsonSerializer.Deserialize<object[]>(json);
                        }
                        catch
                        {
                            // fallback: treat entire string as single object
                            parameters = new object[] { json };
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }





        // Event / callback for handling server messages
        public static Action<MessageType, object[]?>? OnMessageReceived;
    }
}