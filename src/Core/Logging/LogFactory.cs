using System.IO;
using Microsoft.Extensions.Logging;

namespace EdenOnline.Logging;

/// <summary>
/// Helpers for building file-backed <see cref="ILoggerFactory"/> instances for
/// the Core subsystem. Uses <c>LoggerFactory.Create</c>, which is the
/// AOT/trimming-safe path.
/// </summary>
public static class LogFactory
{
    public static ILoggerFactory CreateFileLoggerFactory(string folderPath, string fileName, LogLevel minLevel = LogLevel.Trace)
    {
        Directory.CreateDirectory(folderPath);

        string filePath = Path.Combine(folderPath, fileName);

        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minLevel);
            builder.AddProvider(new FileLoggerProvider(filePath, minLevel));
        });
    }
}
