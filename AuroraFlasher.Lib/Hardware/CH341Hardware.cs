using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuroraFlasher.Interfaces;
using AuroraFlasher.Logging;
using AuroraFlasher.Models;
using AuroraFlasher.NativeInterop;
using AuroraFlasher.Utilities;

namespace AuroraFlasher.Hardware
{
    /// <summary>
    /// CH341/CH341A hardware implementation for SPI, I2C, and MicroWire
    /// </summary>
    public class CH341Hardware : HardwareBase
    {
        private uint _deviceIndex = 0;
        private int _deviceHandle = -1;
        private bool _isCH341A = false;

        public override HardwareType Type => HardwareType.CH341;
        public override string Name => _isCH341A ? "CH341A USB Programmer" : "CH341 USB Programmer";

        private static readonly HardwareCapabilities _capabilities = new HardwareCapabilities
        {
            SupportsSpi = true,
            SupportsI2C = true,
            SupportsMicroWire = true,
            MaxSpiTransferSize = 4096,
            MaxI2CTransferSize = 256,
            AvailableSpiSpeeds = new[] { SpiSpeed.Low, SpiSpeed.Normal, SpiSpeed.High },
            AvailableI2CSpeeds = new[] { 20, 100, 400, 750 },
            SupportsGpio = false,
            GpioPinCount = 0,
            HasFirmwareVersion = true,
            RequiresExternalPower = false
        };

        public override HardwareCapabilities Capabilities => _capabilities;

        #region Connection Management

        public override Task<string[]> EnumerateDevicesAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                Logger.Info("Enumerating CH341/CH341A devices...");
                var devices = new System.Collections.Generic.List<string>();

                // Try to open up to 4 devices
                for (uint i = 0; i < 4; i++)
                {
                    var handle = CH341Dll.CH341OpenDevice(i);
                    if (handle >= 0)
                    {
                        // Device found
                        var version = CH341Dll.CH341GetVerIC(i);
                        var deviceName = version == 0x20 ? $"CH341A Device #{i}" : $"CH341 Device #{i}";
                        devices.Add(deviceName);
                        Logger.Info($"Found device: {deviceName} (version=0x{version:X2}, handle={handle})");
                        
                        // Close immediately
                        CH341Dll.CH341CloseDevice(i);
                    }
                }

