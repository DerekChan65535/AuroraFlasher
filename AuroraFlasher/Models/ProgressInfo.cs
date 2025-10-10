using System;

namespace AuroraFlasher.Models
{
    /// <summary>
    /// Progress information for long-running operations
    /// </summary>
    public class ProgressInfo
    {
        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// Bytes processed
        /// </summary>
        public long BytesProcessed { get; set; }

        /// <summary>
        /// Total bytes
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// Current status message
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Operation start time
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Current time
        /// </summary>
        public DateTime CurrentTime { get; set; }

        /// <summary>
        /// Estimated time remaining
        /// </summary>
        public TimeSpan EstimatedTimeRemaining
        {
            get
            {
                if (BytesProcessed == 0 || Percentage == 0)
                    return TimeSpan.Zero;

                var elapsed = CurrentTime - StartTime;
                var totalEstimated = TimeSpan.FromSeconds(elapsed.TotalSeconds / Percentage * 100);
                var remaining = totalEstimated - elapsed;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Transfer speed (bytes per second)
        /// </summary>
        public double Speed
        {
            get
            {
                var elapsed = (CurrentTime - StartTime).TotalSeconds;
                return elapsed > 0 ? BytesProcessed / elapsed : 0;
            }
        }

        public ProgressInfo()
        {
            StartTime = DateTime.Now;
            CurrentTime = DateTime.Now;
            Status = string.Empty;
        }

        public ProgressInfo(long bytesProcessed, long totalBytes, string status = null)
        {
            StartTime = DateTime.Now;
            CurrentTime = DateTime.Now;
            BytesProcessed = bytesProcessed;
            TotalBytes = totalBytes;
            Percentage = totalBytes > 0 ? (double)bytesProcessed / totalBytes * 100 : 0;
            Status = status ?? string.Empty;
        }

        public void Update(long bytesProcessed, string status = null)
        {
            CurrentTime = DateTime.Now;
            BytesProcessed = bytesProcessed;
            Percentage = TotalBytes > 0 ? (double)bytesProcessed / TotalBytes * 100 : 0;
            if (status != null)
                Status = status;
        }

        public override string ToString()
        {
            return $"{Percentage:F1}% - {Status} - {Speed / 1024:F1} KB/s";
        }
    }

    /// <summary>
    /// Log message
    /// </summary>
    public class LogMessage
    {
        /// <summary>
        /// Timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Log level
        /// </summary>
        public LogLevel Level { get; set; }

        /// <summary>
        /// Message text
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Exception (if error)
        /// </summary>
        public Exception Exception { get; set; }

        public LogMessage()
        {
            Timestamp = DateTime.Now;
            Message = string.Empty;
        }

        public LogMessage(LogLevel level, string message, Exception exception = null)
        {
            Timestamp = DateTime.Now;
            Level = level;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        public override string ToString()
        {
            var prefix = Level switch
            {
                LogLevel.Debug => "[DEBUG]",
                LogLevel.Info => "[INFO]",
                LogLevel.Warning => "[WARN]",
                LogLevel.Error => "[ERROR]",
                _ => "[LOG]"
            };

            var msg = $"{Timestamp:HH:mm:ss} {prefix} {Message}";
            if (Exception != null)
                msg += $"\n  Exception: {Exception.Message}";

            return msg;
        }
    }
}
