using System;
using NLog;

namespace AuroraFlasher.Logging
{
    /// <summary>
    /// Static logger wrapper for NLog.
    /// Provides simple logging methods for all levels.
    /// </summary>
    public static class Logger
    {
        private static readonly NLog.Logger _logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Logs a trace message (most detailed logging level).
        /// Use for detailed SPI commands and low-level operations.
        /// </summary>
        public static void Trace(string message)
        {
            _logger.Trace(message);
        }

        /// <summary>
        /// Logs a trace message with exception details.
        /// </summary>
        public static void Trace(Exception ex, string message)
        {
            _logger.Trace(ex, message);
        }

        /// <summary>
        /// Logs a debug message.
        /// Use for operation details and debugging information.
        /// </summary>
        public static void Debug(string message)
        {
            _logger.Debug(message);
        }

        /// <summary>
        /// Logs a debug message with exception details.
        /// </summary>
        public static void Debug(Exception ex, string message)
        {
            _logger.Debug(ex, message);
        }

        /// <summary>
        /// Logs an informational message.
        /// Use for successful operations and important events.
        /// </summary>
        public static void Info(string message)
        {
            _logger.Info(message);
        }

        /// <summary>
        /// Logs an informational message with exception details.
        /// </summary>
        public static void Info(Exception ex, string message)
        {
            _logger.Info(ex, message);
        }

        /// <summary>
        /// Logs a warning message.
        /// Use for non-critical issues that should be noted.
        /// </summary>
        public static void Warn(string message)
        {
            _logger.Warn(message);
        }

        /// <summary>
        /// Logs a warning message with exception details.
        /// </summary>
        public static void Warn(Exception ex, string message)
        {
            _logger.Warn(ex, message);
        }

        /// <summary>
        /// Logs an error message.
        /// Use for operation failures and recoverable errors.
        /// </summary>
        public static void Error(string message)
        {
            _logger.Error(message);
        }

        /// <summary>
        /// Logs an error message with exception details.
        /// </summary>
        public static void Error(Exception ex, string message)
        {
            _logger.Error(ex, message);
        }

        /// <summary>
        /// Logs a fatal error message.
        /// Use for unrecoverable errors and hardware failures.
        /// </summary>
        public static void Fatal(string message)
        {
            _logger.Fatal(message);
        }

        /// <summary>
        /// Logs a fatal error message with exception details.
        /// </summary>
        public static void Fatal(Exception ex, string message)
        {
            _logger.Fatal(ex, message);
        }

        /// <summary>
        /// Gets a logger instance for a specific class.
        /// </summary>
        /// <param name="name">The logger name (typically the class name).</param>
        /// <returns>A logger instance.</returns>
        public static NLog.Logger GetLogger(string name)
        {
            return LogManager.GetLogger(name);
        }

        /// <summary>
        /// Gets a logger instance for a specific type.
        /// </summary>
        /// <typeparam name="T">The type to create a logger for.</typeparam>
        /// <returns>A logger instance.</returns>
        public static NLog.Logger GetLogger<T>()
        {
            return LogManager.GetLogger(typeof(T).FullName);
        }

        /// <summary>
        /// Flushes any pending log messages.
        /// Call this before application shutdown to ensure all logs are written.
        /// </summary>
        public static void Flush()
        {
            LogManager.Flush();
        }

        /// <summary>
        /// Shuts down the logging system.
        /// Call this during application cleanup.
        /// </summary>
        public static void Shutdown()
        {
            LogManager.Shutdown();
        }
    }
}
