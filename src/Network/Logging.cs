using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DynTypeNetwork;

/// <summary>
/// Injected logging facade for the DynTypeNetwork library.
///
/// DynTypeNetwork is a standalone, self-contained package with no opinion
/// about how (or whether) logging is produced. Consumers call
/// <see cref="Configure(NetworkSide, ILoggerFactory)"/> to inject their own
/// <see cref="ILoggerFactory"/>; until then a <see cref="NullLogger"/> is used
/// so the library can be dropped into any project without a logging dependency.
///
/// A listen server runs the client and the server half in the same process, so
/// each side has its own logger. That lets the host route them to separate
/// files (DynTypeNetwork_Client.log / DynTypeNetwork_Server.log) and enable or
/// disable either side independently.
/// </summary>
public static class Log
{
    /// <summary>Toggle logging for client-side network code. (Default is True)</summary>
    public static bool ClientEnabled { get; set; } = true;

    /// <summary>Toggle logging for server-side network code. (Default is True)</summary>
    public static bool ServerEnabled { get; set; } = true;

    private static ILogger _clientLogger = NullLogger.Instance;
    private static ILogger _serverLogger = NullLogger.Instance;

    /// <summary>Log writer for client-side network code.</summary>
    public static NetworkLogger Client => new(NetworkSide.Client);

    /// <summary>Log writer for server-side network code.</summary>
    public static NetworkLogger Server => new(NetworkSide.Server);

    /// <summary>Log writer for the given side. Use when the side is only known at runtime.</summary>
    public static NetworkLogger For(NetworkSide side) => new(side);

    public static void ConfigureClient(ILoggerFactory factory) => Configure(NetworkSide.Client, factory);

    public static void ConfigureServer(ILoggerFactory factory) => Configure(NetworkSide.Server, factory);

    /// <summary>Injects the logger factory used by the given side.</summary>
    public static void Configure(NetworkSide side, ILoggerFactory factory)
    {
        factory ??= NullLoggerFactory.Instance;

        ILogger logger = factory.CreateLogger(side == NetworkSide.Server ? "DynTypeNetwork.Server" : "DynTypeNetwork.Client");

        if (side == NetworkSide.Server) _serverLogger = logger;
        else _clientLogger = logger;

        For(side).Info($"DynTypeNetwork {side} logging initialized.");
    }

    public static bool IsEnabled(NetworkSide side) => side == NetworkSide.Server ? ServerEnabled : ClientEnabled;

    internal static void Write(NetworkSide side, LogLevel level, string? message, Exception? exception = null)
    {
        if (!IsEnabled(side)) return;

        ILogger logger = side == NetworkSide.Server ? _serverLogger : _clientLogger;
        logger.Log(level, exception, "{Message}", message);
    }
}

/// <summary>
/// Writes log entries for one side of DynTypeNetwork. Obtained from
/// <see cref="Log.Client"/>, <see cref="Log.Server"/> or <see cref="Log.For"/>.
/// </summary>
public readonly struct NetworkLogger
{
    internal NetworkLogger(NetworkSide side) => Side = side;

    public NetworkSide Side { get; }

    public void Debug(string? message) => Log.Write(Side, LogLevel.Debug, message);

    public void Info(string? message) => Log.Write(Side, LogLevel.Information, message);

    public void Warning(string? message) => Log.Write(Side, LogLevel.Warning, message);

    public void Error(string? message) => Log.Write(Side, LogLevel.Error, message);

    public void Error(Exception ex, string? message = null) => Log.Write(Side, LogLevel.Error, message ?? ex.Message, ex);
}