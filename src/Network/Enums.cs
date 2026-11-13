

namespace DynTypeNetwork;


/// <summary>
/// Identifies which half of DynTypeNetwork is executing: the connecting client
/// or the accepting server. Both can run in the same process (listen server).
/// </summary>
public enum NetworkSide
{
    Client,
    Server
}

public enum HandshakeFailureReason
{
    Unknown,
    VersionMismatch,
    InvalidHash,
    ServerFull,
    Timeout,
    RejectedByServer,
    AuthenticationFailed
}

public enum DisconnectReason
{
    Unknown,
    ServerShutdown,
    ConnectionLost,
    ConnectionTimeout,
    ConnectionError,
    ClientDisconnect
}