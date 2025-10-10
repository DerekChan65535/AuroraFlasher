using System;
using System.Threading;
using System.Threading.Tasks;
using AuroraFlasher.Models;

namespace AuroraFlasher.Interfaces
{
    /// <summary>
    /// Interface for I2C EEPROM protocol operations
    /// </summary>
    public interface II2CProtocol : IDisposable
    {
        /// <summary>
        /// Associated hardware
        /// </summary>
        IHardware Hardware { get; }

        /// <summary>
        /// Chip information
        /// </summary>
        ChipInfo ChipInfo { get; set; }

        /// <summary>
        /// I2C device address (7-bit)
        /// </summary>
        byte DeviceAddress { get; set; }

        /// <summary>
        /// Address type (determines addressing scheme)
        /// </summary>
        I2CAddressType AddressType { get; set; }

        /// <summary>
        /// Progress reporting event
        /// </summary>
        event EventHandler<ProgressInfo> ProgressChanged;

        // Lifecycle
        
        /// <summary>
        /// Enter programming mode
        /// </summary>
        Task<OperationResult> EnterProgrammingModeAsync(int speedKHz = 100, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exit programming mode
        /// </summary>
        Task<OperationResult> ExitProgrammingModeAsync(CancellationToken cancellationToken = default);

        // Identification
        
        /// <summary>
        /// Scan bus and find device
        /// </summary>
        Task<OperationResult<byte[]>> ScanBusAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Read device ID (if supported)
        /// </summary>
        Task<OperationResult<MemoryId>> ReadIdAsync(CancellationToken cancellationToken = default);

        // Read Operations
        
        /// <summary>
        /// Read data from memory
        /// </summary>
        Task<OperationResult<byte[]>> ReadAsync(uint address, int length, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sequential read (current address read)
        /// </summary>
        Task<OperationResult<byte[]>> SequentialReadAsync(int length, CancellationToken cancellationToken = default);

        // Write Operations
        
        /// <summary>
        /// Write data to memory (with automatic page handling)
        /// </summary>
        Task<OperationResult> WriteAsync(uint address, byte[] data, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Write page (single page only, no boundary checking)
        /// </summary>
        Task<OperationResult> WritePageAsync(uint address, byte[] data, CancellationToken cancellationToken = default);

        // Status and Control
        
        /// <summary>
        /// Wait until device acknowledges (write cycle complete)
        /// </summary>
        Task<OperationResult> WaitNotBusyAsync(int timeoutMs = 10000, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if device is responding
        /// </summary>
        Task<OperationResult<bool>> IsDeviceReadyAsync(CancellationToken cancellationToken = default);

        // Verification
        
        /// <summary>
        /// Verify data matches memory contents
        /// </summary>
        Task<OperationResult<bool>> VerifyAsync(uint address, byte[] data, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if range is blank (all 0xFF)
        /// </summary>
        Task<OperationResult<bool>> IsBlankAsync(uint address, int length, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);
    }
}
