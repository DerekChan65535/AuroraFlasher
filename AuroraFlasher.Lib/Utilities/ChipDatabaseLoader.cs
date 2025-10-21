using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AuroraFlasher.Logging;
using AuroraFlasher.Models;

namespace AuroraFlasher.Utilities
{
    /// <summary>
    /// Loads and parses the chiplist.xml database file.
    /// </summary>
    public class ChipDatabaseLoader
    {
        private const string DefaultDatabaseFileName = "chiplist.xml";

        /// <summary>
        /// Loads chip database from the specified XML file path.
        /// </summary>
        /// <param name="filePath">Path to chiplist.xml file. If null, looks in current directory.</param>
        /// <returns>List of ChipInfo objects parsed from the XML, or empty list if file not found/invalid.</returns>
        public static List<ChipInfo> LoadDatabase(string filePath = null)
        {
            var chips = new List<ChipInfo>();

            try
            {
                // Default to chiplist.xml in current directory
                if (string.IsNullOrEmpty(filePath))
                {
                    var executablePath = AppDomain.CurrentDomain.BaseDirectory;
                    filePath = Path.Combine(executablePath, DefaultDatabaseFileName);
                }

                if (!File.Exists(filePath))
                {
                    Logger.Warn($"Chip database file not found: {filePath}");
                    return chips;
                }

                Logger.Info($"Loading chip database from: {filePath}");

                var xmlDoc = XDocument.Load(filePath);
                var root = xmlDoc.Element("chiplist");

                if (root == null)
                {
                    Logger.Error("Invalid chiplist.xml: Missing <chiplist> root element");
                    return chips;
                }

                // Iterate through protocol types (SPI, I2C, Microwire)
                foreach (var protocolNode in root.Elements())
                {
                    var protocolName = protocolNode.Name.LocalName;
                    var protocolType = ParseProtocolType(protocolName);

                    // Iterate through manufacturer nodes
                    foreach (var manufacturerNode in protocolNode.Elements())
                    {
                        var manufacturerName = manufacturerNode.Name.LocalName;

                        // Iterate through chip nodes
                        foreach (var chipNode in manufacturerNode.Elements())
                        {
                            try
                            {
                                var chip = ParseChipNode(chipNode, protocolType, manufacturerName);
                                if (chip != null)
                                {
                                    chips.Add(chip);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Warn($"Failed to parse chip '{chipNode.Name.LocalName}': {ex.Message}");
                            }
                        }
                    }
                }

                Logger.Info($"Loaded {chips.Count} chips from database");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load chip database from {filePath}");
            }

            return chips;
        }

        /// <summary>
        /// Parses a single chip node from the XML.
        /// </summary>
        private static ChipInfo ParseChipNode(XElement chipNode, ProtocolType protocolType, string manufacturerName)
        {
            var chipName = chipNode.Name.LocalName;

            // Extract attributes
            var idAttr = (string)chipNode.Attribute("id");
            var sizeAttr = (string)chipNode.Attribute("size");
            var pageAttr = (string)chipNode.Attribute("page");
            var spicmdAttr = (string)chipNode.Attribute("spicmd");
            var scriptAttr = (string)chipNode.Attribute("script");
            var addrtypeAttr = (string)chipNode.Attribute("addrtype");
            var addrbitlenAttr = (string)chipNode.Attribute("addrbitlen");

            // Parse chip ID (JEDEC format: 3 hex bytes like "EF4016")
            byte manufacturerId = 0;
            ushort deviceId = 0;
            byte[] jedecId = null;

            if (!string.IsNullOrEmpty(idAttr))
            {
                jedecId = ParseHexId(idAttr);
                if (jedecId != null && jedecId.Length >= 3)
                {
                    manufacturerId = jedecId[0];
                    deviceId = (ushort)((jedecId[1] << 8) | jedecId[2]);
                }
            }

            // Parse size (in bytes)
            var size = 0;
            if (!string.IsNullOrEmpty(sizeAttr) && int.TryParse(sizeAttr, out var parsedSize))
            {
                size = parsedSize;
            }

            // Parse page size
            var pageSize = 256; // Default
            if (!string.IsNullOrEmpty(pageAttr))
            {
                if (pageAttr.ToUpper() == "SSTB")
                {
                    pageSize = -1; // SST AAI Byte program
                }
                else if (pageAttr.ToUpper() == "SSTW")
                {
                    pageSize = -2; // SST AAI Word program
                }
                else if (int.TryParse(pageAttr, out var parsedPage))
                {
                    pageSize = parsedPage;
                }
            }

            // Parse SPI command set
            var spiCommandSet = SpiCommandSet.Series25; // Default
            if (!string.IsNullOrEmpty(spicmdAttr))
            {
                spiCommandSet = ParseSpiCommandSet(spicmdAttr);
            }

            // Determine protocol type based on attributes
            if (!string.IsNullOrEmpty(addrbitlenAttr))
            {
                protocolType = ProtocolType.MicroWire;
            }
            else if (!string.IsNullOrEmpty(addrtypeAttr))
            {
                protocolType = ProtocolType.I2C;
            }
            else if (protocolType == ProtocolType.None)
            {
                protocolType = ProtocolType.SPI; // Default to SPI
            }

            // Parse voltage from chip name if specified (e.g., "W25Q80BW_1.8V")
            var voltage = ParseVoltageFromName(chipName);

            // Create ChipInfo object
            var chip = new ChipInfo
            {
                Name = chipName,
                Manufacturer = ParseManufacturer(manufacturerName),
                ProtocolType = protocolType,
                SpiCommandSet = spiCommandSet,
                Size = size,
                PageSize = pageSize,
                SectorSize = (protocolType == ProtocolType.SPI) ? 4096 : 0,
                BlockSize = (protocolType == ProtocolType.SPI) ? 65536 : 0,
                ManufacturerId = manufacturerId,
                DeviceId = deviceId,
                Voltage = voltage,
                Supports4ByteAddress = false,
                Description = $"{manufacturerName} {chipName}"
            };

            return chip;
        }

        /// <summary>
        /// Parses voltage from chip name if specified (e.g., "W25Q80BW_1.8V" → 1800).
        /// Returns voltage in millivolts. Defaults to 3300 (3.3V) if not specified.
        /// </summary>
        private static int ParseVoltageFromName(string chipName)
        {
            if (string.IsNullOrEmpty(chipName))
                return 3300;

            // Common voltage patterns in chip names: "_1.8V", "_2.5V", "_3.3V", etc.
            var voltagePatterns = new Dictionary<string, int>
            {
                { "_1.8V", 1800 },
                { "_2.5V", 2500 },
                { "_3.3V", 3300 },
                { "_5V", 5000 }
            };

            // Use IndexOf for case-insensitive search (.NET Framework 4.8 compatible)
            var chipNameUpper = chipName.ToUpperInvariant();
            foreach (var pattern in voltagePatterns)
            {
                if (chipNameUpper.IndexOf(pattern.Key.ToUpperInvariant(), StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return pattern.Value;
                }
            }

            // Default to 3.3V if no voltage specified in name
            return 3300;
        }

        /// <summary>
        /// Parses hex ID string (e.g., "EF4016") into byte array.
        /// </summary>
        private static byte[] ParseHexId(string hexId)
        {
            try
            {
                hexId = hexId.Trim().ToUpper();
                if (hexId.Length % 2 != 0)
                {
                    Logger.Warn($"Invalid hex ID length: {hexId}");
                    return null;
                }

                var byteCount = hexId.Length / 2;
                var bytes = new byte[byteCount];

                for (var i = 0; i < byteCount; i++)
                {
                    var byteStr = hexId.Substring(i * 2, 2);
                    bytes[i] = Convert.ToByte(byteStr, 16);
                }

                return bytes;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to parse hex ID '{hexId}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parses protocol type from XML element name.
        /// </summary>
        private static ProtocolType ParseProtocolType(string protocolName)
        {
            switch (protocolName.ToUpper())
            {
                case "SPI":
                    return ProtocolType.SPI;
                case "I2C":
                    return ProtocolType.I2C;
                case "MICROWIRE":
                    return ProtocolType.MicroWire;
                default:
                    return ProtocolType.None;
            }
        }

        /// <summary>
        /// Parses SPI command set from attribute value.
        /// </summary>
        private static SpiCommandSet ParseSpiCommandSet(string spicmd)
        {
            switch (spicmd.ToUpper())
            {
                case "25":
                    return SpiCommandSet.Series25;
                case "45":
                    return SpiCommandSet.Series45;
                case "95":
                    return SpiCommandSet.Series95;
                case "KB":
                    return SpiCommandSet.KB9012;
                default:
                    Logger.Warn($"Unknown SPI command set: {spicmd}, defaulting to Series25");
                    return SpiCommandSet.Series25;
            }
        }

        /// <summary>
        /// Parses manufacturer enum from manufacturer name string.
        /// </summary>
        private static ChipManufacturer ParseManufacturer(string manufacturerName)
        {
            // Try exact match first
            if (Enum.TryParse<ChipManufacturer>(manufacturerName.Replace(" ", "").Replace("-", "").Replace("_", ""), true, out var manufacturer))
            {
                return manufacturer;
            }

            // Try common mappings
            switch (manufacturerName.ToUpper())
            {
                case "WINBOND":
                case "W":
                    return ChipManufacturer.Winbond;
                case "MACRONIX":
                case "MXIC":
                case "MX":
                    return ChipManufacturer.Macronix;
                case "MICRON":
                case "MT":
                    return ChipManufacturer.Numonyx;
                case "SPANSION":
                case "S":
                    return ChipManufacturer.Spansion;
                case "SST":
                    return ChipManufacturer.SST;
                case "ATMEL":
                case "AT":
                    return ChipManufacturer.Atmel;
                case "GIGADEVICE":
                case "GD":
                    return ChipManufacturer.GigaDevice;
                case "EON":
                    return ChipManufacturer.EON;
                case "AMIC":
                    return ChipManufacturer.AMIC;
                case "PMC":
                    return ChipManufacturer.PMC;
                default:
                    Logger.Debug($"Unknown manufacturer: {manufacturerName}, using Other");
                    return ChipManufacturer.Other;
            }
        }

        /// <summary>
        /// Validates the chip database for integrity.
        /// </summary>
        /// <param name="chips">List of chips to validate</param>
        /// <returns>True if database is valid</returns>
        public static bool ValidateDatabase(List<ChipInfo> chips)
        {
            if (chips == null || chips.Count == 0)
            {
                Logger.Warn("Chip database is empty");
                return false;
            }

            var invalidCount = 0;

            foreach (var chip in chips)
            {
                if (string.IsNullOrEmpty(chip.Name))
                {
                    Logger.Warn("Chip with empty name detected");
                    invalidCount++;
                }

                if (chip.Size <= 0)
                {
                    Logger.Warn($"Chip '{chip.Name}' has invalid size: {chip.Size}");
                    invalidCount++;
                }
            }

            if (invalidCount > 0)
            {
                Logger.Warn($"Database validation found {invalidCount} invalid chips");
            }

            return invalidCount == 0;
        }
    }
}
