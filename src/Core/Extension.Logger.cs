using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EdenOnline;

/// <summary>
/// Microsoft.Extensions.Logging facade for the Core subsystem.
///
/// Hosts inject an <see cref="ILoggerFactory"/> via <see cref="Configure"/>;
/// until then a <see cref="NullLogger"/> is used. The static API mirrors the
/// previous hand-rolled Logger so existing call sites keep working, while all
/// output now flows through MEL to a per-subsystem log file.
/// </summary>
public static class Logger {
    /// <summary>Toggle writing to the log file. (Default is True)</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>Kept for backwards compatibility; console output is not used in the Arma host.</summary>
    public static bool LogToConsole { get; set; } = true;

    public static string[] BlacklistedWords { get; set; } = ["CameraUpdate"];

    public static LogLevel CurrentLogLevel { get; set; } = LogLevel.Info;

    private static ILoggerFactory _factory = NullLoggerFactory.Instance;
    private static ILogger _logger = NullLogger.Instance;

    public enum LogLevel {
        Debug,
        Info,
        Warning,
        Error
    }

    /// <summary>Injects the Core subsystem logger factory.</summary>
    public static void Configure(ILoggerFactory factory) {
        _factory = factory ?? NullLoggerFactory.Instance;
        _logger = _factory.CreateLogger("Core");
    }

    private static Microsoft.Extensions.Logging.LogLevel ToMel(LogLevel level) => level switch {
        LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
        LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
        LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
        LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
        _ => Microsoft.Extensions.Logging.LogLevel.Information
    };

    /// <summary>
    /// Used to create a log entry.
    /// </summary>
    /// <param name="text">A text to be logged</param>
    /// <param name="level">The log level</param>
    /// <param name="forcePrintConsole">Ignored; kept for API compatibility.</param>
    public static void Log(object? text = null, LogLevel level = LogLevel.Info, bool forcePrintConsole = false) {
        if (text == null || !Enabled) return;

        if (level < CurrentLogLevel) return;

        string message = text.ToString() ?? string.Empty;

        if (BlacklistedWords.Any(word => message.Contains(word, StringComparison.OrdinalIgnoreCase))) return;

        _logger.Log(ToMel(level), "{Message}", message);
    }

    public static void Debug(object? text, bool forcePrintConsole = false) {
        Log(text, LogLevel.Debug, forcePrintConsole);
    }

    public static void Error(object? text, bool forcePrintConsole = false) {
        Log(text, LogLevel.Error, forcePrintConsole);
    }

    public static void Warning(object? text, bool forcePrintConsole = false) {
        Log(text, LogLevel.Warning, forcePrintConsole);
    }
}
