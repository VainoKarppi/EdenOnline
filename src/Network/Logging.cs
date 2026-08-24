using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DynTypeNetwork;

/// <summary>
/// Injected logging facade for the DynTypeNetwork library.
///
/// DynTypeNetwork is a standalone, self-contained package with no opinion
/// about how (or whether) logging is produced. Consumers call
/// <see cref="Configure"/> to inject their own <see cref="ILoggerFactory"/>;
/// until then a <see cref="NullLogger"/> is used so the library can be dropped
/// into any project without a logging dependency.
/// </summary>
public static class Log
{
    private static ILoggerFactory _factory = NullLoggerFactory.Instance;
    private static ILogger _logger = NullLogger.Instance;

    public static void Configure(ILoggerFactory factory)
    {
        _factory = factory ?? NullLoggerFactory.Instance;
        _logger = _factory.CreateLogger("Network");
        _logger.LogInformation("DynTypeNetwork logging initialized.");
    }

    public static void Debug(string? message) => _logger.LogDebug("{Message}", message);

    public static void Info(string? message) => _logger.LogInformation("{Message}", message);

    public static void Warning(string? message) => _logger.LogWarning("{Message}", message);

    public static void Error(string? message) => _logger.LogError("{Message}", message);

    public static void Error(Exception ex, string? message = null) => _logger.LogError(ex, "{Message}", message ?? ex.Message);
}
