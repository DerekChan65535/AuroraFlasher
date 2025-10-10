using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AuroraFlasher.Models
{
    /// <summary>
    /// Application settings
    /// </summary>
    public class Settings : INotifyPropertyChanged
    {
        private HardwareType _selectedHardware;
        private ProtocolType _selectedProtocol;
        private SpiSpeed _spiSpeed;
        private int _i2cSpeed;
        private string _lastChipId;
        private Language _language;
        private ThemeMode _theme;
        private bool _autoDetectChip;
        private bool _verifyAfterWrite;
        private bool _autoSaveLog;
        private string _logDirectory;
        private string _lastOpenDirectory;
        private string _lastSaveDirectory;

        public event PropertyChangedEventHandler PropertyChanged;

        #region Hardware Settings

        /// <summary>
        /// Selected hardware type
        /// </summary>
        public HardwareType SelectedHardware
        {
            get => _selectedHardware;
            set => SetProperty(ref _selectedHardware, value);
        }

        /// <summary>
        /// Selected protocol type
        /// </summary>
        public ProtocolType SelectedProtocol
        {
            get => _selectedProtocol;
            set => SetProperty(ref _selectedProtocol, value);
        }

        /// <summary>
        /// SPI communication speed
        /// </summary>
        public SpiSpeed SpiSpeed
        {
            get => _spiSpeed;
            set => SetProperty(ref _spiSpeed, value);
        }

        /// <summary>
        /// I2C communication speed (kHz)
        /// </summary>
        public int I2CSpeed
        {
            get => _i2cSpeed;
            set => SetProperty(ref _i2cSpeed, value);
        }

        /// <summary>
        /// Last selected chip ID
        /// </summary>
        public string LastChipId
        {
            get => _lastChipId;
            set => SetProperty(ref _lastChipId, value);
        }

        #endregion

        #region UI Settings

        /// <summary>
        /// Application language
        /// </summary>
        public Language Language
        {
            get => _language;
            set => SetProperty(ref _language, value);
        }

        /// <summary>
        /// Theme mode
        /// </summary>
        public ThemeMode Theme
        {
            get => _theme;
            set => SetProperty(ref _theme, value);
        }

        #endregion

        #region Operation Settings

        /// <summary>
        /// Auto-detect chip on connect
        /// </summary>
        public bool AutoDetectChip
        {
            get => _autoDetectChip;
            set => SetProperty(ref _autoDetectChip, value);
        }

        /// <summary>
        /// Verify after write operation
        /// </summary>
        public bool VerifyAfterWrite
        {
            get => _verifyAfterWrite;
            set => SetProperty(ref _verifyAfterWrite, value);
        }

        /// <summary>
        /// Automatically save log to file
        /// </summary>
        public bool AutoSaveLog
        {
            get => _autoSaveLog;
            set => SetProperty(ref _autoSaveLog, value);
        }

        /// <summary>
        /// Directory for log files
        /// </summary>
        public string LogDirectory
        {
            get => _logDirectory;
            set => SetProperty(ref _logDirectory, value);
        }

        #endregion

        #region File Paths

        /// <summary>
        /// Last directory used for opening files
        /// </summary>
        public string LastOpenDirectory
        {
            get => _lastOpenDirectory;
            set => SetProperty(ref _lastOpenDirectory, value);
        }

        /// <summary>
        /// Last directory used for saving files
        /// </summary>
        public string LastSaveDirectory
        {
            get => _lastSaveDirectory;
            set => SetProperty(ref _lastSaveDirectory, value);
        }

        /// <summary>
        /// Recent files list
        /// </summary>
        public List<string> RecentFiles { get; set; }

        #endregion

        #region Advanced Settings

        /// <summary>
        /// Custom settings dictionary
        /// </summary>
        public Dictionary<string, object> CustomSettings { get; set; }

        #endregion

        public Settings()
        {
            // Default values
            _selectedHardware = HardwareType.UsbAsp;
            _selectedProtocol = ProtocolType.SPI;
            _spiSpeed = SpiSpeed.Normal;
            _i2cSpeed = 100;
            _language = Language.English;
            _theme = ThemeMode.System;
            _autoDetectChip = true;
            _verifyAfterWrite = true;
            _autoSaveLog = false;
            _logDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _lastOpenDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _lastSaveDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            
            RecentFiles = new List<string>();
            CustomSettings = new Dictionary<string, object>();
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

        /// <summary>
        /// Create default settings
        /// </summary>
        public static Settings CreateDefault()
        {
            return new Settings();
        }

        /// <summary>
        /// Reset to default values
        /// </summary>
        public void ResetToDefaults()
        {
            var defaults = CreateDefault();
            SelectedHardware = defaults.SelectedHardware;
            SelectedProtocol = defaults.SelectedProtocol;
            SpiSpeed = defaults.SpiSpeed;
            I2CSpeed = defaults.I2CSpeed;
            Language = defaults.Language;
            Theme = defaults.Theme;
            AutoDetectChip = defaults.AutoDetectChip;
            VerifyAfterWrite = defaults.VerifyAfterWrite;
            AutoSaveLog = defaults.AutoSaveLog;
        }
    }
}
