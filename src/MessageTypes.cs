public enum MessageType : byte
{
    // Object operations
    ObjectCreate      = 1,
    ObjectUpdate      = 2,
    ObjectDelete      = 3,

    RequestObjectSync = 10, // Client requests full object sync from server

    // Client-server lifecycle
    ClientConnect     = 100,  // Client informs server it wants to connect
    ClientHandshake   = 101,  // Handshake / mod/version check
    ClientDisconnect  = 102,  // Client voluntarily disconnecting

    // Server lifecycle
    ServerShutdown    = 255  // Server shutting down
}