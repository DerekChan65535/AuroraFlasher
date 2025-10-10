using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuroraFlasher.Interfaces;
using AuroraFlasher.Logging;
using AuroraFlasher.Models;

namespace AuroraFlasher.Services
{
    /// <summary>
    /// Main programmer service - coordinates hardware and protocol operations
    /// NOTE: Minimal version - does not implement full IProgrammerService interface
    /// </summary>
    public class ProgrammerService
    {
        private IHardware _hardware;
        private ISpiProtocol _spiProtocol;
        private II2CProtocol _i2cProtocol;
        private IMicrowireProtocol _microwireProtocol;

        public IHardware Hardware => _hardware;
        public ISpiProtocol SpiProtocol => _spiProtocol;
        public II2CProtocol I2CProtocol => _i2cProtocol;
        public IMicrowireProtocol MicrowireProtocol => _microwireProtocol;

        public event EventHandler<ProgressInfo> ProgressChanged;

        #region Hardware Management

        public Task<OperationResult<IHardware[]>> EnumerateHardwareAsync(HardwareType type = HardwareType.Auto, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    var hardwareList = new List<IHardware>();

                    // For minimal version, only CH341 is supported
                    if (type == HardwareType.Auto || type == HardwareType.CH341)
                    {
                        hardwareList.Add(new Hardware.CH341Hardware());
                    }

                    return OperationResult<IHardware[]>.SuccessResult(
                        hardwareList.ToArray(),
                        $"Found {hardwareList.Count} hardware type(s)");
                }
                catch (Exception ex)
                {
                    return OperationResult<IHardware[]>.FailureResult($"Hardware enumeration failed: {ex.Message}", ex);
                }
            }, cancellationToken);
        }

        public async Task<OperationResult> ConnectAsync(IHardware hardware, string devicePath = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (hardware == null)
                {
                    Logger.Error("ConnectAsync called with null hardware");
                    return OperationResult.FailureResult("Hardware is null");
                }

                Logger.Info($"Connecting to {hardware.Name} (type: {hardware.Type})");
                _hardware = hardware;

                var result = await _hardware.OpenAsync(devicePath, cancellationToken);
                if (result.Success)
                {
                    Logger.Info($"Successfully connected to {hardware.Name}");
                }
                else
                {
                    Logger.Error($"Failed to connect: {result.Message}");
                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Connection failed with exception");
                return OperationResult.FailureResult($"Connection failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult> DisconnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_hardware == null)
                {
                    Logger.Debug("DisconnectAsync called but no hardware connected");
                    return OperationResult.SuccessResult("No hardware connected");
                }

                Logger.Info("Disconnecting hardware...");

                // Exit programming mode first
                if (_spiProtocol != null)
                {
                    Logger.Debug("Exiting SPI programming mode");
                    await _spiProtocol.ExitProgrammingModeAsync(cancellationToken);
                }
                if (_i2cProtocol != null)
                {
                    Logger.Debug("Exiting I2C programming mode");
                    await _i2cProtocol.ExitProgrammingModeAsync(cancellationToken);
                }
                if (_microwireProtocol != null)
                {
                    Logger.Debug("Exiting MicroWire programming mode");
                    await _microwireProtocol.ExitProgrammingModeAsync(cancellationToken);
                }

                var result = await _hardware.CloseAsync(cancellationToken);
                
                _hardware = null;
                _spiProtocol = null;
                _i2cProtocol = null;
                _microwireProtocol = null;

                Logger.Info("Disconnected successfully");
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Disconnect failed with exception");
                return OperationResult.FailureResult($"Disconnect failed: {ex.Message}", ex);
            }
        }

        #endregion

        #region Chip Detection

        public async Task<OperationResult<ChipInfo>> DetectChipAsync(ProtocolType protocol, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_hardware == null || !_hardware.IsConnected)
                {
                    Logger.Error("DetectChipAsync called but hardware not connected");
                    return OperationResult<ChipInfo>.FailureResult("Hardware not connected");
                }

                Logger.Info($"Detecting chip using {protocol} protocol...");

                switch (protocol)
                {
                    case ProtocolType.SPI:
                        return await DetectSpiChipAsync(cancellationToken);
                    
                    case ProtocolType.I2C:
                    case ProtocolType.MicroWire:
                        Logger.Warn($"{protocol} detection not implemented in minimal version");
                        return OperationResult<ChipInfo>.FailureResult($"{protocol} detection not implemented in minimal version");
                    
                    default:
                        Logger.Error($"Unknown protocol: {protocol}");
                        return OperationResult<ChipInfo>.FailureResult($"Unknown protocol: {protocol}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Chip detection failed with exception");
                return OperationResult<ChipInfo>.FailureResult($"Chip detection failed: {ex.Message}", ex);
            }
        }

        private async Task<OperationResult<ChipInfo>> DetectSpiChipAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Create SPI protocol instance
                Logger.Debug("Creating SPI protocol instance");
                _spiProtocol = new Protocols.Spi25Protocol(_hardware);

                // Enter programming mode
                Logger.Debug("Entering SPI programming mode");
                var initResult = await _spiProtocol.EnterProgrammingModeAsync(cancellationToken);
                if (!initResult.Success)
                {
                    Logger.Error($"Failed to initialize SPI: {initResult.Message}");
                    return OperationResult<ChipInfo>.FailureResult($"Failed to initialize SPI: {initResult.Message}");
                }

                // Read chip ID
                var idResult = await _spiProtocol.ReadIdAsync(cancellationToken);
                if (!idResult.Success)
                {
                    Logger.Error($"Failed to read chip ID: {idResult.Message}");
                    return OperationResult<ChipInfo>.FailureResult($"Failed to read chip ID: {idResult.Message}");
                }

                Logger.Info($"Read chip ID - Manufacturer: 0x{idResult.Data.ManufacturerId:X2}, Device: 0x{idResult.Data.DeviceId:X4}");

                // Look up chip in database
                var chipInfo = FindChipByMemoryId(idResult.Data);
                if (chipInfo == null)
                {
                    Logger.Warn("Chip not found in database - creating generic chip info");
                    // Unknown chip - create basic info from ID
                    chipInfo = new ChipInfo
                    {
                        Name = "Unknown SPI Chip",
                        Manufacturer = GetManufacturerEnum(idResult.Data.ManufacturerId),
                        ProtocolType = ProtocolType.SPI,
                        SpiCommandSet = SpiCommandSet.Series25,
                        ManufacturerId = idResult.Data.ManufacturerId,
                        DeviceId = idResult.Data.DeviceId,
                        Size = 0,
                        PageSize = 256,
                        SectorSize = 4096,
                        BlockSize = 65536,
                        Voltage = 3300
                    };
                }
                else
                {
                    Logger.Info($"Chip identified: {chipInfo.Name} ({chipInfo.Manufacturer}, {chipInfo.SizeKB}KB)");
                }

                _spiProtocol.ChipInfo = chipInfo;

                string successMsg = $"Detected: {chipInfo.Name} ({chipInfo.SizeKB}KB)";
                Logger.Info(successMsg);
                return OperationResult<ChipInfo>.SuccessResult(chipInfo, successMsg);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "SPI detection failed with exception");
                return OperationResult<ChipInfo>.FailureResult($"SPI detection failed: {ex.Message}", ex);
            }
        }

        #endregion

        #region Chip Database (Hardcoded for minimal version)

        private ChipInfo FindChipByMemoryId(MemoryId memoryId)
        {
            if (memoryId == null)
                return null;

            // Get hardcoded chip list
            var chips = GetHardcodedChipDatabase();

            // Primary match: Use JEDEC ID (bytes 1 and 2) if available
            if (memoryId.JedecId != null && memoryId.JedecId.Length >= 3)
            {
                byte jedecManufacturer = memoryId.JedecId[0];
                ushort jedecDeviceId = (ushort)((memoryId.JedecId[1] << 8) | memoryId.JedecId[2]);

                foreach (var chip in chips)
                {
                    if (chip.ManufacturerId == jedecManufacturer &&
                        chip.DeviceId == jedecDeviceId)
                    {
                        Logger.Debug($"Chip matched by JEDEC ID: {jedecManufacturer:X2}-{memoryId.JedecId[1]:X2}-{memoryId.JedecId[2]:X2} -> {chip.Name}");
                        return chip;
                    }
                }
            }

            // Fallback match: Try Manufacturer ID and Device ID from 0x90 command
            foreach (var chip in chips)
            {
                if (chip.ManufacturerId == memoryId.ManufacturerId &&
                    chip.DeviceId == memoryId.DeviceId)
                {
                    Logger.Debug($"Chip matched by MFR/Device ID: {memoryId.ManufacturerId:X2}-{memoryId.DeviceId:X4} -> {chip.Name}");
                    return chip;
                }
            }

            return null;
        }

        private List<ChipInfo> GetHardcodedChipDatabase()
        {
            return new List<ChipInfo>
            {
                // Winbond W25Q series (Most common)
                new ChipInfo
                {
                    Name = "W25Q32",
                    Manufacturer = ChipManufacturer.Winbond,
                    ProtocolType = ProtocolType.SPI,
                    SpiCommandSet = SpiCommandSet.Series25,
                    Size = 4 * 1024 * 1024, // 4MB
                    PageSize = 256,
                    SectorSize = 4096,
                    BlockSize = 65536,
                    ManufacturerId = 0xEF,
                    DeviceId = 0x4016,
                    Voltage = 3300, // 3.3V
                    Supports4ByteAddress = false,
                    Description = "Winbond W25Q32 4MB SPI Flash"
                },
                new ChipInfo
                {
                    Name = "W25Q64",
                    Manufacturer = ChipManufacturer.Winbond,
                    ProtocolType = ProtocolType.SPI,
                    SpiCommandSet = SpiCommandSet.Series25,
                    Size = 8 * 1024 * 1024, // 8MB
                    PageSize = 256,
                    SectorSize = 4096,
                    BlockSize = 65536,
                    ManufacturerId = 0xEF,
                    DeviceId = 0x4017,
                    Voltage = 3300,
                    Supports4ByteAddress = false,
                    Description = "Winbond W25Q64 8MB SPI Flash"
                },
                new ChipInfo
                {
                    Name = "W25Q128",
                    Manufacturer = ChipManufacturer.Winbond,
                    ProtocolType = ProtocolType.SPI,
                    SpiCommandSet = SpiCommandSet.Series25,
                    Size = 16 * 1024 * 1024, // 16MB
                    PageSize = 256,
                    SectorSize = 4096,
                    BlockSize = 65536,
                    ManufacturerId = 0xEF,
                    DeviceId = 0x4018,
                    Voltage = 3300,
                    Supports4ByteAddress = false,
                    Description = "Winbond W25Q128 16MB SPI Flash"
                },
                new ChipInfo
                {
                    Name = "W25Q256",
                    Manufacturer = ChipManufacturer.Winbond,
                    ProtocolType = ProtocolType.SPI,
                    SpiCommandSet = SpiCommandSet.Series25,
                    Size = 32 * 1024 * 1024, // 32MB
                    PageSize = 256,
                    SectorSize = 4096,
                    BlockSize = 65536,
                    ManufacturerId = 0xEF,
                    DeviceId = 0x4019,
                    Voltage = 3300,
                    Supports4ByteAddress = true,
                    Description = "Winbond W25Q256 32MB SPI Flash"
                },

                // Winbond W25X series (Older 3V flash, JEDEC ID format: EF-30-xx)
                // Note: Multiple W25X40 variants share same ID (EF3013), pick most common
                new ChipInfo
                {
                    Name = "W25X40CL",
                    Manufacturer = ChipManufacturer.Winbond,
                    ProtocolType = ProtocolType.SPI,
                    SpiCommandSet = SpiCommandSet.Series25,
                    Size = 512 * 1024, // 512KB (4Mbit)
                    PageSize = 256,
                    SectorSize = 4096,
                    BlockSize = 65536,
                    ManufacturerId = 0xEF,
                    DeviceId = 0x3013, // JEDEC: EF-30-13
                    Voltage = 3300,
                    Supports4ByteAddress = false,
                    Description = "Winbond W25X40 512KB SPI Flash (W25X40AL/AV/BL/BV/CL/L/V variants)"
                },
                new ChipInfo
                {
                    Name = "W25X80",
                    Manufacturer = ChipManufacturer.Winbond,
                    ProtocolType = ProtocolType.SPI,
                    SpiCommandSet = SpiCommandSet.Series25,
                    Size = 1 * 1024 * 1024, // 1MB (8Mbit)
                    PageSize = 256,
                    SectorSize = 4096,
                    BlockSize = 65536,
                    ManufacturerId = 0xEF,
                    DeviceId = 0x3014, // JEDEC: EF-30-14
                    Voltage = 3300,
                    Supports4ByteAddress = false,
                    Description = "Winbond W25X80 1MB SPI Flash"
                },
                new ChipInfo
                {
                    Name = "W25X16",
                    Manufacturer = ChipManufacturer.Winbond,
                    ProtocolType = ProtocolType.SPI,
                    SpiCommandSet = SpiCommandSet.Series25,
                    Size = 2 * 1024 * 1024, // 2MB (16Mbit)
                    PageSize = 256,
                    SectorSize = 4096,
                    BlockSize = 65536,
                    ManufacturerId = 0xEF,
                    DeviceId = 0x3015, // JEDEC: EF-30-15
                    Voltage = 3300,
                    Supports4ByteAddress = false,
                    Description = "Winbond W25X16 2MB SPI Flash"
                },
                new ChipInfo
                {
                    Name = "W25X32",
                    Manufacturer = ChipManufacturer.Winbond,
                    ProtocolType = ProtocolType.SPI,
                    SpiCommandSet = SpiCommandSet.Series25,
                    Size = 4 * 1024 * 1024, // 4MB (32Mbit)
                    PageSize = 256,
                    SectorSize = 4096,
                    BlockSize = 65536,
                    ManufacturerId = 0xEF,
                    DeviceId = 0x3016, // JEDEC: EF-30-16
                    Voltage = 3300,
                    Supports4ByteAddress = false,
                    Description = "Winbond W25X32 4MB SPI Flash"
                },
                new ChipInfo
                {
                    Name = "W25X64",
                    Manufacturer = ChipManufacturer.Winbond,
                    ProtocolType = ProtocolType.SPI,
                    SpiCommandSet = SpiCommandSet.Series25,
                    Size = 8 * 1024 * 1024, // 8MB (64Mbit)
                    PageSize = 256,
                    SectorSize = 4096,
                    BlockSize = 65536,
                    ManufacturerId = 0xEF,
                    DeviceId = 0x3017, // JEDEC: EF-30-17
                    Voltage = 3300,
                    Supports4ByteAddress = false,
                    Description = "Winbond W25X64 8MB SPI Flash"
                },

                // Macronix MX25L series
                new ChipInfo
                {
                    Name = "MX25L3233F",
                    Manufacturer = ChipManufacturer.Macronix,
                    ProtocolType = ProtocolType.SPI,
                    SpiCommandSet = SpiCommandSet.Series25,
                    Size = 4 * 1024 * 1024,
                    PageSize = 256,
                    SectorSize = 4096,
                    BlockSize = 65536,
                    ManufacturerId = 0xC2,
                    DeviceId = 0x2016,
                    Voltage = 3300,
                    Supports4ByteAddress = false,
                    Description = "Macronix MX25L3233F 4MB SPI Flash"
                },
                new ChipInfo
                {
                    Name = "MX25L6433F",
                    Manufacturer = ChipManufacturer.Macronix,
                    ProtocolType = ProtocolType.SPI,
                    SpiCommandSet = SpiCommandSet.Series25,
                    Size = 8 * 1024 * 1024,
                    PageSize = 256,
                    SectorSize = 4096,
                    BlockSize = 65536,
                    ManufacturerId = 0xC2,
                    DeviceId = 0x2017,
                    Voltage = 3300,
                    Supports4ByteAddress = false,
                    Description = "Macronix MX25L6433F 8MB SPI Flash"
                },

                // GigaDevice GD25Q series
                new ChipInfo
                {
                    Name = "GD25Q32",
                    Manufacturer = ChipManufacturer.GigaDevice,
                    ProtocolType = ProtocolType.SPI,
                    SpiCommandSet = SpiCommandSet.Series25,
                    Size = 4 * 1024 * 1024,
                    PageSize = 256,
                    SectorSize = 4096,
                    BlockSize = 65536,
                    ManufacturerId = 0xC8,
                    DeviceId = 0x4016,
                    Voltage = 3300,
                    Supports4ByteAddress = false,
                    Description = "GigaDevice GD25Q32 4MB SPI Flash"
                },
                new ChipInfo
                {
                    Name = "GD25Q64",
                    Manufacturer = ChipManufacturer.GigaDevice,
                    ProtocolType = ProtocolType.SPI,
                    SpiCommandSet = SpiCommandSet.Series25,
                    Size = 8 * 1024 * 1024,
                    PageSize = 256,
                    SectorSize = 4096,
                    BlockSize = 65536,
                    ManufacturerId = 0xC8,
                    DeviceId = 0x4017,
                    Voltage = 3300,
                    Supports4ByteAddress = false,
                    Description = "GigaDevice GD25Q64 8MB SPI Flash"
                },

                // SST SST25VF series (uses AAI programming)
                new ChipInfo
                {
                    Name = "SST25VF032B",
                    Manufacturer = ChipManufacturer.SST,
                    ProtocolType = ProtocolType.SPI,
                    SpiCommandSet = SpiCommandSet.SST_AAI,
                    Size = 4 * 1024 * 1024,
                    PageSize = 256,
                    SectorSize = 4096,
                    BlockSize = 65536,
                    ManufacturerId = 0xBF,
                    DeviceId = 0x254A,
                    Voltage = 3300,
                    Supports4ByteAddress = false,
                    Description = "SST SST25VF032B 4MB SPI Flash with AAI"
                },

                // Atmel AT25 series
                new ChipInfo
                {
                    Name = "AT25DF321",
                    Manufacturer = ChipManufacturer.Atmel,
                    ProtocolType = ProtocolType.SPI,
                    SpiCommandSet = SpiCommandSet.Series25,
                    Size = 4 * 1024 * 1024,
                    PageSize = 256,
                    SectorSize = 4096,
                    BlockSize = 65536,
                    ManufacturerId = 0x1F,
                    DeviceId = 0x4700,
                    Voltage = 3300,
                    Supports4ByteAddress = false,
                    Description = "Atmel AT25DF321 4MB SPI Flash"
                }
            };
        }

        private ChipManufacturer GetManufacturerEnum(byte manufacturerId)
        {
            return manufacturerId switch
            {
                0xEF => ChipManufacturer.Winbond,
                0xC2 => ChipManufacturer.Macronix,
                0xC8 => ChipManufacturer.GigaDevice,
                0xBF => ChipManufacturer.SST,
                0x1F => ChipManufacturer.Atmel,
                0x20 => ChipManufacturer.STMicro,
                0x01 => ChipManufacturer.Spansion,
                0x9D => ChipManufacturer.ISSI,
                0x37 => ChipManufacturer.AMIC,
                0xA1 => ChipManufacturer.Fudan,
                _ => ChipManufacturer.Unknown
            };
        }

        #endregion

        #region Read/Write Operations

        public async Task<OperationResult<byte[]>> ReadMemoryAsync(uint address, int length, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_spiProtocol != null)
                {
                    var progressHandler = CreateProgressHandler(progress);
                    return await _spiProtocol.ReadAsync(address, length, progressHandler, cancellationToken);
                }
                else if (_i2cProtocol != null)
                {
                    return OperationResult<byte[]>.FailureResult("I2C read not implemented in minimal version");
                }
                else if (_microwireProtocol != null)
                {
                    return OperationResult<byte[]>.FailureResult("MicroWire read not implemented in minimal version");
                }
                else
                {
                    return OperationResult<byte[]>.FailureResult("No protocol initialized");
                }
            }
            catch (Exception ex)
            {
                return OperationResult<byte[]>.FailureResult($"Read memory failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult> WriteMemoryAsync(uint address, byte[] data, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_spiProtocol != null)
                {
                    var progressHandler = CreateProgressHandler(progress);
                    return await _spiProtocol.WriteAsync(address, data, progressHandler, cancellationToken);
                }
                else if (_i2cProtocol != null)
                {
                    return OperationResult.FailureResult("I2C write not implemented in minimal version");
                }
                else if (_microwireProtocol != null)
                {
                    return OperationResult.FailureResult("MicroWire write not implemented in minimal version");
                }
                else
                {
                    return OperationResult.FailureResult("No protocol initialized");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"Write memory failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult> EraseChipAsync(IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_spiProtocol != null)
                {
                    var progressHandler = CreateProgressHandler(progress);
                    return await _spiProtocol.EraseChipAsync(progressHandler, cancellationToken);
                }
                else
                {
                    return OperationResult.FailureResult("No protocol initialized");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"Chip erase failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult<bool>> VerifyMemoryAsync(uint address, byte[] data, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_spiProtocol != null)
                {
                    var progressHandler = CreateProgressHandler(progress);
                    return await _spiProtocol.VerifyAsync(address, data, progressHandler, cancellationToken);
                }
                else
                {
                    return OperationResult<bool>.FailureResult("No protocol initialized");
                }
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.FailureResult($"Verify failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult<bool>> BlankCheckAsync(uint address, int length, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_spiProtocol != null)
                {
                    var progressHandler = CreateProgressHandler(progress);
                    return await _spiProtocol.IsBlankAsync(address, length, progressHandler, cancellationToken);
                }
                else
                {
                    return OperationResult<bool>.FailureResult("No protocol initialized");
                }
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.FailureResult($"Blank check failed: {ex.Message}", ex);
            }
        }

        #endregion

        #region Helper Methods

        private IProgress<ProgressInfo> CreateProgressHandler(IProgress<ProgressInfo> progress)
        {
            if (progress == null)
                return null;

            return new Progress<ProgressInfo>(info =>
            {
                progress.Report(info);
                ProgressChanged?.Invoke(this, info);
            });
        }

        #endregion
    }
}
