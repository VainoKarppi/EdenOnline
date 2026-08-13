

namespace DynTypeNetwork;


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