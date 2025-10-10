using System;

namespace AuroraFlasher.Models
{
    /// <summary>
    /// Represents the result of an operation
    /// </summary>
    public class OperationResult
    {
        /// <summary>
        /// Operation succeeded
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Result message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Exception if operation failed
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Operation duration
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Additional data
        /// </summary>
        public object Tag { get; set; }

        public OperationResult()
        {
            Success = true;
            Message = string.Empty;
        }

        public OperationResult(bool success, string message = null, Exception exception = null)
        {
            Success = success;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        public static OperationResult SuccessResult(string message = null)
        {
            return new OperationResult(true, message);
        }

        public static OperationResult FailureResult(string message, Exception exception = null)
        {
            return new OperationResult(false, message, exception);
        }

        public override string ToString()
        {
            return Success ? $"Success: {Message}" : $"Failed: {Message}";
        }
    }

    /// <summary>
    /// Represents the result of an operation with data
    /// </summary>
    public class OperationResult<T> : OperationResult
    {
        /// <summary>
        /// Result data
        /// </summary>
        public T Data { get; set; }

        public OperationResult() : base()
        {
        }

        public OperationResult(bool success, string message = null, Exception exception = null, T data = default)
            : base(success, message, exception)
        {
            Data = data;
        }

        public static OperationResult<T> SuccessResult(T data, string message = null)
        {
            return new OperationResult<T>(true, message, null, data);
        }

        public new static OperationResult<T> FailureResult(string message, Exception exception = null)
        {
            return new OperationResult<T>(false, message, exception, default);
        }
    }
}
