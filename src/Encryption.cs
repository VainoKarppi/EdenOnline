using System;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace ArmaExtension;

public static partial class EdenOnline
{
    public static byte[] PerformClientKeyExchange(TcpClient client)
    {
        if (!client.Connected) throw new InvalidOperationException("Client must be connected to perform key exchange.");

        var stream = client.GetStream();

        try
        {
            // 1️⃣ Generate ECDH key pair (P-256 curve)
            using var clientDh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

            // 2️⃣ Export public key in standard SubjectPublicKeyInfo format
            byte[] clientPublicKey = clientDh.PublicKey.ExportSubjectPublicKeyInfo();

            // 3️⃣ Send client public key length + public key
            byte[] keyLengthBytes = BitConverter.GetBytes(clientPublicKey.Length);
            stream.Write(keyLengthBytes, 0, keyLengthBytes.Length);
            stream.Write(clientPublicKey, 0, clientPublicKey.Length);

            // 4️⃣ Receive server public key length + public key
            byte[] serverKeyLengthBytes = new byte[4];
            int read = stream.Read(serverKeyLengthBytes, 0, 4);
            if (read != 4) throw new Exception("Failed to read server key length.");
            int serverKeyLength = BitConverter.ToInt32(serverKeyLengthBytes, 0);

            byte[] serverPublicKey = new byte[serverKeyLength];
            int totalRead = 0;
            while (totalRead < serverKeyLength)
            {
                int r = stream.Read(serverPublicKey, totalRead, serverKeyLength - totalRead);
                if (r == 0) throw new Exception("Server closed connection during key exchange.");
                totalRead += r;
            }

            // 5️⃣ Import server public key from SubjectPublicKeyInfo
            using var serverDhKey = ECDiffieHellman.Create();
            serverDhKey.ImportSubjectPublicKeyInfo(serverPublicKey, out _);

            // 6️⃣ Derive shared secret
            byte[] sharedSecret = clientDh.DeriveKeyMaterial(serverDhKey.PublicKey);

            Console.WriteLine($"CLIENT: Key exchange complete. Shared secret length: {string.Join(",", sharedSecret.Take(10))} bytes");

            return sharedSecret;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Key exchange failed: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Handles the client handshake including key exchange and returns the shared secret.
    /// </summary>
    public static byte[] HandleClientHandshakeWithKeyExchange(TcpClient client)
    {
        if (client == null || !client.Connected) throw new InvalidOperationException("Invalid client.");

        Console.WriteLine("Handling client key exchange...");

        // Perform ECDH key exchange first
        byte[] sharedSecret = PerformClientKeyExchange(client);
        Console.WriteLine($"SERVER: Shared secret established ({string.Join(",", sharedSecret.Take(10))} bytes).");

        // 1️⃣ Now read the handshake parameters (userName, modsHash, requestId, etc.)
        var stream = client.GetStream();
        byte[] buffer = new byte[4096];
        int read = stream.Read(buffer, 0, buffer.Length);
        if (read == 0) throw new Exception("No handshake data received.");

        string data = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
        object[] handshakeParams = Serializer.DeserializeParameters(data);

        Console.WriteLine($"Received handshake parameters: {string.Join(", ", handshakeParams)}");

        return sharedSecret;
    }

    public static byte[] EncryptPayload(byte[] data, byte[]? sharedSecret)
    {
        if (sharedSecret == null) throw new InvalidOperationException("Shared secret is null.");

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = SHA256.HashData(sharedSecret); // derive 256-bit key
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        aes.GenerateIV();
        byte[] iv = aes.IV;

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        byte[] encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);

        // Prepend IV
        byte[] result = new byte[iv.Length + encrypted.Length];
        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
        Buffer.BlockCopy(encrypted, 0, result, iv.Length, encrypted.Length);
        return result;
    }

    /// <summary>
    /// Decrypts data using AES-CBC with a derived key from the shared secret.
    /// Expects IV to be prepended to the ciphertext.
    /// </summary>
    public static byte[] DecryptPayload(byte[] encryptedData, byte[]? sharedSecret)
    {
        if (sharedSecret == null) throw new InvalidOperationException("Shared secret is null.");
        if (encryptedData.Length < 16) throw new ArgumentException("Invalid encrypted data.");

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = SHA256.HashData(sharedSecret);
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        // Extract IV
        byte[] iv = new byte[16];
        Buffer.BlockCopy(encryptedData, 0, iv, 0, 16);
        aes.IV = iv;

        int cipherLength = encryptedData.Length - 16;
        byte[] cipher = new byte[cipherLength];
        Buffer.BlockCopy(encryptedData, 16, cipher, 0, cipherLength);

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        return decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
    }
}