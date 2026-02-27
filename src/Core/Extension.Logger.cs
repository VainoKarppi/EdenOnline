using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using static ArmaExtension.Logger;
using static ArmaExtension.Enums;
using static ArmaExtension.MethodSystem;
using static ArmaExtension.Events;

namespace ArmaExtension;

/// <summary>
/// Logger class for logging messages to a file and optionally to the console.
/// </summary>
public static class Logger {
    /// <summary>Toggle writing to external .log file. Creates a Logs folder in executing assembly path. (Default is True)</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>Toggle writing to console. (Default is False)</summary>
    public static bool LogToConsole { get; set; } = true;


    private static readonly Lock lockObject = new();
    private static Thread? writerThread;
    private static readonly ConcurrentQueue<string> Texts = new();
    private static string? logFile;

    private static void WriterThread() {
        try {
            Debug("Starting WriterThread...");
            string dir = Extension.AssemblyDirectory;
            string logFolder = Path.Combine(Path.GetDirectoryName(dir) ?? string.Empty, $"{Extension.ExtensionName}_Logs");

            if (!Directory.Exists(logFolder)) Directory.CreateDirectory(logFolder);

            logFile ??= Path.Combine(logFolder, $"Log_{DateTime.Now:yyyy-MM-dd-HH_mm_ss}.log");

            while (writerThread != null) {
                if (!Texts.IsEmpty) {
                    try {
                        using StreamWriter writer = new(logFile, true);
                        while (Texts.TryDequeue(out string? text)) {
                            writer.WriteLine(text);
                        }
                        writer.Flush();
                    } catch (Exception ex) {
                        Texts.Enqueue(ex.Message);
                    }
                }

                Thread.Sleep(5);
            }
        } catch (Exception ex) {
            Error($"WriterThread: {ex.Message}");
        }
    }

    /// <summary>Closes writer thread</summary>
    public static void CloseWriter() {
        writerThread = null;
    }

    /// <summary>
    /// Used to create a log entry.
    /// </summary>
    /// <param name="text">A text to be logged</param>
    /// <param name="forcePrintConsole">Print the message directly in to the Console</param>
    public static void Log(object? text, LogLevel level = LogLevel.Info, bool forcePrintConsole = false) {
        if (text == null || !Enabled) return;

        // Check log level
        if (level < CurrentLogLevel) return;
        
        string time = DateTime.Now.ToString("HH:mm:ss.fff");
        string logText = $"{time} | [{level}] {text}";

        if (forcePrintConsole || LogToConsole) Console.WriteLine(logText);
        
        lock (lockObject) Texts.Enqueue(logText);

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