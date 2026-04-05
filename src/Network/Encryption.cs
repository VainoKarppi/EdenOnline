using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;

using static ArmaExtension.Logger;


/*

namespace EdenOnline;

public static class Encryption
{
    // General flags
    public static bool IsEncryptionEnabled => SharedSecrets.Count > 0;

    // Single-client properties (for client-side use)
    public static byte[] ClientPrivateKey { get; private set; } = Array.Empty<byte>();
    public static byte[] ServerPublicKey { get; private set; } = Array.Empty<byte>();
    public static byte[] SharedSecretClient { get; private set; } = Array.Empty<byte>();

    // Multi-client server storage
    public class ClientKeys
    {
        public int ClientId { get; init; }
        public byte[] ClientPublicKey { get; init; } = Array.Empty<byte>();
        public byte[] ServerPublicKey { get; init; } = Array.Empty<byte>();
        public byte[] SharedSecret { get; init; } = Array.Empty<byte>();
    }

    // Map clientId -> keys & secrets
    public static ConcurrentDictionary<int, ClientKeys> SharedSecrets { get; } = new();

    #region Client-Side Methods

    /// <summary>
    /// Client-side key exchange with server.
    /// </summary>
    public static bool PerformClientKeyExchange(Connection client)
    {
        try {
            if (!client.Connected) throw new InvalidOperationException("Client must be connected.");

            Log($"[CLIENT] Starting Key Exchange with server...");
            
            using var clientDh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            // export public key as byte[] for sending
            ECDiffieHellmanPublicKey clientPubKey = clientDh.PublicKey;

            byte[] clientPublicKey = clientDh.PublicKey.ExportSubjectPublicKeyInfo();

            var stream = client.GetStream();

            // Send client public key
            byte[] lengthBytes = BitConverter.GetBytes(clientPublicKey.Length);
            stream.Write(lengthBytes);
            stream.Write(clientPublicKey);

            // Receive server public key
            byte[] serverLenBytes = new byte[4];
            stream.ReadExactly(serverLenBytes, 0, 4);
            int serverLen = BitConverter.ToInt32(serverLenBytes, 0);

            byte[] serverPublicKey = new byte[serverLen];
            int read = 0;
            while (read < serverLen)
            {
                int r = stream.Read(serverPublicKey, read, serverLen - read);
                if (r == 0) throw new Exception("Server closed connection during key exchange.");
                read += r;
            }

            ServerPublicKey = serverPublicKey;

            using var serverDhKey = ECDiffieHellman.Create();
            serverDhKey.ImportSubjectPublicKeyInfo(serverPublicKey, out _);

            SharedSecretClient = clientDh.DeriveKeyMaterial(serverDhKey.PublicKey);
            
            SharedSecrets[-1] = new ClientKeys // -1 = self
            {
                ClientId = -1,
                ClientPublicKey = clientPublicKey,
                ServerPublicKey = serverPublicKey,
                SharedSecret = SharedSecretClient
            };

            string sharedKeyPreview = BitConverter.ToString(SharedSecretClient[..Math.Min(4, SharedSecretClient.Length)]);
            Log($"[CLIENT] Key exchange complete. Shared secret: ({sharedKeyPreview})");

            return true;
        } catch (Exception ex) {
            Error($"[CLIENT] Client key exchange failed: {ex}");
            return false;
        }
    }

    #endregion

    #region Server-Side Methods

    /// <summary>
    /// Server-side key exchange for a specific client.
    /// </summary>
    public static byte[] PerformServerKeyExchange(Connection client)
    {
        if (!client.Connected) throw new InvalidOperationException("Client must be connected.");

        Log($"[SERVER] Starting Key Exchange with client {client.Id}");

        var stream = client.GetStream();
        using var serverDh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        // Receive client public key
        byte[] lenBytes = new byte[4];
        stream.ReadExactly(lenBytes, 0, 4);
        int clientLen = BitConverter.ToInt32(lenBytes, 0);

        byte[] clientPublicKey = new byte[clientLen];
        int read = 0;
        while (read < clientLen)
        {
            int r = stream.Read(clientPublicKey, read, clientLen - read);
            if (r == 0) throw new Exception("Client closed connection during key exchange.");
            read += r;
        }

        // Send server public key
        byte[] serverPublicKey = serverDh.PublicKey.ExportSubjectPublicKeyInfo();
        byte[] serverLenBytes = BitConverter.GetBytes(serverPublicKey.Length);
        stream.Write(serverLenBytes);
        stream.Write(serverPublicKey);

        // Derive shared secret
        using var clientDhKey = ECDiffieHellman.Create();
        clientDhKey.ImportSubjectPublicKeyInfo(clientPublicKey, out _);
        byte[] sharedSecret = serverDh.DeriveKeyMaterial(clientDhKey.PublicKey);

        SharedSecrets[client.Id] = new ClientKeys
        {
            ClientId = client.Id,
            ClientPublicKey = clientPublicKey,
            ServerPublicKey = serverPublicKey,
            SharedSecret = sharedSecret
        };

        string sharedKeyPreview = BitConverter.ToString(sharedSecret[..Math.Min(4, sharedSecret.Length)]);
        Log($"[SERVER] Key exchange complete. Shared secret: ({sharedKeyPreview})");

        return sharedSecret;
    }

    #endregion

    #region AES Encryption/Decryption

    public static byte[] EncryptPayload(byte[] data, int clientId = -1)
    {
        if (!IsEncryptionEnabled) return data;

        byte[] secret = clientId == -1 ? SharedSecretClient : SharedSecrets[clientId].SharedSecret;
        if (secret.Length == 0) throw new InvalidOperationException("Shared secret not established.");

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = SHA256.HashData(secret);
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        byte[] encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);

        byte[] result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);

        return result;
    }

    public static byte[] DecryptPayload(byte[] encryptedData, int clientId = -1)
    {
        byte[] secret = clientId == -1 ? SharedSecretClient : SharedSecrets[clientId].SharedSecret;
        if (secret.Length == 0) throw new InvalidOperationException("Shared secret not established.");
        if (encryptedData.Length < 16) throw new ArgumentException("Invalid encrypted data.");

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = SHA256.HashData(secret);
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        byte[] iv = encryptedData[..16];
        aes.IV = iv;
        byte[] cipher = encryptedData[16..];

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        return decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
    }

    #endregion
}

*/