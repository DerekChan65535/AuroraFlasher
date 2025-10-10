using System;
using System.Linq;

namespace AuroraFlasher.Models
{
    /// <summary>
    /// Memory chip identification information
    /// </summary>
    public class MemoryId
    {
        /// <summary>
        /// JEDEC ID (3 bytes: Manufacturer, Memory Type, Capacity)
        /// </summary>
        public byte[] JedecId { get; set; }

        /// <summary>
        /// Manufacturer ID (first byte of JEDEC ID or separate read)
        /// </summary>
        public byte ManufacturerId { get; set; }

        /// <summary>
        /// Device ID (can be 1 or 2 bytes)
        /// </summary>
        public ushort DeviceId { get; set; }

        /// <summary>
        /// Electronic signature (for older chips)
        /// </summary>
        public byte ElectronicSignature { get; set; }

        /// <summary>
        /// Unique ID (if supported, up to 16 bytes)
        /// </summary>
        public byte[] UniqueId { get; set; }

        public MemoryId()
        {
            JedecId = new byte[3];
        }

        public MemoryId(byte[] jedecId)
        {
            if (jedecId == null || jedecId.Length < 3)
                throw new ArgumentException("JEDEC ID must be at least 3 bytes", nameof(jedecId));

            JedecId = jedecId;
            ManufacturerId = jedecId[0];
            DeviceId = (ushort)((jedecId[1] << 8) | jedecId[2]);
        }

        public MemoryId(byte manufacturerId, ushort deviceId)
        {
            ManufacturerId = manufacturerId;
            DeviceId = deviceId;
            JedecId = new byte[] { manufacturerId, (byte)(deviceId >> 8), (byte)(deviceId & 0xFF) };
        }

        /// <summary>
        /// Compare IDs for equality
        /// </summary>
        public bool Matches(MemoryId other)
        {
            if (other == null)
                return false;

            // Compare JEDEC ID if available
            if (JedecId != null && other.JedecId != null && JedecId.Length >= 3 && other.JedecId.Length >= 3)
            {
                return JedecId[0] == other.JedecId[0] &&
                       JedecId[1] == other.JedecId[1] &&
                       JedecId[2] == other.JedecId[2];
            }

            // Fallback to manufacturer and device ID
            return ManufacturerId == other.ManufacturerId && DeviceId == other.DeviceId;
        }

        /// <summary>
        /// Check if ID is blank/invalid
        /// </summary>
        public bool IsBlank()
        {
            return (JedecId == null || JedecId.All(b => b == 0x00 || b == 0xFF)) &&
                   (ManufacturerId == 0x00 || ManufacturerId == 0xFF);
        }

        public override string ToString()
        {
            if (JedecId != null && JedecId.Length >= 3)
                return $"{JedecId[0]:X2}{JedecId[1]:X2}{JedecId[2]:X2}h";
            
            return $"{ManufacturerId:X2}{DeviceId:X4}h";
        }

        public string ToDetailedString()
        {
            var result = $"Manufacturer: {ManufacturerId:X2}h, Device: {DeviceId:X4}h";
            
            if (JedecId != null && JedecId.Length >= 3)
                result += $", JEDEC: {BitConverter.ToString(JedecId).Replace("-", "")}";
            
            if (ElectronicSignature != 0)
                result += $", Signature: {ElectronicSignature:X2}h";
            
            if (UniqueId != null && UniqueId.Length > 0)
                result += $", UID: {BitConverter.ToString(UniqueId).Replace("-", "")}";

            return result;
        }
    }

    /// <summary>
    /// Hardware capabilities descriptor
    /// </summary>
    public class HardwareCapabilities
    {
        /// <summary>
        /// Supports SPI protocol
        /// </summary>
        public bool SupportsSpi { get; set; }

        /// <summary>
        /// Supports I2C protocol
        /// </summary>
        public bool SupportsI2C { get; set; }

        /// <summary>
        /// Supports MicroWire protocol
        /// </summary>
        public bool SupportsMicroWire { get; set; }

        /// <summary>
        /// Maximum SPI transfer size (bytes)
        /// </summary>
        public int MaxSpiTransferSize { get; set; }

        /// <summary>
        /// Maximum I2C transfer size (bytes)
        /// </summary>
        public int MaxI2CTransferSize { get; set; }

        /// <summary>
        /// Available SPI speeds
        /// </summary>
        public SpiSpeed[] AvailableSpiSpeeds { get; set; }

        /// <summary>
        /// Available I2C speeds (kHz)
        /// </summary>
        public int[] AvailableI2CSpeeds { get; set; }

        /// <summary>
        /// Supports GPIO control
        /// </summary>
        public bool SupportsGpio { get; set; }

        /// <summary>
        /// Number of GPIO pins
        /// </summary>
        public int GpioPinCount { get; set; }

        /// <summary>
        /// Has firmware version
        /// </summary>
        public bool HasFirmwareVersion { get; set; }

        /// <summary>
        /// Requires external power
        /// </summary>
        public bool RequiresExternalPower { get; set; }

        public HardwareCapabilities()
        {
            AvailableSpiSpeeds = new[] { SpiSpeed.Low, SpiSpeed.Normal };
            AvailableI2CSpeeds = new[] { 100, 400 };
        }

        public static HardwareCapabilities Default()
        {
            return new HardwareCapabilities
            {
                SupportsSpi = true,
                SupportsI2C = true,
                SupportsMicroWire = true,
                MaxSpiTransferSize = 4096,
                MaxI2CTransferSize = 256,
                AvailableSpiSpeeds = new[] { SpiSpeed.Low, SpiSpeed.Normal, SpiSpeed.High },
                AvailableI2CSpeeds = new[] { 100, 400 },
                SupportsGpio = false,
                GpioPinCount = 0,
                HasFirmwareVersion = false,
                RequiresExternalPower = false
            };
        }
    }
}
