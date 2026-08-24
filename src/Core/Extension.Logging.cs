using System;
using System.IO;

namespace EdenOnline;

public static partial class Extension {
    private static bool _loggingInitialized;

    /// <summary>
    /// Initializes the Core subsystem's own file-backed logger. Core is
    /// standalone and only wires up its own <see cref="Logger"/>; other
    /// subsystems (ArmaPlugin, DynTypeSerializer, Network) are configured by
    /// the host that composes them.
    /// </summary>
    internal static void InitializeLogging() {
        if (_loggingInitialized) return;
        _loggingInitialized = true;

        string baseDir = Path.GetDirectoryName(AssemblyDirectory) ?? AppContext.BaseDirectory;
        string logsDir = Path.Combine(baseDir, $"{ExtensionName}_Logs");
        Directory.CreateDirectory(logsDir);

        Logger.Configure(
            Logging.LogFactory.CreateFileLoggerFactory(logsDir, "Core.log"));
    }
}

