namespace EdenOnline;

/// <summary>
/// Runtime configuration flags for the EdenOnline extension.
/// </summary>
public static class Settings
{
    /// <summary>
    /// Development-only flag for solo testing. Enables mirror mode so the sender also receives its own messages.
    /// </summary>
    public const bool MIRROR = false;

    /// <summary>
    /// Allow the same client to connect to both server and client at the same time, for testing purposes.
    /// </summary>
    public const bool ALLOW_DUAL_CONNECTIONS = false;
}
