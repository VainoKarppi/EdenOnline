

namespace DynTypeNetwork;

public static class Settings
{
    public static class Logging
    {
        public enum LogLevel
        {
            Debug = 0,
            Info = 1,
            Warning = 2,
            Error = 3
        }

        public static LogLevel CurrentLogLevel { get; set; } = LogLevel.Info;

        /// <summary>
        /// Returns true if the specified log level should be written
        /// with the currently configured log level.
        /// </summary>
        public static bool LogItem(LogLevel level)
        {
            return level >= CurrentLogLevel;
        }
    }
}