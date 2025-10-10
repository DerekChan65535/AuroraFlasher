using System;
using System.Threading;
using System.Threading.Tasks;
using AuroraFlasher.Models;

namespace AuroraFlasher.Interfaces
{
    /// <summary>
    /// Base interface for all hardware programmers
    /// </summary>
    public interface IHardware : IDisposable
    {
        /// <summary>
        /// Hardware type identifier
        /// </summary>
        HardwareType Type { get; }

        /// <summary>
        /// Hardware display name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Is hardware connected and ready
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Hardware capabilities
        /// </summary>
        HardwareCapabilities Capabilities { get; }

        /// <summary>
        /// Current SPI speed setting
        /// </summary>
        SpiSpeed Speed { get; set; }

        // Connection Management
        
        /// <summary>
        /// Enumerate available devices
        /// </summary>
        Task<string[]> EnumerateDevicesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Open connection to hardware
        /// </summary>
        Task<OperationResult> OpenAsync(string devicePath = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Close connection to hardware
        /// </summary>
        Task<OperationResult> CloseAsync(CancellationToken cancellationToken = default);

        // SPI Operations
        
        /// <summary>
        /// Initialize SPI mode
        /// </summary>
        Task<OperationResult> SpiInitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Deinitialize SPI mode
        /// </summary>
        Task<OperationResult> SpiDeinitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Send SPI command
        /// </summary>
        Task<OperationResult> SpiSendCommandAsync(byte command, CancellationToken cancellationToken = default);

        /// <summary>
        /// Read data via SPI
        /// </summary>
        Task<OperationResult<byte[]>> SpiReadAsync(int length, CancellationToken cancellationToken = default);

        /// <summary>
        /// Write data via SPI
        /// </summary>
        Task<OperationResult> SpiWriteAsync(byte[] data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Transfer data (write then read)
        /// </summary>
        Task<OperationResult<byte[]>> SpiTransferAsync(byte[] writeData, int readLength, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send command with address and read data
        /// </summary>
        Task<OperationResult<byte[]>> SpiReadWithAddressAsync(byte command, uint address, int addressBytes, int dataLength, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send command with address and write data
        /// </summary>
        Task<OperationResult> SpiWriteWithAddressAsync(byte command, uint address, int addressBytes, byte[] data, CancellationToken cancellationToken = default);

        // I2C Operations
        
        /// <summary>
        /// Initialize I2C mode
        /// </summary>
        Task<OperationResult> I2CInitAsync(int speedKHz = 100, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deinitialize I2C mode
        /// </summary>
        Task<OperationResult> I2CDeinitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Scan I2C bus for devices
        /// </summary>
        Task<OperationResult<byte[]>> I2CScanAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Read from I2C device
        /// </summary>
        Task<OperationResult<byte[]>> I2CReadAsync(byte deviceAddress, int length, CancellationToken cancellationToken = default);

        /// <summary>
        /// Write to I2C device
        /// </summary>
        Task<OperationResult> I2CWriteAsync(byte deviceAddress, byte[] data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Write address then read data
        /// </summary>
        Task<OperationResult<byte[]>> I2CReadFromAddressAsync(byte deviceAddress, uint memoryAddress, int addressBytes, int dataLength, CancellationToken cancellationToken = default);

        /// <summary>
        /// Write address and data
        /// </summary>
        Task<OperationResult> I2CWriteToAddressAsync(byte deviceAddress, uint memoryAddress, int addressBytes, byte[] data, CancellationToken cancellationToken = default);

        // MicroWire Operations
        
        /// <summary>
        /// Initialize MicroWire mode
        /// </summary>
        Task<OperationResult> MicroWireInitAsync(int addressBits, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deinitialize MicroWire mode
        /// </summary>
        Task<OperationResult> MicroWireDeinitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Send EWEN (Erase/Write Enable) command
        /// </summary>
        Task<OperationResult> MicroWireEnableAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Send EWDS (Erase/Write Disable) command
        /// </summary>
        Task<OperationResult> MicroWireDisableAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Read from MicroWire device
        /// </summary>
        Task<OperationResult<byte[]>> MicroWireReadAsync(uint address, int length, CancellationToken cancellationToken = default);

        /// <summary>
        /// Write to MicroWire device
        /// </summary>
        Task<OperationResult> MicroWireWriteAsync(uint address, byte[] data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Erase word at address
        /// </summary>
        Task<OperationResult> MicroWireEraseAsync(uint address, CancellationToken cancellationToken = default);

        /// <summary>
        /// Erase all chip
        /// </summary>
        Task<OperationResult> MicroWireEraseAllAsync(CancellationToken cancellationToken = default);

        // Utility Methods
        
        /// <summary>
        /// Get firmware version (if applicable)
        /// </summary>
        Task<OperationResult<string>> GetFirmwareVersionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Set GPIO pin state (if supported)
        /// </summary>
        Task<OperationResult> SetGpioPinAsync(int pin, bool state, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get GPIO pin state (if supported)
        /// </summary>
        Task<OperationResult<bool>> GetGpioPinAsync(int pin, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delay in milliseconds
        /// </summary>
        Task DelayAsync(int milliseconds, CancellationToken cancellationToken = default);
    }
}
