using System;
using System.Threading;
using System.Threading.Tasks;
using AuroraFlasher.Models;

namespace AuroraFlasher.Interfaces
{
    /// <summary>
    /// Interface for MicroWire EEPROM protocol operations
    /// </summary>
    public interface IMicrowireProtocol : IDisposable
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
        /// Number of address bits (6-12)
        /// </summary>
        int AddressBits { get; set; }

        /// <summary>
        /// Progress reporting event
        /// </summary>
        event EventHandler<ProgressInfo> ProgressChanged;

        // Lifecycle
        
        /// <summary>
        /// Enter programming mode
        /// </summary>
        Task<OperationResult> EnterProgrammingModeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Exit programming mode
        /// </summary>
        Task<OperationResult> ExitProgrammingModeAsync(CancellationToken cancellationToken = default);

        // Control Commands
        
        /// <summary>
        /// Send EWEN (Erase/Write Enable) command
        /// </summary>
        Task<OperationResult> WriteEnableAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Send EWDS (Erase/Write Disable) command
        /// </summary>
        Task<OperationResult> WriteDisableAsync(CancellationToken cancellationToken = default);

        // Read Operations
        
        /// <summary>
        /// Read data from memory
        /// </summary>
        Task<OperationResult<byte[]>> ReadAsync(uint address, int length, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Read single word (16-bit)
        /// </summary>
        Task<OperationResult<ushort>> ReadWordAsync(uint address, CancellationToken cancellationToken = default);

        // Write Operations
        
        /// <summary>
        /// Write data to memory
        /// </summary>
        Task<OperationResult> WriteAsync(uint address, byte[] data, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Write single word (16-bit)
        /// </summary>
        Task<OperationResult> WriteWordAsync(uint address, ushort value, CancellationToken cancellationToken = default);

        /// <summary>
        /// Write all memory with same value
        /// </summary>
        Task<OperationResult> WriteAllAsync(ushort value, CancellationToken cancellationToken = default);

        // Erase Operations
        
        /// <summary>
        /// Erase word at address
        /// </summary>
        Task<OperationResult> EraseWordAsync(uint address, CancellationToken cancellationToken = default);

        /// <summary>
        /// Erase all memory
        /// </summary>
        Task<OperationResult> EraseAllAsync(CancellationToken cancellationToken = default);

        // Status and Control
        
        /// <summary>
        /// Wait until device is not busy
        /// </summary>
        Task<OperationResult> WaitNotBusyAsync(int timeoutMs = 5000, CancellationToken cancellationToken = default);

        // Verification
        
        /// <summary>
        /// Verify data matches memory contents
        /// </summary>
        Task<OperationResult<bool>> VerifyAsync(uint address, byte[] data, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if range is blank (all 0xFFFF)
        /// </summary>
        Task<OperationResult<bool>> IsBlankAsync(uint address, int length, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);
    }
}
