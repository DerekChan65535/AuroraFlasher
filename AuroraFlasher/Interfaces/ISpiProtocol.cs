using System;
using System.Threading;
using System.Threading.Tasks;
using AuroraFlasher.Models;

namespace AuroraFlasher.Interfaces
{
    /// <summary>
    /// Interface for SPI flash memory protocol operations
    /// </summary>
    public interface ISpiProtocol : IDisposable
    {
        /// <summary>
        /// Associated hardware
        /// </summary>
        IHardware Hardware { get; }

        /// <summary>
        /// Command set variant
        /// </summary>
        SpiCommandSet CommandSet { get; }

        /// <summary>
        /// Chip information
        /// </summary>
        ChipInfo ChipInfo { get; set; }

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

        // Identification
        
        /// <summary>
        /// Read chip ID (JEDEC ID, Manufacturer ID, Electronic Signature)
        /// </summary>
        Task<OperationResult<MemoryId>> ReadIdAsync(CancellationToken cancellationToken = default);

        // Read Operations
        
        /// <summary>
        /// Read data from memory
        /// </summary>
        Task<OperationResult<byte[]>> ReadAsync(uint address, int length, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fast read (if supported)
        /// </summary>
        Task<OperationResult<byte[]>> FastReadAsync(uint address, int length, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        // Write Operations
        
        /// <summary>
        /// Write data to memory (with automatic page handling)
        /// </summary>
        Task<OperationResult> WriteAsync(uint address, byte[] data, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Write page (single page only, no boundary checking)
        /// </summary>
        Task<OperationResult> WritePageAsync(uint address, byte[] data, CancellationToken cancellationToken = default);

        /// <summary>
        /// SST AAI byte program
        /// </summary>
        Task<OperationResult> AaiByteWriteAsync(uint address, byte[] data, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// SST AAI word program
        /// </summary>
        Task<OperationResult> AaiWordWriteAsync(uint address, byte[] data, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        // Erase Operations
        
        /// <summary>
        /// Erase entire chip
        /// </summary>
        Task<OperationResult> EraseChipAsync(IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Erase sector (4KB)
        /// </summary>
        Task<OperationResult> EraseSectorAsync(uint address, CancellationToken cancellationToken = default);

        /// <summary>
        /// Erase 32KB block
        /// </summary>
        Task<OperationResult> EraseBlock32Async(uint address, CancellationToken cancellationToken = default);

        /// <summary>
        /// Erase 64KB block
        /// </summary>
        Task<OperationResult> EraseBlock64Async(uint address, CancellationToken cancellationToken = default);

        /// <summary>
        /// Erase range (automatic sector/block selection)
        /// </summary>
        Task<OperationResult> EraseRangeAsync(uint startAddress, uint length, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        // Status and Control
        
        /// <summary>
        /// Read status register
        /// </summary>
        Task<OperationResult<byte>> ReadStatusRegisterAsync(int registerIndex = 1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Write status register
        /// </summary>
        Task<OperationResult> WriteStatusRegisterAsync(byte value, int registerIndex = 1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Wait until chip is not busy
        /// </summary>
        Task<OperationResult> WaitNotBusyAsync(int timeoutMs = 30000, CancellationToken cancellationToken = default);

        /// <summary>
        /// Write enable
        /// </summary>
        Task<OperationResult> WriteEnableAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Write disable
        /// </summary>
        Task<OperationResult> WriteDisableAsync(CancellationToken cancellationToken = default);

        // Protection
        
        /// <summary>
        /// Check if chip has write protection
        /// </summary>
        Task<OperationResult<bool>> IsWriteProtectedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Set block protection bits
        /// </summary>
        Task<OperationResult> SetBlockProtectionAsync(byte level, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clear block protection
        /// </summary>
        Task<OperationResult> ClearBlockProtectionAsync(CancellationToken cancellationToken = default);

        // 4-Byte Addressing
        
        /// <summary>
        /// Enter 4-byte address mode
        /// </summary>
        Task<OperationResult> Enter4ByteAddressModeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Exit 4-byte address mode
        /// </summary>
        Task<OperationResult> Exit4ByteAddressModeAsync(CancellationToken cancellationToken = default);

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
