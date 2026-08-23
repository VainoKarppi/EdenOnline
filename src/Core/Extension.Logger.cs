using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace EdenOnline;

/// <summary>
/// Logger class for logging messages to a file and optionally to the console.
/// </summary>
public static class Logger {
    /// <summary>Toggle writing to external .log file. Creates a Logs folder in executing assembly path. (Default is True)</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>Toggle writing to console. (Default is False)</summary>
    public static bool LogToConsole { get; set; } = false;

    private static readonly object WriterStateLock = new();
    private static Thread? writerThread;
    private static readonly ConcurrentQueue<string> Texts = new();
    private static string? logFile;

    private static readonly AutoResetEvent LogEvent = new(false);
    private static volatile bool stopRequested;

    public static string[] BlacklistedWords { get; set; } = ["CameraUpdate"];

    internal static bool IsEnabled(LogLevel level) => Enabled && level >= CurrentLogLevel;

    private static void WriterThread()
    {
        try
        {
            string logFolder = Path.Combine(Path.GetDirectoryName(Extension.AssemblyDirectory) ?? string.Empty, $"{Extension.ExtensionName}_Logs");

            Directory.CreateDirectory(logFolder);

            logFile ??= Path.Combine(logFolder, $"Log_{DateTime.Now:yyyy-MM-dd-HH_mm_ss}.log");

            using var writer = new StreamWriter(logFile, append: true) { AutoFlush = false };

            while (true) {
                LogEvent.WaitOne();

                while (Texts.TryDequeue(out string? text))
                    writer.WriteLine(text);

                writer.Flush();

                if (stopRequested && Texts.IsEmpty) break;
            }
        } catch (Exception ex) {
            Console.Error.WriteLine($"Logger WriterThread: {ex}");
        }
    }

    private static void EnsureWriterStarted() {
        lock (WriterStateLock) {
            if (writerThread is { IsAlive: true }) return;

            stopRequested = false;
            writerThread = new Thread(WriterThread) {
                IsBackground = true,
                Name = "EdenOnline.LogWriter"
            };
            writerThread.Start();
        }
    }

    /// <summary>Closes writer thread after flushing queued entries.</summary>
    public static void CloseWriter()
    {
        Thread? thread;
        lock (WriterStateLock) {
            thread = writerThread;
            if (thread == null) return;

            stopRequested = true;
            LogEvent.Set();
        }

        thread.Join();

        lock (WriterStateLock) {
            if (ReferenceEquals(writerThread, thread)) writerThread = null;
            stopRequested = false;
        }
    }

    /// <summary>
    /// Used to create a log entry.
    /// </summary>
    /// <param name="text">A text to be logged</param>
    /// <param name="forcePrintConsole">Print the message directly in to the Console</param>
    public static void Log(object? text = null, LogLevel level = LogLevel.Info, bool forcePrintConsole = false) {
        if (text == null || !IsEnabled(level)) return;

        string logText = $"{DateTime.Now:HH:mm:ss.fff} | [{level}] {text}";

        if (forcePrintConsole || LogToConsole) Console.WriteLine(logText);

        // Preserve console output for blacklisted entries, but avoid queueing
        // and waking the writer for messages that will never reach the file.
        if (BlacklistedWords.Any(word => logText.Contains(word, StringComparison.OrdinalIgnoreCase))) return;

        Texts.Enqueue(logText);
        EnsureWriterStarted();
        LogEvent.Set();
    }

    public static void Debug(object? text, bool forcePrintConsole = false) {
        Log(text, LogLevel.Debug, forcePrintConsole);
    }

    public static void Debug(ref DebugInterpolatedStringHandler text, bool forcePrintConsole = false) {
        if (!text.IsEnabled) return;
        Log(text.GetFormattedText(), LogLevel.Debug, forcePrintConsole);
    }

    public static void Error(object? text, bool forcePrintConsole = false) {
        Log(text, LogLevel.Error, forcePrintConsole);
    }

    public static void Warning(object? text, bool forcePrintConsole = false) {
        Log(text, LogLevel.Warning, forcePrintConsole);
    }

    public static LogLevel CurrentLogLevel { get; set; } = LogLevel.Info;

    public enum LogLevel {
        Debug,
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Prevents interpolation expressions from running when Debug logging is
    /// disabled or filtered by the current level.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct DebugInterpolatedStringHandler {
        private DefaultInterpolatedStringHandler inner;

        public bool IsEnabled { get; }

        public DebugInterpolatedStringHandler(int literalLength, int formattedCount, out bool shouldAppend) {
            IsEnabled = shouldAppend = Logger.IsEnabled(LogLevel.Debug);
            inner = shouldAppend
                ? new DefaultInterpolatedStringHandler(literalLength, formattedCount)
                : default;
        }

        public void AppendLiteral(string value) => inner.AppendLiteral(value);
        public void AppendFormatted<T>(T value) => inner.AppendFormatted(value);
        public void AppendFormatted<T>(T value, string? format) => inner.AppendFormatted(value, format);
        public void AppendFormatted<T>(T value, int alignment) => inner.AppendFormatted(value, alignment);
        public void AppendFormatted<T>(T value, int alignment, string? format) => inner.AppendFormatted(value, alignment, format);
        public void AppendFormatted(string? value) => inner.AppendFormatted(value);
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => inner.AppendFormatted(value, alignment, format);

        internal string GetFormattedText() => inner.ToStringAndClear();
    }
}
