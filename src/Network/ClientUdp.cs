


using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using ArmaExtension;
using static ArmaExtension.Logger;


namespace EdenOnline;


public static class ClientUdp {

    private static UdpClient? _udpClient;
    private static IPEndPoint? _udpServerEndpoint;
    private static Thread? _udpReceiveThread;

    private static void StartUdpClient(string host, int port)
    {
        if (_udpClient != null) return;

        _udpClient = new UdpClient();
        _udpServerEndpoint = new IPEndPoint(IPAddress.Parse(host), port);



        _udpReceiveThread = new Thread(UdpReceiveLoop) { IsBackground = true };
        _udpReceiveThread.Start();

        Console.WriteLine("[CLIENT] UDP connected & registered.");
    }

    public static void SendUdpMessage(string message)
    {
        if (_udpClient == null || _udpServerEndpoint == null) return;

        byte[] data = Encoding.UTF8.GetBytes(message);
        _udpClient.Send(data, data.Length, _udpServerEndpoint);
    }

    private static void UdpReceiveLoop()
    {
        if (_udpClient == null) return;

        IPEndPoint remote = new(IPAddress.Any, 0);

        try
        {
            while (true)
            {
                byte[] data = _udpClient.Receive(ref remote);
                string message = Encoding.UTF8.GetString(data);

                Extension.SendToArma("CameraUpdateReceived", [message]);
            }
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            Error($"UDP receive error: {ex.Message}");
        }
    }
}