using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AuroraFlasher.Models
{
    /// <summary>
    /// Complete chip information from database
    /// </summary>
    public class ChipInfo : INotifyPropertyChanged
    {
        private string _id;
        private string _name;
        private ChipManufacturer _manufacturer;
        private ProtocolType _protocolType;
        private SpiCommandSet _spiCommandSet;
        private int _size;
        private int _pageSize;
        private int _sectorSize;
        private int _blockSize;
        private bool _supports4ByteAddress;
        private bool _supportsDualSpi;
        private bool _supportsQuadSpi;
        private byte _manufacturerId;
        private ushort _deviceId;
        private int _voltage;
        private I2CAddressType _i2cAddressType;
        private byte _i2cDeviceAddress;
        private int _microWireAddressBits;
        private int _microWireDataBits;
        private string _description;
        private bool _isSelected;

        public event PropertyChangedEventHandler PropertyChanged;

        #region Basic Information

        /// <summary>
        /// Unique chip identifier
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// Chip name (e.g., "W25Q32")
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// Manufacturer
        /// </summary>
        public ChipManufacturer Manufacturer
        {
            get => _manufacturer;
            set => SetProperty(ref _manufacturer, value);
        }

        /// <summary>
        /// Protocol type
        /// </summary>
        public ProtocolType ProtocolType
        {
            get => _protocolType;
            set => SetProperty(ref _protocolType, value);
        }

        /// <summary>
        /// SPI command set variant
        /// </summary>
        public SpiCommandSet SpiCommandSet
        {
            get => _spiCommandSet;
            set => SetProperty(ref _spiCommandSet, value);
        }

        /// <summary>
        /// Description
        /// </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        #endregion

        #region Memory Organization

        /// <summary>
        /// Total size in bytes
        /// </summary>
        public int Size
        {
            get => _size;
            set => SetProperty(ref _size, value);
        }

        /// <summary>
        /// Page size in bytes (for programming)
        /// </summary>
        public int PageSize
        {
            get => _pageSize;
            set => SetProperty(ref _pageSize, value);
        }

        /// <summary>
        /// Sector size in bytes (typically 4KB)
        /// </summary>
        public int SectorSize
        {
            get => _sectorSize;
            set => SetProperty(ref _sectorSize, value);
        }

        /// <summary>
        /// Block size in bytes (typically 64KB)
        /// </summary>
        public int BlockSize
        {
            get => _blockSize;
            set => SetProperty(ref _blockSize, value);
        }

        #endregion

        #region Identification

        /// <summary>
        /// Manufacturer ID byte
        /// </summary>
        public byte ManufacturerId
        {
            get => _manufacturerId;
            set => SetProperty(ref _manufacturerId, value);
        }

        /// <summary>
        /// Device ID (2 bytes)
        /// </summary>
        public ushort DeviceId
        {
            get => _deviceId;
            set => SetProperty(ref _deviceId, value);
        }

        /// <summary>
        /// Memory ID for comparison
        /// </summary>
        public MemoryId MemoryId => new MemoryId(ManufacturerId, DeviceId);

        #endregion

        #region SPI-Specific Properties

        /// <summary>
        /// Supports 4-byte addressing (for chips > 16MB)
        /// </summary>
        public bool Supports4ByteAddress
        {
            get => _supports4ByteAddress;
            set => SetProperty(ref _supports4ByteAddress, value);
        }

        /// <summary>
        /// Supports Dual SPI mode
        /// </summary>
        public bool SupportsDualSpi
        {
            get => _supportsDualSpi;
            set => SetProperty(ref _supportsDualSpi, value);
        }

        /// <summary>
        /// Supports Quad SPI mode
        /// </summary>
        public bool SupportsQuadSpi
        {
            get => _supportsQuadSpi;
            set => SetProperty(ref _supportsQuadSpi, value);
        }

        #endregion

        #region I2C-Specific Properties

        /// <summary>
        /// I2C address type
        /// </summary>
        public I2CAddressType I2CAddressType
        {
            get => _i2cAddressType;
            set => SetProperty(ref _i2cAddressType, value);
        }

        /// <summary>
        /// I2C device address (7-bit)
        /// </summary>
        public byte I2CDeviceAddress
        {
            get => _i2cDeviceAddress;
            set => SetProperty(ref _i2cDeviceAddress, value);
        }

        #endregion

        #region MicroWire-Specific Properties

        /// <summary>
        /// Number of address bits (6-12)
        /// </summary>
        public int MicroWireAddressBits
        {
            get => _microWireAddressBits;
            set => SetProperty(ref _microWireAddressBits, value);
        }

        /// <summary>
        /// Number of data bits per word (8 or 16)
        /// </summary>
        public int MicroWireDataBits
        {
            get => _microWireDataBits;
            set => SetProperty(ref _microWireDataBits, value);
        }

        #endregion

        #region Other Properties

        /// <summary>
        /// Operating voltage in millivolts (e.g., 3300 for 3.3V)
        /// </summary>
        public int Voltage
        {
            get => _voltage;
            set => SetProperty(ref _voltage, value);
        }

        /// <summary>
        /// Is this chip selected in UI
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        #endregion

        #region Calculated Properties

        /// <summary>
        /// Size in kilobytes
        /// </summary>
        public int SizeKB => Size / 1024;

        /// <summary>
        /// Size in megabytes
        /// </summary>
        public double SizeMB => Size / 1024.0 / 1024.0;

        /// <summary>
        /// Number of pages
        /// </summary>
        public int PageCount => PageSize > 0 ? Size / PageSize : 0;

        /// <summary>
        /// Number of sectors
        /// </summary>
        public int SectorCount => SectorSize > 0 ? Size / SectorSize : 0;

        /// <summary>
        /// Number of blocks
        /// </summary>
        public int BlockCount => BlockSize > 0 ? Size / BlockSize : 0;

        /// <summary>
        /// Display name for UI
        /// </summary>
        public string DisplayName => $"{Name} ({SizeKB}KB)";

        /// <summary>
        /// Full description for UI
        /// </summary>
        public string FullDescription =>
            $"{Name} - {Manufacturer} - {SizeKB}KB - {ProtocolType} - ID: {ManufacturerId:X2}{DeviceId:X4}h";

        #endregion

        public ChipInfo()
        {
            _id = Guid.NewGuid().ToString();
            _name = "Unknown";
            _manufacturer = ChipManufacturer.Unknown;
            _protocolType = ProtocolType.SPI;
            _spiCommandSet = SpiCommandSet.Series25;
            _description = string.Empty;
            _voltage = 3300;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public override string ToString()
        {
            return DisplayName;
        }

        public ChipInfo Clone()
        {
            return (ChipInfo)MemberwiseClone();
        }
    }
}
