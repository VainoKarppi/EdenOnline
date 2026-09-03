using System;
using System.IO;

namespace EdenOnline;

public static partial class Extension {
    private static bool _loggingInitialized;
    public static bool LoggingEnabled { get; set; } = true;
    public static string LogFolder => Path.Combine(Path.GetDirectoryName(AssemblyDirectory) ?? AppContext.BaseDirectory, $"{ExtensionName}_Logs");

    /// <summary>
    /// Initializes the Core subsystem's own file-backed logger. Core is
    /// standalone and only wires up its own <see cref="Logger"/>; other
    /// subsystems (ArmaPlugin, DynTypeSerializer, Network) are configured by
    /// the host that composes them.
    /// </summary>
    internal static void InitializeLogging() {
        if (_loggingInitialized) return;
        _loggingInitialized = true;

        // Remove all existing log files before starting new loggers.
        if (Directory.Exists(LogFolder)) {
            foreach (string file in Directory.EnumerateFiles(LogFolder)) {
                try {
                    File.Delete(file);
                } catch (Exception ex) {
                    Logger.Debug($"Failed to delete log file '{file}': {ex.Message}");
                }
            }
        } else {
            Directory.CreateDirectory(LogFolder);
        }

        Logger.Configure(Logging.LogFactory.CreateFileLoggerFactory(LogFolder, "Core.log"));
    }
}

