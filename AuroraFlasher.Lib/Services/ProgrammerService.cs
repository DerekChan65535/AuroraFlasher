using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuroraFlasher.Interfaces;
using AuroraFlasher.Logging;
using AuroraFlasher.Models;
using AuroraFlasher.Utilities;

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
        private List<ChipInfo> _chipDatabase;
        private bool _databaseLoaded = false;

        public IHardware Hardware => _hardware;
        public ISpiProtocol SpiProtocol => _spiProtocol;
        public II2CProtocol I2CProtocol => _i2cProtocol;
        public IMicrowireProtocol MicrowireProtocol => _microwireProtocol;

        public event EventHandler<ProgressInfo> ProgressChanged;

        /// <summary>
        /// Loads chip database from chiplist.xml. Called automatically on first use.
        /// </summary>
        private void EnsureDatabaseLoaded()
        {
            if (_databaseLoaded)
                return;

            Logger.Info("Loading chip database from chiplist.xml...");
            _chipDatabase = ChipDatabaseLoader.LoadDatabase();

            if (_chipDatabase.Count == 0)
            {
                Logger.Warn("No chips loaded from database, using minimal hardcoded fallback");
                _chipDatabase = GetHardcodedChipDatabase();
            }
            else
            {
                // Validate database
                if (!ChipDatabaseLoader.ValidateDatabase(_chipDatabase))
                {
                    Logger.Warn("Database validation found issues, but continuing with loaded data");
                }
            }

            _databaseLoaded = true;
            Logger.Info($"Chip database loaded: {_chipDatabase.Count} chips available");
        }

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

        /// <summary>
        /// Detects chip and returns list of possible candidates.
        /// If only one match, returns single-item list. If multiple matches, user should select.
        /// </summary>
        public async Task<OperationResult<List<ChipInfo>>> DetectChipCandidatesAsync(ProtocolType protocol, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_hardware == null || !_hardware.IsConnected)
                {
                    Logger.Error("DetectChipCandidatesAsync called but hardware not connected");
                    return OperationResult<List<ChipInfo>>.FailureResult("Hardware not connected");
                }

                Logger.Info($"Detecting chip candidates using {protocol} protocol...");

                switch (protocol)
                {
                    case ProtocolType.SPI:
                        return await DetectSpiChipCandidatesAsync(cancellationToken);
                    
                    case ProtocolType.I2C:
                    case ProtocolType.MicroWire:
                        Logger.Warn($"{protocol} detection not implemented");
                        return OperationResult<List<ChipInfo>>.FailureResult($"{protocol} detection not implemented");
                    
                    default:
                        Logger.Error($"Unknown protocol: {protocol}");
                        return OperationResult<List<ChipInfo>>.FailureResult($"Unknown protocol: {protocol}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Chip detection failed with exception");
                return OperationResult<List<ChipInfo>>.FailureResult($"Chip detection failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility. Returns first candidate.
        /// </summary>
        public async Task<OperationResult<ChipInfo>> DetectChipAsync(ProtocolType protocol, CancellationToken cancellationToken = default)
        {
            var candidatesResult = await DetectChipCandidatesAsync(protocol, cancellationToken);
            
            if (!candidatesResult.Success)
            {
                return OperationResult<ChipInfo>.FailureResult(candidatesResult.Message);
            }

            if (candidatesResult.Data.Count == 0)
            {
                return OperationResult<ChipInfo>.FailureResult("No chip detected");
            }

            return OperationResult<ChipInfo>.SuccessResult(candidatesResult.Data[0], candidatesResult.Message);
        }

        private async Task<OperationResult<List<ChipInfo>>> DetectSpiChipCandidatesAsync(CancellationToken cancellationToken)
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
                    return OperationResult<List<ChipInfo>>.FailureResult($"Failed to initialize SPI: {initResult.Message}");
                }

                // Read chip ID
                var idResult = await _spiProtocol.ReadIdAsync(cancellationToken);
                if (!idResult.Success)
                {
                    Logger.Error($"Failed to read chip ID: {idResult.Message}");
                    return OperationResult<List<ChipInfo>>.FailureResult($"Failed to read chip ID: {idResult.Message}");
                }

                Logger.Info($"Read chip ID - Manufacturer: 0x{idResult.Data.ManufacturerId:X2}, Device: 0x{idResult.Data.DeviceId:X4}");

                // Look up chip candidates in database
                var candidates = FindChipCandidatesByMemoryId(idResult.Data);
                
                if (candidates.Count == 0)
                {
                    Logger.Warn("Chip not found in database - creating generic chip info");
                    // Unknown chip - create basic info from ID
                    var unknownChip = new ChipInfo
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
                        Voltage = 3300,
                        Description = $"Unknown chip with ID: {idResult.Data.ManufacturerId:X2}-{idResult.Data.DeviceId:X4}"
                    };
                    candidates.Add(unknownChip);
                }

                // Use smart selection strategy for multiple candidates
                var selectedChip = SelectBestCandidate(candidates);
                
                // Set selected chip as the active chip for the protocol
                _spiProtocol.ChipInfo = selectedChip;

                string successMsg = $"Detected: {selectedChip.Name} ({selectedChip.SizeKB}KB)";
                Logger.Info(successMsg);

                // Return single-item list for API compatibility
                return OperationResult<List<ChipInfo>>.SuccessResult(new List<ChipInfo> { selectedChip }, successMsg);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "SPI detection failed with exception");
                return OperationResult<List<ChipInfo>>.FailureResult($"SPI detection failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Smart selection strategy for multiple chip candidates.
        /// If same size: fold names like "MX25Q128FV(JV/RV)"
        /// If different sizes: use largest chip
        /// Returns a single ChipInfo representing the best choice.
        /// </summary>
        private ChipInfo SelectBestCandidate(List<ChipInfo> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            if (candidates.Count == 1)
                return candidates[0];

            // Group by size
            var sizeGroups = candidates.GroupBy(c => c.Size).OrderByDescending(g => g.Key).ToList();

            if (sizeGroups.Count == 1)
            {
                // All candidates have same size - fold names
                var size = sizeGroups[0].Key;
                var chips = sizeGroups[0].ToList();
                
                // Use first chip as base
                var baseChip = chips[0];
                
                // Create folded name
                string foldedName = CreateFoldedName(chips);
                
                Logger.Info($"Multiple candidates with same size ({size} bytes), folding to: {foldedName}");
                
                // Clone the chip with folded name
                var result = new ChipInfo
                {
                    Name = foldedName,
                    Manufacturer = baseChip.Manufacturer,
                    ProtocolType = baseChip.ProtocolType,
                    SpiCommandSet = baseChip.SpiCommandSet,
                    ManufacturerId = baseChip.ManufacturerId,
                    DeviceId = baseChip.DeviceId,
                    Size = baseChip.Size,
                    PageSize = baseChip.PageSize,
                    SectorSize = baseChip.SectorSize,
                    BlockSize = baseChip.BlockSize,
                    Voltage = baseChip.Voltage,
                    Description = $"Multiple variants: {string.Join(", ", chips.Select(c => c.Name))}"
                };
                
                return result;
            }
            else
            {
                // Different sizes - use largest
                var largestGroup = sizeGroups[0]; // Already sorted descending
                var largestChip = largestGroup.First();
                
                Logger.Info($"Multiple candidates with different sizes, using largest: {largestChip.Name} ({largestChip.SizeKB}KB)");
                Logger.Debug($"Available sizes: {string.Join(", ", sizeGroups.Select(g => $"{g.Key / 1024}KB"))}");
                
                return largestChip;
            }
        }

        /// <summary>
        /// Creates a folded name from multiple chips with same size.
        /// Example: ["MX25Q128FV", "MX25Q128JV", "MX25Q128RV"] -> "MX25Q128FV(JV/RV)"
        /// </summary>
        private string CreateFoldedName(List<ChipInfo> chips)
        {
            if (chips.Count == 1)
                return chips[0].Name;

            // Sort by name for consistent output
            var sortedNames = chips.Select(c => c.Name).OrderBy(n => n).ToList();
            
            // Find common prefix
            string baseName = sortedNames[0];
            string commonPrefix = baseName;
            
            foreach (var name in sortedNames.Skip(1))
            {
                int i = 0;
                while (i < commonPrefix.Length && i < name.Length && commonPrefix[i] == name[i])
                    i++;
                commonPrefix = commonPrefix.Substring(0, i);
            }

            // Extract suffixes (the unique parts)
            var suffixes = new List<string>();
            foreach (var name in sortedNames)
            {
                if (name.Length > commonPrefix.Length)
                {
                    suffixes.Add(name.Substring(commonPrefix.Length));
                }
            }

            if (suffixes.Count == 0)
            {
                // All names identical? Just return first
                return baseName;
            }

            // Build folded name: "MX25Q128FV(JV/RV)"
            string firstSuffix = suffixes[0];
            string otherSuffixes = string.Join("/", suffixes.Skip(1));
            
            return $"{commonPrefix}{firstSuffix}({otherSuffixes})";
        }

        #endregion

        #region Chip Database

        /// <summary>
        /// Finds all chip candidates matching the given memory ID.
        /// Returns list of possible matches (may be multiple chips with same ID).
        /// </summary>
        private List<ChipInfo> FindChipCandidatesByMemoryId(MemoryId memoryId)
        {
            var candidates = new List<ChipInfo>();

            if (memoryId == null)
                return candidates;

            // Ensure database is loaded
            EnsureDatabaseLoaded();

            // Primary match: Use JEDEC ID (bytes 1 and 2) if available
            if (memoryId.JedecId != null && memoryId.JedecId.Length >= 3)
            {
                byte jedecManufacturer = memoryId.JedecId[0];
                ushort jedecDeviceId = (ushort)((memoryId.JedecId[1] << 8) | memoryId.JedecId[2]);

                foreach (var chip in _chipDatabase)
                {
                    if (chip.ManufacturerId == jedecManufacturer &&
                        chip.DeviceId == jedecDeviceId)
                    {
                        Logger.Debug($"Chip matched by JEDEC ID: {jedecManufacturer:X2}-{memoryId.JedecId[1]:X2}-{memoryId.JedecId[2]:X2} -> {chip.Name}");
                        candidates.Add(chip);
                    }
                }
            }

            // Fallback match: Try Manufacturer ID and Device ID from 0x90 command
            if (candidates.Count == 0)
            {
                foreach (var chip in _chipDatabase)
                {
                    if (chip.ManufacturerId == memoryId.ManufacturerId &&
                        chip.DeviceId == memoryId.DeviceId)
                    {
                        Logger.Debug($"Chip matched by MFR/Device ID: {memoryId.ManufacturerId:X2}-{memoryId.DeviceId:X4} -> {chip.Name}");
                        candidates.Add(chip);
                    }
                }
            }

            if (candidates.Count > 0)
            {
                Logger.Info($"Found {candidates.Count} chip candidate(s) for ID {memoryId.ManufacturerId:X2}-{memoryId.DeviceId:X4}");
                foreach (var candidate in candidates)
                {
                    Logger.Debug($"  - {candidate.Name} ({candidate.Manufacturer}, {candidate.SizeKB}KB)");
                }
            }

            return candidates;
        }

        /// <summary>
        /// Legacy method for backward compatibility. Returns first candidate or null.
        /// </summary>
        private ChipInfo FindChipByMemoryId(MemoryId memoryId)
        {
            var candidates = FindChipCandidatesByMemoryId(memoryId);
            return candidates.Count > 0 ? candidates[0] : null;
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
                Logger.Debug($"ReadMemoryAsync: address=0x{address:X6}, length={length}");
                
                if (_spiProtocol != null)
                {
                    var progressHandler = CreateProgressHandler(progress);
                    var result = await _spiProtocol.ReadAsync(address, length, progressHandler, cancellationToken);
                    Logger.Debug($"ReadMemoryAsync: SPI read completed, success={result.Success}");
                    return result;
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

        #region Clear Flash Operations

        /// <summary>
        /// Clears (erases) the entire flash chip and verifies 1% of random addresses
        /// </summary>
        /// <param name="progress">Progress reporter for UI updates</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>OperationResult with verification statistics</returns>
        public async Task<OperationResult> ClearFlashAsync(IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validation
                if (_hardware == null || !_hardware.IsConnected)
                {
                    Logger.Error("ClearFlashAsync called but hardware not connected");
                    return OperationResult.FailureResult("Hardware not connected");
                }

                if (_spiProtocol == null)
                {
                    Logger.Error("ClearFlashAsync called but SPI protocol not initialized");
                    return OperationResult.FailureResult("No protocol initialized");
                }

                if (_spiProtocol.ChipInfo == null)
                {
                    Logger.Error("ClearFlashAsync called but chip not detected");
                    return OperationResult.FailureResult("No chip detected");
                }

                var chipSize = _spiProtocol.ChipInfo.Size;
                Logger.Info($"Starting clear flash operation for chip size: {chipSize} bytes ({chipSize / 1024}KB)");

                var progressHandler = CreateProgressHandler(progress);

                // Phase 1: Erase chip (0-50% progress)
                Logger.Info("Phase 1: Erasing chip...");
                progressHandler?.Report(new ProgressInfo(0, 100, "Erasing chip... (this may take 30-60 seconds)"));

                var eraseResult = await EraseChipAsync(progressHandler, cancellationToken);
                if (!eraseResult.Success)
                {
                    Logger.Error($"Chip erase failed: {eraseResult.Message}");
                    return OperationResult.FailureResult($"Erase failed: {eraseResult.Message}");
                }

                Logger.Info("Phase 1 complete: Chip erased successfully");
                progressHandler?.Report(new ProgressInfo(50, 100, "Chip erased successfully"));

                // Phase 2: Random verification (50-100% progress)
                Logger.Info("Phase 2: Starting random verification...");
                
                // Calculate sample count: Much smaller for practical verification
                // For 1MB chip: 1000 samples, for 8MB chip: 2000 samples, max 2000
                int sampleCount = Math.Min(2000, Math.Max(100, (int)(chipSize / 1024)));
                Logger.Info($"Will verify {sampleCount} random addresses (optimized for performance)");

                var verificationResult = await VerifyRandomAddressesAsync((uint)chipSize, sampleCount, progressHandler, cancellationToken);
                
                if (verificationResult.Success)
                {
                    var message = $"Clear completed: {verificationResult.Data.verified} addresses verified, {verificationResult.Data.failures.Count} failures";
                    Logger.Info(message);
                    return OperationResult.SuccessResult(message);
                }
                else
                {
                    Logger.Error($"Verification failed: {verificationResult.Message}");
                    return OperationResult.FailureResult($"Verification failed: {verificationResult.Message}");
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("Clear flash operation cancelled");
                return OperationResult.FailureResult("Clear operation cancelled");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Clear flash operation failed with exception");
                return OperationResult.FailureResult($"Clear flash failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generates random addresses for verification sampling
        /// </summary>
        private HashSet<uint> GenerateRandomAddresses(uint chipSize, int sampleCount)
        {
            var random = new Random();
            var addresses = new HashSet<uint>();
            
            // Generate addresses aligned to 256-byte boundaries (page boundaries)
            int maxPages = (int)(chipSize / 256);
            
            while (addresses.Count < sampleCount)
            {
                // Generate random page-aligned address
                uint pageIndex = (uint)random.Next(maxPages);
                uint addr = pageIndex * 256;
                addresses.Add(addr);
            }
            
            Logger.Debug($"Generated {addresses.Count} random addresses for verification");
            return addresses;
        }

        /// <summary>
        /// Verifies random addresses are all 0xFF (erased state)
        /// </summary>
        private async Task<OperationResult<(int verified, List<uint> failures)>> VerifyRandomAddressesAsync(
            uint chipSize, 
            int sampleCount, 
            IProgress<ProgressInfo> progress, 
            CancellationToken cancellationToken)
        {
            try
            {
                var addresses = GenerateRandomAddresses(chipSize, sampleCount);
                var failures = new List<uint>();
                int count = 0;
                var startTime = DateTime.Now;
                
                Logger.Info($"Starting verification of {addresses.Count} addresses...");
                Logger.Debug($"First 10 addresses to verify: {string.Join(", ", addresses.Take(10).Select(a => $"0x{a:X6}"))}");
                
                foreach (var addr in addresses.OrderBy(a => a))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var addressStartTime = DateTime.Now;
                    Logger.Debug($"Verifying address 0x{addr:X6} ({count + 1}/{addresses.Count})...");
                    
                    // Read only 64 bytes for verification (much faster than 256)
                    var readResult = await ReadMemoryAsync(addr, 64, null, cancellationToken);
                    
                    var addressElapsed = DateTime.Now - addressStartTime;
                    Logger.Debug($"Address 0x{addr:X6} read completed in {addressElapsed.TotalMilliseconds:F1}ms");
                    
                    if (!readResult.Success)
                    {
                        Logger.Warn($"Failed to read address 0x{addr:X6}: {readResult.Message}");
                        failures.Add(addr);
                    }
                    else
                    {
                        // Check if all bytes are 0xFF (erased state)
                        if (!readResult.Data.All(b => b == 0xFF))
                        {
                            // Find first non-0xFF byte for logging
                            int firstNonFF = Array.FindIndex(readResult.Data, b => b != 0xFF);
                            Logger.Warn($"Address 0x{addr:X6} not erased: found 0x{readResult.Data[firstNonFF]:X2} at offset {firstNonFF}");
                            failures.Add(addr);
                        }
                        else
                        {
                            Logger.Debug($"Address 0x{addr:X6} verified as erased (all 0xFF)");
                        }
                    }
                    
                    count++;
                    
                    // Report progress (50-100%) and log every 100 addresses
                    int progressPercent = 50 + (count * 50 / addresses.Count);
                    progress?.Report(new ProgressInfo(count, addresses.Count, $"Verifying {count}/{addresses.Count} addresses..."));
                    
                    if (count % 50 == 0 || count == addresses.Count)
                    {
                        var elapsed = DateTime.Now - startTime;
                        var avgTimePerAddress = elapsed.TotalMilliseconds / count;
                        var estimatedRemaining = TimeSpan.FromMilliseconds(avgTimePerAddress * (addresses.Count - count));
                        Logger.Info($"Verification progress: {count}/{addresses.Count} ({progressPercent}%) - Avg: {avgTimePerAddress:F1}ms/addr - ETA: {estimatedRemaining:mm\\:ss}");
                    }
                }
                
                Logger.Info($"Verification complete: {count} addresses checked, {failures.Count} failures");
                
                // Build result message
                string message;
                if (failures.Count == 0)
                {
                    message = $"All {count} addresses verified successfully";
                }
                else
                {
                    var failureList = string.Join(", ", failures.Take(5).Select(a => $"0x{a:X6}"));
                    if (failures.Count > 5)
                        failureList += $" and {failures.Count - 5} more";
                    message = $"Verification failed at addresses: {failureList}";
                }
                
                return OperationResult<(int verified, List<uint> failures)>.SuccessResult(
                    (count, failures), 
                    message);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Random address verification failed");
                return OperationResult<(int verified, List<uint> failures)>.FailureResult(
                    $"Verification failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Clears (erases) the entire flash chip and verifies by reading the whole ROM
        /// </summary>
        /// <param name="progress">Progress reporter for UI updates</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>OperationResult with verification statistics</returns>
        public async Task<OperationResult> ClearFlashWholeRomAsync(IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validation
                if (_hardware == null || !_hardware.IsConnected)
                {
                    Logger.Error("ClearFlashWholeRomAsync called but hardware not connected");
                    return OperationResult.FailureResult("Hardware not connected");
                }

                if (_spiProtocol == null)
                {
                    Logger.Error("ClearFlashWholeRomAsync called but SPI protocol not initialized");
                    return OperationResult.FailureResult("No protocol initialized");
                }

                if (_spiProtocol.ChipInfo == null)
                {
                    Logger.Error("ClearFlashWholeRomAsync called but chip not detected");
                    return OperationResult.FailureResult("No chip detected");
                }

                var chipSize = _spiProtocol.ChipInfo.Size;
                Logger.Info($"Starting clear flash operation for chip size: {chipSize} bytes ({chipSize / 1024}KB)");

                var progressHandler = CreateProgressHandler(progress);

                // Phase 1: Erase chip (0-100% progress)
                Logger.Info("Phase 1: Erasing chip...");
                progressHandler?.Report(new ProgressInfo(0, 100, "Phase 1: Erasing chip... (this may take 30-60 seconds)"));

                var eraseResult = await EraseChipAsync(progressHandler, cancellationToken);
                if (!eraseResult.Success)
                {
                    Logger.Error($"Chip erase failed: {eraseResult.Message}");
                    return OperationResult.FailureResult($"Erase failed: {eraseResult.Message}");
                }

                Logger.Info("Phase 1 complete: Chip erased successfully");
                progressHandler?.Report(new ProgressInfo(100, 100, "Phase 1 Complete: Chip erased successfully"));

                // Phase 2: Read entire ROM to verify (0-100% progress - reset)
                Logger.Info("Phase 2: Reading entire ROM to verify clearing...");
                progressHandler?.Report(new ProgressInfo(0, 100, "Phase 2: Reading entire ROM to verify clearing..."));

                var readResult = await ReadMemoryAsync(0, chipSize, progressHandler, cancellationToken);
                if (!readResult.Success)
                {
                    Logger.Error($"Failed to read entire ROM: {readResult.Message}");
                    return OperationResult.FailureResult($"ROM read failed: {readResult.Message}");
                }

                Logger.Info("Phase 2 complete: Entire ROM read successfully");
                progressHandler?.Report(new ProgressInfo(100, 100, "Phase 2 Complete: Entire ROM read successfully"));

                // Phase 3: Verify all bytes are 0xFF (erased state) - quick verification
                Logger.Info("Phase 3: Verifying all bytes are erased (0xFF)...");
                progressHandler?.Report(new ProgressInfo(0, 100, "Phase 3: Verifying all bytes are erased..."));

                var verificationResult = VerifyEntireRom(readResult.Data);
                
                if (verificationResult.Success)
                {
                    var message = $"Clear completed: Entire ROM verified as cleared ({chipSize:N0} bytes all 0xFF)";
                    Logger.Info(message);
                    progressHandler?.Report(new ProgressInfo(100, 100, "Phase 3 Complete: All bytes verified as erased"));
                    return OperationResult.SuccessResult(message);
                }
                else
                {
                    Logger.Error($"Verification failed: {verificationResult.Message}");
                    return OperationResult.FailureResult($"Verification failed: {verificationResult.Message}");
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("Clear flash operation cancelled");
                return OperationResult.FailureResult("Clear operation cancelled");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Clear flash operation failed with exception");
                return OperationResult.FailureResult($"Clear flash failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Verifies that all bytes in the ROM data are 0xFF (erased state)
        /// </summary>
        private OperationResult VerifyEntireRom(byte[] romData)
        {
            try
            {
                if (romData == null || romData.Length == 0)
                {
                    return OperationResult.FailureResult("ROM data is null or empty");
                }

                Logger.Info($"Verifying {romData.Length:N0} bytes are all 0xFF...");

                // Find first non-0xFF byte
                int firstNonFF = Array.FindIndex(romData, b => b != 0xFF);
                
                if (firstNonFF == -1)
                {
                    // All bytes are 0xFF
                    Logger.Info($"Verification successful: All {romData.Length:N0} bytes are 0xFF (erased)");
                    return OperationResult.SuccessResult($"All {romData.Length:N0} bytes verified as erased (0xFF)");
                }
                else
                {
                    // Found non-0xFF byte
                    byte nonFFByte = romData[firstNonFF];
                    Logger.Warn($"Verification failed: Found 0x{nonFFByte:X2} at address 0x{firstNonFF:X6}");
                    
                    // Count total non-0xFF bytes for statistics
                    int nonFFCount = romData.Count(b => b != 0xFF);
                    Logger.Warn($"Total non-0xFF bytes found: {nonFFCount:N0} out of {romData.Length:N0}");
                    
                    return OperationResult.FailureResult($"ROM not fully erased: Found 0x{nonFFByte:X2} at address 0x{firstNonFF:X6} ({nonFFCount:N0} non-0xFF bytes total)");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "ROM verification failed with exception");
                return OperationResult.FailureResult($"Verification failed: {ex.Message}", ex);
            }
        }

        #endregion

        #region Flash Operations

        /// <summary>
        /// Flash binary file to chip starting at address 0x000000
        /// Assumes chip is already erased
        /// </summary>
        public async Task<OperationResult> FlashAsync(
            string filePath, 
            IProgress<ProgressInfo> progress = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.Info($"Starting flash operation for file: {filePath}");

                // 1. Validate inputs
                var validationResult = await ValidateFlashInputsAsync(filePath, cancellationToken);
                if (!validationResult.Success)
                {
                    return validationResult;
                }

                // 2. Load file
                Logger.Info($"Loading binary file: {filePath}");
                byte[] fileData;
                try
                {
                    fileData = File.ReadAllBytes(filePath);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to read file: {filePath}");
                    return OperationResult.FailureResult($"Failed to read file: {ex.Message}", ex);
                }

                Logger.Info($"File loaded: {fileData.Length:N0} bytes");
                Logger.Info($"Chip size: {_spiProtocol.ChipInfo.Size:N0} bytes");
                Logger.Info($"Page size: {_spiProtocol.ChipInfo.PageSize} bytes");

                // 3. Write data using existing WriteMemoryAsync
                var progressHandler = CreateProgressHandler(progress);
                var writeResult = await WriteMemoryAsync(0, fileData, progressHandler, cancellationToken);

                if (writeResult.Success)
                {
                    Logger.Info($"Flash operation completed successfully: {fileData.Length:N0} bytes written");
                    return OperationResult.SuccessResult($"Flash completed: {fileData.Length:N0} bytes written to address 0x000000");
                }
                else
                {
                    Logger.Error($"Flash operation failed: {writeResult.Message}");
                    return writeResult;
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("Flash operation cancelled");
                return OperationResult.FailureResult("Flash operation cancelled");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Flash operation failed with exception");
                return OperationResult.FailureResult($"Flash operation failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Flash binary file with immediate page-by-page verification
        /// Assumes chip is already erased
        /// </summary>
        public async Task<OperationResult> FlashWithVerifyAsync(
            string filePath,
            IProgress<ProgressInfo> progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.Info($"Starting flash with verify operation for file: {filePath}");

                // 1. Validate inputs
                var validationResult = await ValidateFlashInputsAsync(filePath, cancellationToken);
                if (!validationResult.Success)
                {
                    return validationResult;
                }

                // 2. Load file
                Logger.Info($"Loading binary file: {filePath}");
                byte[] fileData;
                try
                {
                    fileData = File.ReadAllBytes(filePath);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to read file: {filePath}");
                    return OperationResult.FailureResult($"Failed to read file: {ex.Message}", ex);
                }

                Logger.Info($"File loaded: {fileData.Length:N0} bytes");
                Logger.Info($"Chip size: {_spiProtocol.ChipInfo.Size:N0} bytes");
                Logger.Info($"Page size: {_spiProtocol.ChipInfo.PageSize} bytes");

                // 3. Get page size from chip info
                int pageSize = _spiProtocol.ChipInfo.PageSize;
                Logger.Debug($"Using page size: {pageSize} bytes");

                // 4. Write page-by-page with immediate verification
                var stopwatch = Stopwatch.StartNew();
                int bytesWritten = 0;
                uint currentAddress = 0;

                while (bytesWritten < fileData.Length)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Calculate bytes to write in this page
                    int remainingBytes = fileData.Length - bytesWritten;
                    int bytesInPage = Math.Min(pageSize, remainingBytes);

                    // Extract page data
                    byte[] pageData = new byte[bytesInPage];
                    Array.Copy(fileData, bytesWritten, pageData, 0, bytesInPage);

                    Logger.Debug($"Writing page at address 0x{currentAddress:X6}, size: {bytesInPage} bytes");

                    // Write page
                    var writeResult = await _spiProtocol.WritePageAsync(currentAddress, pageData, cancellationToken);
                    if (!writeResult.Success)
                    {
                        Logger.Error($"Page write failed at address 0x{currentAddress:X6}: {writeResult.Message}");
                        return OperationResult.FailureResult($"Page write failed at address 0x{currentAddress:X6}: {writeResult.Message}");
                    }

                    Logger.Debug($"Page written successfully, now verifying...");

                    // Read back page for verification
                    var readResult = await _spiProtocol.ReadAsync(currentAddress, bytesInPage, null, cancellationToken);
                    if (!readResult.Success)
                    {
                        Logger.Error($"Page read failed at address 0x{currentAddress:X6}: {readResult.Message}");
                        return OperationResult.FailureResult($"Page read failed at address 0x{currentAddress:X6}: {readResult.Message}");
                    }

                    // Compare data byte-by-byte
                    bool dataMatches = true;
                    int firstMismatchIndex = -1;
                    for (int i = 0; i < bytesInPage; i++)
                    {
                        if (readResult.Data[i] != pageData[i])
                        {
                            dataMatches = false;
                            firstMismatchIndex = i;
                            break;
                        }
                    }

                    if (!dataMatches)
                    {
                        uint errorAddress = currentAddress + (uint)firstMismatchIndex;
                        byte expectedByte = pageData[firstMismatchIndex];
                        byte actualByte = readResult.Data[firstMismatchIndex];
                        
                        Logger.Error($"Verify failed at address 0x{errorAddress:X6}: expected 0x{expectedByte:X2}, got 0x{actualByte:X2}");
                        return OperationResult.FailureResult($"Verify failed at address 0x{errorAddress:X6}: expected 0x{expectedByte:X2}, got 0x{actualByte:X2}");
                    }

                    Logger.Debug($"Page verified successfully at address 0x{currentAddress:X6}");

                    // Update counters
                    bytesWritten += bytesInPage;
                    currentAddress += (uint)bytesInPage;

                    // Report progress
                    if (progress != null)
                    {
                        var progressInfo = new ProgressInfo(bytesWritten, fileData.Length, $"Flashing with verify... {stopwatch.Elapsed.TotalSeconds:F1}s");
                        progress.Report(progressInfo);
                        OnProgressChanged(progressInfo);
                    }
                }

                stopwatch.Stop();
                Logger.Info($"Flash with verify completed successfully: {fileData.Length:N0} bytes written and verified in {stopwatch.Elapsed.TotalSeconds:F2}s ({fileData.Length / stopwatch.Elapsed.TotalSeconds / 1024:F1} KB/s)");
                
                return OperationResult.SuccessResult($"Flash with verify completed: {fileData.Length:N0} bytes written and verified to address 0x000000");
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("Flash with verify operation cancelled");
                return OperationResult.FailureResult("Flash with verify operation cancelled");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Flash with verify operation failed with exception");
                return OperationResult.FailureResult($"Flash with verify operation failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Validates inputs for flash operations
        /// </summary>
        private async Task<OperationResult> ValidateFlashInputsAsync(string filePath, CancellationToken cancellationToken)
        {
            // Check hardware connected
            if (_hardware == null || !_hardware.IsConnected)
            {
                Logger.Error("ValidateFlashInputsAsync called but hardware not connected");
                return OperationResult.FailureResult("Hardware not connected");
            }

            // Check protocol initialized
            if (_spiProtocol == null)
            {
                Logger.Error("ValidateFlashInputsAsync called but SPI protocol not initialized");
                return OperationResult.FailureResult("No protocol initialized");
            }

            // Check chip detected
            if (_spiProtocol.ChipInfo == null)
            {
                Logger.Error("ValidateFlashInputsAsync called but chip not detected");
                return OperationResult.FailureResult("No chip detected");
            }

            // Check file path
            if (string.IsNullOrWhiteSpace(filePath))
            {
                Logger.Error("ValidateFlashInputsAsync called with null or empty file path");
                return OperationResult.FailureResult("File path is required");
            }

            // Check file exists
            if (!File.Exists(filePath))
            {
                Logger.Error($"File does not exist: {filePath}");
                return OperationResult.FailureResult($"File does not exist: {filePath}");
            }

            // Check file extension
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension != ".bin")
            {
                Logger.Error($"Invalid file extension: {extension}. Only .bin files are supported");
                return OperationResult.FailureResult($"Invalid file extension: {extension}. Only .bin files are supported");
            }

            // Check file size
            try
            {
                var fileInfo = new FileInfo(filePath);
                long fileSize = fileInfo.Length;
                long chipSize = _spiProtocol.ChipInfo.Size;

                Logger.Debug($"File size: {fileSize:N0} bytes, Chip size: {chipSize:N0} bytes");

                if (fileSize > chipSize)
                {
                    Logger.Error($"File size ({fileSize:N0} bytes) exceeds chip size ({chipSize:N0} bytes)");
                    return OperationResult.FailureResult($"File size ({fileSize:N0} bytes) exceeds chip size ({chipSize:N0} bytes)");
                }

                if (fileSize == 0)
                {
                    Logger.Error("File is empty");
                    return OperationResult.FailureResult("File is empty");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to check file size: {filePath}");
                return OperationResult.FailureResult($"Failed to check file size: {ex.Message}", ex);
            }

            return OperationResult.SuccessResult("Flash inputs validated successfully");
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
                OnProgressChanged(info);
            });
        }

        protected virtual void OnProgressChanged(ProgressInfo progressInfo)
        {
            ProgressChanged?.Invoke(this, progressInfo);
        }

        #endregion
    }
}
