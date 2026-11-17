using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace EdenOnline.Logging;

/// <summary>
/// Minimal file-based <see cref="ILoggerProvider"/> for Microsoft.Extensions.Logging.
/// Writes one formatted line per log entry. Thread-safe, reflection-free and safe
/// for NativeAOT/trimming builds. Each provider instance writes to a single file,
/// so hosts create one provider per subsystem to get per-folder log files.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly LogLevel _minLevel;
    private readonly object _gate = new();
    private StreamWriter? _writer;

    public FileLoggerProvider(string filePath, LogLevel minLevel = LogLevel.Trace)
    {
        _filePath = filePath;
        _minLevel = minLevel;

        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    public ILogger CreateLogger(string categoryName)
        => new FileLogger(this, categoryName, _minLevel);

    internal void Write(LogLevel level, string categoryName, string message)
    {
        if (_writer == null)
        {
            lock (_gate)
            {
                _writer ??= new StreamWriter(
                    new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                    new UTF8Encoding(false))
                {
                    AutoFlush = true
                };
            }
        }

        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {ShortLevel(level)} | [{categoryName}] {message}";

        lock (_gate)
        {
            _writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    private static string ShortLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO ",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRIT ",
        _ => "INFO "
    };

    private sealed class FileLogger(FileLoggerProvider provider, string categoryName, LogLevel minLevel) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
            => logLevel >= minLevel && logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            string message = formatter(state, exception);
            if (exception != null)
                message += $"{Environment.NewLine}{exception}";

            provider.Write(logLevel, categoryName, message);
        }
    }
}
