using System;
using System.Threading;
using System.Threading.Tasks;
using AuroraFlasher.Models;

namespace AuroraFlasher.Interfaces
{
    /// <summary>
    /// Service for managing programmer operations
    /// </summary>
    public interface IProgrammerService : IDisposable
    {
        /// <summary>
        /// Current hardware instance
        /// </summary>
        IHardware CurrentHardware { get; }

        /// <summary>
        /// Current protocol instance
        /// </summary>
        object CurrentProtocol { get; } // ISpiProtocol, II2CProtocol, or IMicrowireProtocol

        /// <summary>
        /// Selected chip information
        /// </summary>
        ChipInfo SelectedChip { get; set; }

        /// <summary>
        /// Is hardware connected
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Current operation status
        /// </summary>
        OperationStatus Status { get; }

        /// <summary>
        /// Progress reporting event
        /// </summary>
        event EventHandler<ProgressInfo> ProgressChanged;

        /// <summary>
        /// Status changed event
        /// </summary>
        event EventHandler<OperationStatus> StatusChanged;

        // Hardware Management

        /// <summary>
        /// Initialize hardware by type
        /// </summary>
        Task<OperationResult> InitializeHardwareAsync(HardwareType hardwareType, string devicePath = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnect current hardware
        /// </summary>
        Task<OperationResult> DisconnectHardwareAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get available devices for hardware type
        /// </summary>
        Task<OperationResult<string[]>> GetAvailableDevicesAsync(HardwareType hardwareType, CancellationToken cancellationToken = default);

        // Protocol Operations

        /// <summary>
        /// Read chip ID and auto-detect
        /// </summary>
        Task<OperationResult<MemoryId>> ReadChipIdAsync(ProtocolType protocol, CancellationToken cancellationToken = default);

        /// <summary>
        /// Read memory to byte array
        /// </summary>
        Task<OperationResult<byte[]>> ReadMemoryAsync(uint address, int length, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Write memory from byte array
        /// </summary>
        Task<OperationResult> WriteMemoryAsync(uint address, byte[] data, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verify memory contents
        /// </summary>
        Task<OperationResult<bool>> VerifyMemoryAsync(uint address, byte[] data, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Erase memory
        /// </summary>
        Task<OperationResult> EraseMemoryAsync(EraseType eraseType, uint address = 0, uint length = 0, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Blank check
        /// </summary>
        Task<OperationResult<bool>> BlankCheckAsync(uint address, int length, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        // File Operations

        /// <summary>
        /// Read memory and save to file
        /// </summary>
        Task<OperationResult> ReadToFileAsync(string filePath, uint address, int length, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Load file and write to memory
        /// </summary>
        Task<OperationResult> WriteFromFileAsync(string filePath, uint address, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Load file, write, and verify
        /// </summary>
        Task<OperationResult> ProgramAndVerifyAsync(string filePath, uint address, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default);

        // Cancellation

        /// <summary>
        /// Cancel current operation
        /// </summary>
        void CancelOperation();
    }

    /// <summary>
    /// Service for managing chip database
    /// </summary>
    public interface IChipDatabaseService
    {
        /// <summary>
        /// Load chip database from XML
        /// </summary>
        Task<OperationResult> LoadDatabaseAsync(string xmlPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all chips
        /// </summary>
        ChipInfo[] GetAllChips();

        /// <summary>
        /// Find chip by ID
        /// </summary>
        ChipInfo FindChipById(MemoryId id);

        /// <summary>
        /// Find chips by name pattern
        /// </summary>
        ChipInfo[] FindChipsByName(string pattern);

        /// <summary>
        /// Find chips by manufacturer
        /// </summary>
        ChipInfo[] FindChipsByManufacturer(ChipManufacturer manufacturer);

        /// <summary>
        /// Add or update chip
        /// </summary>
        void AddOrUpdateChip(ChipInfo chip);

        /// <summary>
        /// Remove chip
        /// </summary>
        bool RemoveChip(string chipId);

        /// <summary>
        /// Save database to XML
        /// </summary>
        Task<OperationResult> SaveDatabaseAsync(string xmlPath, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Service for managing application settings
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Current settings
        /// </summary>
        Settings CurrentSettings { get; }

        /// <summary>
        /// Load settings from file
        /// </summary>
        Task<OperationResult> LoadSettingsAsync(string filePath = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Save settings to file
        /// </summary>
        Task<OperationResult> SaveSettingsAsync(string filePath = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reset to default settings
        /// </summary>
        void ResetToDefaults();

        /// <summary>
        /// Get setting value
        /// </summary>
        T GetSetting<T>(string key, T defaultValue = default);

        /// <summary>
        /// Set setting value
        /// </summary>
        void SetSetting<T>(string key, T value);
    }

    /// <summary>
    /// Service for application logging
    /// </summary>
    public interface ILoggingService
    {
        /// <summary>
        /// Log message event
        /// </summary>
        event EventHandler<LogMessage> MessageLogged;

        /// <summary>
        /// Log debug message
        /// </summary>
        void Debug(string message);

        /// <summary>
        /// Log info message
        /// </summary>
        void Info(string message);

        /// <summary>
        /// Log warning message
        /// </summary>
        void Warning(string message);

        /// <summary>
        /// Log error message
        /// </summary>
        void Error(string message, Exception exception = null);

        /// <summary>
        /// Clear all logs
        /// </summary>
        void Clear();

        /// <summary>
        /// Save logs to file
        /// </summary>
        Task<OperationResult> SaveToFileAsync(string filePath, CancellationToken cancellationToken = default);
    }
}