                Logger.Info($"Enumeration complete. Found {devices.Count} device(s)");
                return devices.ToArray();
            }, cancellationToken);
        }

        public override Task<OperationResult> OpenAsync(string devicePath = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    ThrowIfDisposed();

                    if (_isConnected)
                    {
                        Logger.Warn("Attempted to open device that is already open");
                        return OperationResult.FailureResult("Device already open");
                    }

                    // Parse device index from path (e.g., "CH341A Device #0" -> 0)
                    if (!string.IsNullOrEmpty(devicePath))
                    {
                        var parts = devicePath.Split('#');
                        if (parts.Length > 1 && uint.TryParse(parts[1], out var index))
                        {
                            _deviceIndex = index;
                        }
                    }

                    Logger.Info($"Opening CH341 device #{_deviceIndex}...");

                    // Open device
                    _deviceHandle = CH341Dll.CH341OpenDevice(_deviceIndex);
                    if (_deviceHandle < 0)
                    {
                        Logger.Error($"Failed to open CH341 device #{_deviceIndex}. Handle returned: {_deviceHandle}");
                        return OperationResult.FailureResult($"Failed to open CH341 device #{_deviceIndex}. Is it connected?");
                    }

                    Logger.Debug($"Device handle obtained: {_deviceHandle}");

                    // Check chip version
                    var version = CH341Dll.CH341GetVerIC(_deviceIndex);
                    _isCH341A = (version == 0x20);

                    if (version == 0)
                    {
                        CH341Dll.CH341CloseDevice(_deviceIndex);
                        _deviceHandle = -1;
                        Logger.Error("Invalid device version 0 - device communication error");
                        return OperationResult.FailureResult("Invalid device or communication error");
                    }

                    Logger.Info($"Device version detected: 0x{version:X2} ({(_isCH341A ? "CH341A" : "CH341")})");

                    // Set timeout (5 seconds for read/write)
                    CH341Dll.CH341SetTimeout(_deviceIndex, 5000, 5000);
                    Logger.Debug("Set device timeout to 5000ms for read/write");

                    // Flush any pending data
                    CH341Dll.CH341FlushBuffer(_deviceIndex);
                    Logger.Debug("Flushed device buffer");

                    _isConnected = true;
                    var successMsg = $"Opened {Name} (device #{_deviceIndex})";
                    Logger.Info(successMsg);
                    return OperationResult.SuccessResult(successMsg);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Exception while opening CH341 device #{_deviceIndex}");
                    return OperationResult.FailureResult($"Error opening CH341: {ex.Message}", ex);
                }
            }, cancellationToken);
        }

        public override Task<OperationResult> CloseAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (_isConnected)
                    {
                        Logger.Info($"Closing CH341 device #{_deviceIndex}...");
                        CH341Dll.CH341CloseDevice(_deviceIndex);
                        _deviceHandle = -1;
                        _isConnected = false;
                        Logger.Info("CH341 device closed successfully");
                        return OperationResult.SuccessResult("CH341 device closed");
                    }
                    
                    Logger.Debug("Close requested but device was not connected");

                    return OperationResult.SuccessResult("Device was not open");
                }
                catch (Exception ex)
                {
                    return OperationResult.FailureResult($"Error closing CH341: {ex.Message}", ex);
                }
            }, cancellationToken);
        }

        #endregion

        #region SPI Operations

        public override Task<OperationResult> SpiInitAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    ThrowIfNotConnected();

                    // Set stream mode for SPI (MSB first, standard I2C speed)
                    var mode = CH341Dll.MakeStreamMode(1, false, true);
                    var success = CH341Dll.CH341SetStream(_deviceIndex, mode);

                    if (!success)
                        return OperationResult.FailureResult("Failed to initialize SPI mode");

                    return OperationResult.SuccessResult("SPI mode initialized");
                }
                catch (Exception ex)
                {
                    return OperationResult.FailureResult($"SPI init error: {ex.Message}", ex);
                }
            }, cancellationToken);
        }

        public override Task<OperationResult> SpiDeinitAsync(CancellationToken cancellationToken = default)
        {
            // No specific deinit needed for CH341
            return Task.FromResult(OperationResult.SuccessResult("SPI mode deinitialized"));
        }

        public override Task<OperationResult> SpiSendCommandAsync(byte command, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    ThrowIfNotConnected();

                    var cmdBuffer = new byte[] { command };
                    var cs = CH341Dll.MakeChipSelect(0, true); // Use D0 as CS

                    var success = CH341Dll.CH341StreamSPI4(_deviceIndex, cs, 1, cmdBuffer);

                    if (!success)
                        return OperationResult.FailureResult($"Failed to send SPI command 0x{command:X2}");

                    return OperationResult.SuccessResult($"Sent SPI command 0x{command:X2}");
                }
                catch (Exception ex)
                {
                    return OperationResult.FailureResult($"SPI send command error: {ex.Message}", ex);
                }
            }, cancellationToken);
        }

        public override Task<OperationResult<byte[]>> SpiReadAsync(int length, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    ThrowIfNotConnected();

                    Logger.Trace($"SpiReadAsync: Reading {length} bytes");

                    var buffer = new byte[length];
                    // Fill with 0xFF (dummy bytes for read)
                    for (var i = 0; i < length; i++)
                        buffer[i] = 0xFF;
                    
                    var cs = CH341Dll.MakeChipSelect(0, true);
                    var success = CH341Dll.CH341StreamSPI4(_deviceIndex, cs, (uint)length, buffer);

                    if (!success)
                    {
                        Logger.Error($"SPI read failed for {length} bytes");
                        return OperationResult<byte[]>.FailureResult($"Failed to read {length} bytes via SPI");
                    }

                    Logger.Trace($"SpiReadAsync: Successfully read {length} bytes");
                    return OperationResult<byte[]>.SuccessResult(buffer, $"Read {length} bytes");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"SPI read error for {length} bytes");
                    return OperationResult<byte[]>.FailureResult($"SPI read error: {ex.Message}", ex);
                }
            }, cancellationToken);
        }

        public override Task<OperationResult> SpiWriteAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    ThrowIfNotConnected();

                    if (data == null || data.Length == 0)
                    {
                        Logger.Warn("SpiWriteAsync called with null or empty data");
                        return OperationResult.FailureResult("No data to write");
                    }

                    Logger.Trace($"SpiWriteAsync: Writing {data.Length} bytes (first byte: 0x{data[0]:X2})");

                    var cs = CH341Dll.MakeChipSelect(0, true);
                    var success = CH341Dll.CH341StreamSPI4(_deviceIndex, cs, (uint)data.Length, data);

                    if (!success)
                    {
                        Logger.Error($"SPI write failed for {data.Length} bytes");
                        return OperationResult.FailureResult($"Failed to write {data.Length} bytes via SPI");
                    }

                    Logger.Trace($"SpiWriteAsync: Successfully wrote {data.Length} bytes");
                    return OperationResult.SuccessResult($"Wrote {data.Length} bytes");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"SPI write error for {(data?.Length ?? 0)} bytes");
                    return OperationResult.FailureResult($"SPI write error: {ex.Message}", ex);
                }
            }, cancellationToken);
        }

        public override Task<OperationResult<byte[]>> SpiTransferAsync(byte[] writeData, int readLength, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    ThrowIfNotConnected();

                    // Create buffer: write data + dummy bytes for read
                    var buffer = new byte[writeData.Length + readLength];
                    Array.Copy(writeData, 0, buffer, 0, writeData.Length);
                    // Fill remaining with 0xFF (dummy bytes for read)
                    for (var i = 0; i < readLength; i++)
                        buffer[writeData.Length + i] = 0xFF;

                    var cs = CH341Dll.MakeChipSelect(0, true);
                    var success = CH341Dll.CH341StreamSPI4(_deviceIndex, cs, (uint)buffer.Length, buffer);

                    if (!success)
                        return OperationResult<byte[]>.FailureResult("SPI transfer failed");

                    // Extract read data (skip write portion)
                    var readData = new byte[readLength];
                    Array.Copy(buffer, writeData.Length, readData, 0, readLength);

                    return OperationResult<byte[]>.SuccessResult(readData, $"Transferred {writeData.Length} out, {readLength} in");
                }
                catch (Exception ex)
                {
                    return OperationResult<byte[]>.FailureResult($"SPI transfer error: {ex.Message}", ex);
                }
            }, cancellationToken);
        }

        #endregion

        #region I2C Operations

        public override Task<OperationResult> I2CInitAsync(int speedKHz = 100, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    ThrowIfNotConnected();

                    // Map speed to CH341 mode
                    int speedMode;
                    if (speedKHz <= 20)
                        speedMode = 0;      // 20 kHz
                    else if (speedKHz <= 100)
                        speedMode = 1;     // 100 kHz (standard)
                    else if (speedKHz <= 400)
                        speedMode = 2;     // 400 kHz (fast)
                    else
                        speedMode = 3;     // 750 kHz (high speed)

                    var mode = CH341Dll.MakeStreamMode(speedMode, false, true);
                    var success = CH341Dll.CH341SetStream(_deviceIndex, mode);

                    if (!success)
                        return OperationResult.FailureResult("Failed to initialize I2C mode");

                    return OperationResult.SuccessResult($"I2C initialized at {speedKHz} kHz");
                }
                catch (Exception ex)
                {
                    return OperationResult.FailureResult($"I2C init error: {ex.Message}", ex);
                }
            }, cancellationToken);
        }

        public override Task<OperationResult> I2CDeinitAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OperationResult.SuccessResult("I2C deinitialized"));
        }

        #endregion

        #region Utility Methods

        public override Task<OperationResult<string>> GetFirmwareVersionAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    var dllVersion = CH341Dll.CH341GetVersion();
                    var drvVersion = CH341Dll.CH341GetDrvVersion();
                    var chipVersion = _isConnected ? CH341Dll.CH341GetVerIC(_deviceIndex) : 0;

                    var chipName = chipVersion switch
                    {
                        0x10 => "CH341",
                        0x20 => "CH341A",
                        _ => "Unknown"
                    };

                    var version = $"DLL: {(dllVersion >> 16) & 0xFF}.{(dllVersion >> 8) & 0xFF}.{dllVersion & 0xFF}, " +
                                  $"Driver: {(drvVersion >> 16) & 0xFF}.{(drvVersion >> 8) & 0xFF}.{drvVersion & 0xFF}, " +
                                  $"Chip: {chipName}";

                    return OperationResult<string>.SuccessResult(version);
                }
                catch (Exception ex)
                {
                    return OperationResult<string>.FailureResult($"Error getting version: {ex.Message}", ex);
                }
            }, cancellationToken);
        }

        #endregion
    }
}
