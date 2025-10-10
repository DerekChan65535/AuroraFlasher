using System;

namespace AuroraFlasher.Models
{
    /// <summary>
    /// Type of hardware programmer
    /// </summary>
    public enum HardwareType
    {
        Auto = 0,       // Auto-detect
        None = 1,
        UsbAsp = 2,
        CH341 = 3,
        CH347 = 4,
        FT232H = 5,
        AvrIsp = 6,
        Arduino = 7
    }

    /// <summary>
    /// Communication protocol type
    /// </summary>
    public enum ProtocolType
    {
        None = 0,
        SPI = 1,
        I2C = 2,
        MicroWire = 3
    }

    /// <summary>
    /// SPI command set variant
    /// </summary>
    public enum SpiCommandSet
    {
        Standard = 0,   // Standard 25-series (W25, MX25, etc.) - alias for Series25
        Series25 = 0,   // Most common (W25, MX25, etc.)
        Series45 = 1,   // Atmel DataFlash (AT45)
        Series95 = 2,   // Small EEPROM (AT95, 95xxx)
        SST_AAI = 3,    // SST with AAI programming
        KB9012 = 4      // Keyboard controller (multi-protocol)
    }

    /// <summary>
    /// I2C address format type
    /// </summary>
    public enum I2CAddressType
    {
        A0 = 0,         // 7-bit address
        A1 = 1,         // 8-bit address (1 bit from device)
        A2 = 2,         // 9-bit address (2 bits from device)
        A3 = 3,         // 10-bit address (3 bits from device)
        A8 = 4,         // 1 byte address
        A16 = 5,        // 2 byte address (big endian)
        A16LE = 6       // 2 byte address (little endian)
    }

    /// <summary>
    /// MicroWire address bit length
    /// </summary>
    public enum MicroWireAddressBits
    {
        Bits6 = 6,
        Bits7 = 7,
        Bits8 = 8,
        Bits9 = 9,
        Bits10 = 10,
        Bits11 = 11,
        Bits12 = 12
    }

    /// <summary>
    /// Chip manufacturer
    /// </summary>
    public enum ChipManufacturer
    {
        Unknown = 0,
        Winbond = 1,
        Macronix = 2,
        GigaDevice = 3,
        Spansion = 4,
        SST = 5,
        Microchip = 6,
        Atmel = 7,
        ISSI = 8,
        EON = 9,
        PMC = 10,
        Numonyx = 11,
        STMicro = 12,
        Catalyst = 13,
        AMIC = 14,
        Fudan = 15,
        Bright = 16,
        XMC = 17,
        Puya = 18,
        ESMT = 19,
        Other = 99
    }

    /// <summary>
    /// SPI transfer speed
    /// </summary>
    public enum SpiSpeed
    {
        Low = 0,        // ~100 kHz
        Normal = 1,     // ~750 kHz
        High = 2,       // ~1.5 MHz
        Maximum = 3     // Device maximum
    }

    /// <summary>
    /// Operation status
    /// </summary>
    public enum OperationStatus
    {
        Idle = 0,
        InProgress = 1,
        Completed = 2,
        Failed = 3,
        Cancelled = 4
    }

    /// <summary>
    /// Memory operation type
    /// </summary>
    public enum MemoryOperation
    {
        None = 0,
        Read = 1,
        Write = 2,
        Verify = 3,
        Erase = 4,
        BlankCheck = 5,
        ReadId = 6
    }

    /// <summary>
    /// Erase granularity
    /// </summary>
    public enum EraseType
    {
        Chip = 0,       // Full chip erase
        Sector = 1,     // 4KB sector erase
        Block32 = 2,    // 32KB block erase
        Block64 = 3,    // 64KB block erase
        Page = 4        // Page erase (for some chips)
    }

    /// <summary>
    /// Log message level
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    /// <summary>
    /// Application language
    /// </summary>
    public enum Language
    {
        English = 0,
        Russian = 1,
        Chinese = 2,
        Spanish = 3,
        German = 4
    }

    /// <summary>
    /// Theme mode
    /// </summary>
    public enum ThemeMode
    {
        Light = 0,
        Dark = 1,
        System = 2
    }
}
