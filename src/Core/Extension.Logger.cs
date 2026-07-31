using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;

namespace ArmaExtension;

/// <summary>
/// Logger class for logging messages to a file and optionally to the console.
/// </summary>
public static class Logger {
    /// <summary>Toggle writing to external .log file. Creates a Logs folder in executing assembly path. (Default is True)</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>Toggle writing to console. (Default is False)</summary>
    public static bool LogToConsole { get; set; } = true;

    private static Thread? writerThread;
    private static readonly ConcurrentQueue<string> Texts = new();
    private static string? logFile;

    private static readonly AutoResetEvent LogEvent = new(false);
    private static volatile bool running;

    public static string[] BlacklistedWords { get; set; } = ["CameraUpdate"];

    private static void WriterThread()
    {
        try
        {
            Debug("Starting WriterThread...");

            string logFolder = Path.Combine(Path.GetDirectoryName(Extension.AssemblyDirectory) ?? string.Empty, $"{Extension.ExtensionName}_Logs");

            Directory.CreateDirectory(logFolder);

            logFile ??= Path.Combine(logFolder, $"Log_{DateTime.Now:yyyy-MM-dd-HH_mm_ss}.log");

            running = true;

            using var writer = new StreamWriter(logFile, append: true) { AutoFlush = false };

            while (running) {
                // Wait until there is something to write.
                LogEvent.WaitOne();

                while (Texts.TryDequeue(out var text)) {
                    // Skip blacklisted words
                    if (BlacklistedWords.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase))) continue;
                    writer.WriteLine(text);
                }

                writer.Flush();
            }

            // Flush any remaining entries before exiting.
            while (Texts.TryDequeue(out var text)) {
                writer.WriteLine(text);
            }

            writer.Flush();
        } catch (Exception ex) {
            Console.Error.WriteLine($"Logger WriterThread: {ex}");
        }
    }

    /// <summary>Closes writer thread</summary>
    public static void CloseWriter()
    {
        running = false;
        LogEvent.Set(); // Wake the thread so it can exit.
        writerThread?.Join();
        writerThread = null;
    }

    /// <summary>
    /// Used to create a log entry.
    /// </summary>
    /// <param name="text">A text to be logged</param>
    /// <param name="forcePrintConsole">Print the message directly in to the Console</param>
    public static void Log(object? text = null, LogLevel level = LogLevel.Info, bool forcePrintConsole = false) {
        if (text == null || !Enabled) return;

        // Check log level
        if (level < CurrentLogLevel) return;
        
        string time = DateTime.Now.ToString("HH:mm:ss.fff");
        string logText = $"{time} | [{level}] {text}";

        if (forcePrintConsole || LogToConsole) Console.WriteLine(logText);
        
        Texts.Enqueue(logText);
        LogEvent.Set();

        if (writerThread == null) {
            writerThread = new Thread(WriterThread) { IsBackground = true };
            writerThread.Start();
        }
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

    public static LogLevel CurrentLogLevel { get; set; } = LogLevel.Info;

    public enum LogLevel {
        Debug,
        Info,
        Warning,
        Error
    }
}