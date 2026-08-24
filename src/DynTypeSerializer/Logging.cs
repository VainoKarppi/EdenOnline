using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DynTypeSerializer.Logging;

/// <summary>
/// Injected logging facade for DynTypeSerializer.
///
/// Hosts call <see cref="Configure"/> with their own <see cref="ILogger"/>.
/// When not configured, a <see cref="NullLogger"/> is used so the serializer can
/// be dropped into any project without a logging dependency on the host.
/// The log level is controlled entirely by the injected logger.
/// </summary>
public static class SerializerLogging
{
    private static ILogger _logger = NullLogger.Instance;

    public static void Configure(ILogger logger)
    {
        _logger = logger ?? NullLogger.Instance;
        _logger.LogInformation("DynTypeSerializer logging initialized.");
    }

    public static void Debug(string? message) => _logger.LogDebug("{Message}", message);

    public static void Info(string? message) => _logger.LogInformation("{Message}", message);

    public static void Warning(string? message) => _logger.LogWarning("{Message}", message);

    public static void Error(string? message) => _logger.LogError("{Message}", message);

    public static void Error(Exception ex, string? message = null) => _logger.LogError(ex, "{Message}", message ?? ex.Message);
}

