using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EdenOnline;

/// <summary>
/// Microsoft.Extensions.Logging facade for the ArmaPlugin subsystem.
///
/// Hosts inject an <see cref="ILoggerFactory"/> via <see cref="Configure"/>;
/// until then a <see cref="NullLogger"/> is used. The static API mirrors
/// <see cref="Logger"/> so plugin code can use the same style, while output
/// flows through MEL to a dedicated per-subsystem log file.
/// </summary>
public static class ArmaLog {
    public static bool Enabled { get; set; } = true;

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

    /// <summary>Injects the ArmaPlugin subsystem logger factory.</summary>
    public static void Configure(ILoggerFactory factory) {
        _factory = factory ?? NullLoggerFactory.Instance;
        _logger = _factory.CreateLogger("ArmaPlugin");
    }

    private static Microsoft.Extensions.Logging.LogLevel ToMel(LogLevel level) => level switch {
        LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
        LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
        LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
        LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
        _ => Microsoft.Extensions.Logging.LogLevel.Information
    };

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
