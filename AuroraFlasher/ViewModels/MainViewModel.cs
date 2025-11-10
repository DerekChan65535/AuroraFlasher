using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AuroraFlasher.Commands;
using AuroraFlasher.Interfaces;
using AuroraFlasher.Logging;
using AuroraFlasher.Models;
using AuroraFlasher.Services;

namespace AuroraFlasher.ViewModels
{
    /// <summary>
    /// Main window ViewModel - MVP version for CH341 + SPI
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly ProgrammerService _service;
        private readonly int _retryCount;
        private CancellationTokenSource _cancellationTokenSource;

        // Known VID for WCH (CH341 manufacturer)
        private const string CH341_VENDOR_ID = "1A86";

        #region Properties

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                Logger.Debug($"[MainViewModel] IsConnected changing from {_isConnected} to {value}");
                if (SetProperty(ref _isConnected, value))
                {
                    Logger.Debug($"[MainViewModel] IsConnected changed. CanRead={CanRead}, CanClearFlash={CanClearFlash}, CanFlash={CanFlash}");
                    OnPropertyChanged(nameof(CanRead));
                    OnPropertyChanged(nameof(CanClearFlash));
                    OnPropertyChanged(nameof(CanFlash));
                    OnPropertyChanged(nameof(CanToggleWriteProtection));
                    OnPropertyChanged(nameof(ConnectionStatus));
                    
                    // Directly notify affected commands on UI thread (best practice)
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        _readMemoryCommand?.RaiseCanExecuteChanged();
                        _clearFlashCommand?.RaiseCanExecuteChanged();
                        _flashCommand?.RaiseCanExecuteChanged();
                        _flashWithVerifyCommand?.RaiseCanExecuteChanged();
                        _toggleWriteProtectionCommand?.RaiseCanExecuteChanged();
                        Logger.Debug($"[MainViewModel] Commands notified directly");
                    });
                }
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                Logger.Debug($"[MainViewModel] IsBusy changing from {_isBusy} to {value}");
                if (SetProperty(ref _isBusy, value))
                {
                    Logger.Debug($"[MainViewModel] IsBusy changed. CanRead={CanRead}, CanClearFlash={CanClearFlash}, CanFlash={CanFlash}");
                    OnPropertyChanged(nameof(CanRead));
                    OnPropertyChanged(nameof(CanClearFlash));
                    OnPropertyChanged(nameof(CanFlash));
                    OnPropertyChanged(nameof(CanToggleWriteProtection));
                    
                    // Directly notify affected commands on UI thread (best practice)
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        _readMemoryCommand?.RaiseCanExecuteChanged();
                        _clearFlashCommand?.RaiseCanExecuteChanged();
                        _flashCommand?.RaiseCanExecuteChanged();
                        _flashWithVerifyCommand?.RaiseCanExecuteChanged();
                        _toggleWriteProtectionCommand?.RaiseCanExecuteChanged();
                        Logger.Debug($"[MainViewModel] Commands notified directly");
                    });
                }
            }
        }

        private string _deviceInfo;
        public string DeviceInfo
        {
            get => _deviceInfo;
            set => SetProperty(ref _deviceInfo, value);
        }

        private string _chipInfo;
        public string ChipInfo
        {
            get => _chipInfo;
            set
            {
                Logger.Debug($"[MainViewModel] ChipInfo changing from '{_chipInfo}' to '{value}'");
                if (SetProperty(ref _chipInfo, value))
                {
                    Logger.Debug($"[MainViewModel] ChipInfo changed. CanRead={CanRead}, CanClearFlash={CanClearFlash}, CanFlash={CanFlash}");
                    OnPropertyChanged(nameof(CanRead));
                    OnPropertyChanged(nameof(CanClearFlash));
                    OnPropertyChanged(nameof(CanFlash));
                    OnPropertyChanged(nameof(CanToggleWriteProtection));
                    
                    // Directly notify affected commands on UI thread (best practice)
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        _readMemoryCommand?.RaiseCanExecuteChanged();
                        _clearFlashCommand?.RaiseCanExecuteChanged();
                        _flashCommand?.RaiseCanExecuteChanged();
                        _flashWithVerifyCommand?.RaiseCanExecuteChanged();
                        _toggleWriteProtectionCommand?.RaiseCanExecuteChanged();
                        Logger.Debug($"[MainViewModel] Commands notified directly");
                    });
                }
            }
        }

        private string _logOutput;
        public string LogOutput
        {
            get => _logOutput;
            set => SetProperty(ref _logOutput, value);
        }

        private ObservableCollection<HexLineData> _hexLines;
        public ObservableCollection<HexLineData> HexLines
        {
            get => _hexLines;
            set => SetProperty(ref _hexLines, value);
        }

        private ChipInfo _detectedChip;
        /// <summary>
        /// Gets or sets the detected chip information. This is set automatically when a chip is successfully detected.
        /// Used to determine the chip size for read operations.
        /// </summary>
        public ChipInfo DetectedChip
        {
            get => _detectedChip;
            set => SetProperty(ref _detectedChip, value);
        }

        private bool _isWriteProtected;
        /// <summary>
        /// Gets or sets whether the flash chip is write protected
        /// </summary>
        public bool IsWriteProtected
        {
            get => _isWriteProtected;
            set
            {
                if (SetProperty(ref _isWriteProtected, value))
                {
                    OnPropertyChanged(nameof(ToggleWriteProtectionCommandText));
                    OnPropertyChanged(nameof(ToggleWriteProtectionToolTip));
                    
                    // Directly notify command on UI thread (best practice)
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        _toggleWriteProtectionCommand?.RaiseCanExecuteChanged();
                    });
                }
            }
        }

        /// <summary>
        /// Gets the text for the toggle write protection button based on current state
        /// </summary>
        public string ToggleWriteProtectionCommandText => IsWriteProtected ? "Unlock Flash" : "Lock Flash";

        /// <summary>
        /// Gets the tooltip for the toggle write protection button based on current state
        /// </summary>
        public string ToggleWriteProtectionToolTip => IsWriteProtected 
            ? "Disable write protection on the flash chip" 
            : "Enable write protection on the flash chip";

        public ObservableCollection<IHardware> AvailableDevices { get; }

        private IHardware _selectedDevice;
        public IHardware SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (SetProperty(ref _selectedDevice, value))
                {
                    OnPropertyChanged(nameof(ConnectionStatus));
                }
            }
        }

        private bool _isOperationInProgress;
        public bool IsOperationInProgress
        {
            get => _isOperationInProgress;
            set => SetProperty(ref _isOperationInProgress, value);
        }

        private double _progressPercentage;
        public double ProgressPercentage
        {
            get => _progressPercentage;
            set => SetProperty(ref _progressPercentage, value);
        }

        private string _progressText;
        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        /// <summary>
        /// Gets the connection status with device name
        /// </summary>
        public string ConnectionStatus
        {
            get
            {
                if (IsConnected && SelectedDevice != null)
                {
                    return $"Connected: {SelectedDevice.Name}";
                }
                return "Not Connected";
            }
        }

        #endregion

        #region Can Execute Properties

        public bool CanRead => IsConnected && !IsBusy && !string.IsNullOrEmpty(ChipInfo) && !ChipInfo.Contains("No chip detected");
        public bool CanClearFlash => IsConnected && !IsBusy && !string.IsNullOrEmpty(ChipInfo) && !ChipInfo.Contains("No chip detected");
        public bool CanFlash => IsConnected && !IsBusy && !string.IsNullOrEmpty(ChipInfo) && !ChipInfo.Contains("No chip detected");
        public bool CanToggleWriteProtection => IsConnected && !IsBusy && !string.IsNullOrEmpty(ChipInfo) && !ChipInfo.Contains("No chip detected");

        #endregion

        #region Commands

        public ICommand ReadMemoryCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand ClearFlashCommand { get; }
        public ICommand FlashCommand { get; }
        public ICommand FlashWithVerifyCommand { get; }
        public ICommand ToggleWriteProtectionCommand { get; }
        
        // Store RelayCommand references for direct notification
        private readonly RelayCommand _readMemoryCommand;
        private readonly RelayCommand _clearFlashCommand;
        private readonly RelayCommand _flashCommand;
        private readonly RelayCommand _flashWithVerifyCommand;
        private readonly RelayCommand _toggleWriteProtectionCommand;

        #endregion

        public MainViewModel()
        {
            Logger.Debug("[MainViewModel] Constructor started");
            _service = new ProgrammerService();
            var retrySetting = ConfigurationManager.AppSettings["RetryCount"];
            if (!int.TryParse(retrySetting, out var retryCount) || retryCount < 0)
            {
                _retryCount = 0;
                if (!string.IsNullOrWhiteSpace(retrySetting))
                {
                    Logger.Warn($"[MainViewModel] Invalid RetryCount '{retrySetting}', defaulting to 0 (no retries)");
                }
                else
                {
                    Logger.Debug("[MainViewModel] RetryCount not configured, defaulting to 0 (no retries)");
                }
            }
            else
            {
                _retryCount = retryCount;
                Logger.Debug($"[MainViewModel] RetryCount configured: {_retryCount}");
            }
            AvailableDevices = new ObservableCollection<IHardware>();

            // Initialize properties
            Logger.Debug("[MainViewModel] Initializing properties...");
            StatusMessage = "Ready";
            DeviceInfo = "No device connected";
            ChipInfo = "No chip detected";
            LogOutput = string.Empty;
            HexLines = new ObservableCollection<HexLineData>();
            Logger.Debug($"[MainViewModel] Properties initialized. IsConnected={IsConnected}, IsBusy={IsBusy}, ChipInfo='{ChipInfo}'");

            // Initialize commands with stored references for direct notification
            Logger.Debug("[MainViewModel] Initializing commands...");
            _readMemoryCommand = new RelayCommand(async () => await ReadMemoryAsync(), () => CanRead, "ReadMemoryCommand");
            _clearFlashCommand = new RelayCommand(async () => await ClearFlashAsync(), () => CanClearFlash, "ClearFlashCommand");
            _flashCommand = new RelayCommand(async () => await FlashAsync(), () => CanFlash, "FlashCommand");
            _flashWithVerifyCommand = new RelayCommand(async () => await FlashWithVerifyAsync(), () => CanFlash, "FlashWithVerifyCommand");
            _toggleWriteProtectionCommand = new RelayCommand(async () => await ToggleWriteProtectionAsync(), () => CanToggleWriteProtection, "ToggleWriteProtectionCommand");
            
            ReadMemoryCommand = _readMemoryCommand;
            ClearLogCommand = new RelayCommand(() => LogOutput = string.Empty, null, "ClearLogCommand");
            ClearFlashCommand = _clearFlashCommand;
            FlashCommand = _flashCommand;
            FlashWithVerifyCommand = _flashWithVerifyCommand;
            ToggleWriteProtectionCommand = _toggleWriteProtectionCommand;
            Logger.Debug("[MainViewModel] Commands initialized");

            // Auto-enumerate on startup (will auto-connect and auto-detect if device present)
            Logger.Debug("[MainViewModel] Starting auto-enumerate...");
            Task.Run(async () => await EnumerateDevicesAsync());
            
            Logger.Debug("[MainViewModel] Constructor completed");
        }

        #region Methods

        private async Task EnumerateDevicesAsync(bool checkForAutoConnect = true)
        {
            try
            {
                AppendLog("Enumerating hardware...");
                var result = await _service.EnumerateHardwareAsync();

                if (result.Success && result.Data != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AvailableDevices.Clear();
                        foreach (var hardware in result.Data)
                        {
                            AvailableDevices.Add(hardware);
                        }

                        if (AvailableDevices.Count > 0)
                        {
                            SelectedDevice = AvailableDevices[0];
                            AppendLog($"Found {AvailableDevices.Count} hardware type(s)");
                        }
                        else
                        {
                            AppendLog("No hardware found");
                        }
                    });

                    // Check if CH341 device is already connected at startup
                    // Only do this check when explicitly requested (e.g., app startup or USB plug event)
                    // to avoid infinite loop with AutoConnectAsync
                    if (checkForAutoConnect)
                    {
                        await CheckForAlreadyConnectedDeviceAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error enumerating hardware: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if a CH341 device is already connected when the app starts
        /// </summary>
        private async Task CheckForAlreadyConnectedDeviceAsync()
        {
            // Only auto-connect if we have a CH341 device available and we're not already connected
            if (SelectedDevice?.Type == HardwareType.CH341 && !IsConnected && !IsBusy)
            {
                // Wait a moment to let the UI initialize
                await Task.Delay(500);

                // First check if there are actually any physical CH341 devices available
                try
                {
                    var devices = await SelectedDevice.EnumerateDevicesAsync();
                    if (devices != null && devices.Length > 0)
                    {
                        AppendLog("CH341 device detected at startup, attempting auto-connect...");
                        await AutoConnectAsync();
                    }
                    else
                    {
                        AppendLog("No CH341 devices found");
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"Error checking for CH341 devices: {ex.Message}");
                }
            }
        }

        private async Task ConnectAsync()
        {
            if (SelectedDevice == null)
            {
                StatusMessage = "No device selected";
                return;
            }

            IsBusy = true;
            StatusMessage = "Connecting...";

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();

                // Scan for devices
                AppendLog($"Scanning for {SelectedDevice.Name} devices...");
                var devices = await SelectedDevice.EnumerateDevicesAsync(_cancellationTokenSource.Token);

                if (devices == null || devices.Length == 0)
                {
                    StatusMessage = "No devices found";
                    AppendLog("No devices found");
                    return;
                }

                AppendLog($"Found {devices.Length} device(s)");

                // Connect to first device
                var result = await _service.ConnectAsync(SelectedDevice, devices[0], _cancellationTokenSource.Token);

                if (result.Success)
                {
                    IsConnected = true;
                    StatusMessage = $"Connected to {SelectedDevice.Name}";
                    DeviceInfo = $"{SelectedDevice.Name}\nType: {SelectedDevice.Type}\nStatus: Connected";
                    AppendLog($"Connected successfully");

                    // Automatically detect chip after connection
                    await DetectChipAsync();
                }
                else
                {
                    StatusMessage = $"Connection failed: {result.Message}";
                    AppendLog($"Connection failed: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                AppendLog($"Error connecting: {ex.Message}");
                Logger.Error(ex, "Connection error in UI");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DisconnectAsync()
        {
            IsBusy = true;
            StatusMessage = "Disconnecting...";

            try
            {
                _cancellationTokenSource?.Cancel();
                var result = await _service.DisconnectAsync();

                IsConnected = false;
                StatusMessage = "Disconnected";
                DeviceInfo = "No device connected";
                ChipInfo = "No chip detected";
                DetectedChip = null;
                HexLines.Clear();
                AppendLog("Disconnected successfully");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                AppendLog($"Error disconnecting: {ex.Message}");
                Logger.Error(ex, "Disconnection error in UI");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DetectChipAsync()
        {
            IsBusy = true;
            StatusMessage = "Detecting chip...";

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                
                // Use candidate detection API (smart selection handles multiple candidates automatically)
                var result = await _service.DetectChipCandidatesAsync(ProtocolType.SPI, _cancellationTokenSource.Token);

                if (result.Success && result.Data != null && result.Data.Count > 0)
                {
                    // Service already selected the best chip using smart strategy
                    var chip = result.Data[0];
                    DetectedChip = chip;
                    AppendLog($"Chip detected: {chip.Name} ({chip.SizeKB}KB)");

                    // Display chip info
                    StatusMessage = $"Chip detected: {chip.Name}";
                    
                    var chipInfoBuilder = new StringBuilder();
                    chipInfoBuilder.AppendLine($"Chip: {chip.Name}");
                    chipInfoBuilder.AppendLine($"Manufacturer: {chip.Manufacturer}");
                    chipInfoBuilder.AppendLine($"Size: {chip.SizeKB}KB ({chip.SizeMB:F2}MB)");
                    chipInfoBuilder.AppendLine($"Page Size: {chip.PageSize} bytes");
                    chipInfoBuilder.AppendLine($"Sector Size: {chip.SectorSize} bytes");
                    chipInfoBuilder.AppendLine($"Block Size: {chip.BlockSize} bytes");
                    chipInfoBuilder.AppendLine($"Voltage: {chip.Voltage / 1000.0:F1}V");
                    chipInfoBuilder.AppendLine($"Manufacturer ID: 0x{chip.ManufacturerId:X2}");
                    chipInfoBuilder.AppendLine($"Device ID: 0x{chip.DeviceId:X4}");

                    // Check write protection status
                    var wpResult = await _service.CheckWriteProtectionAsync(_cancellationTokenSource.Token);
                    if (wpResult.Success)
                    {
                        IsWriteProtected = wpResult.Data;
                        chipInfoBuilder.AppendLine($"Write Protection: {(wpResult.Data ? "ON" : "OFF")}");
                    }

                    ChipInfo = chipInfoBuilder.ToString();
                }
                else
                {
                    StatusMessage = $"Detection failed: {result.Message}";
                    ChipInfo = "Detection failed";
                    DetectedChip = null;
                    AppendLog($"Detection failed: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                ChipInfo = "Detection error";
                DetectedChip = null;
                AppendLog($"Error detecting chip: {ex.Message}");
                Logger.Error(ex, "Detection error in UI");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ReadMemoryAsync()
        {
            IsBusy = true;
            IsOperationInProgress = true;
            ProgressPercentage = 0;
            ProgressText = "Starting...";
            StatusMessage = "Reading memory...";

            try
            {
                // Validate chip is detected
                if (_detectedChip == null)
                {
                    StatusMessage = "No chip detected";
                    AppendLog("Cannot read memory: No chip detected. Please detect chip first.");
                    return;
                }

                uint address = 0;
                int length = _detectedChip.Size;

                _cancellationTokenSource = new CancellationTokenSource();
                AppendLog($"Reading {length} bytes from 0x000000...");

                // Create progress reporter
                var progress = new Progress<ProgressInfo>(progressInfo =>
                {
                    ProgressPercentage = progressInfo.Percentage;
                    ProgressText = $"{progressInfo.Percentage:F1}% - {progressInfo.BytesProcessed:N0} / {progressInfo.TotalBytes:N0} bytes - {progressInfo.Speed / 1024:F1} KB/s";
                    StatusMessage = $"Reading... {progressInfo.Percentage:F0}%";
                });

                var result = await _service.ReadMemoryAsync(
                    address,
                    length,
                    progress,
                    _retryCount,
                    cancellationToken: _cancellationTokenSource.Token);

                if (result.Success && result.Data != null)
                {
                    StatusMessage = $"Read {result.Data.Length} bytes successfully";
                    UpdateHexDump(result.Data, address);
                    AppendLog($"Read {result.Data.Length} bytes from 0x000000");
                    ProgressText = $"Complete - {result.Data.Length:N0} bytes";
                }
                else
                {
                    StatusMessage = $"Read failed: {result.Message}";
                    AppendLog($"Read failed: {result.Message}");
                    ProgressText = "Failed";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                AppendLog($"Error reading memory: {ex.Message}");
                Logger.Error(ex, "Read error in UI");
                ProgressText = "Error";
            }
            finally
            {
                IsBusy = false;
                // Keep progress bar visible for 2 seconds so user can see final status
                await Task.Delay(2000);
                IsOperationInProgress = false;
                ProgressPercentage = 0;
            }
        }

        /// <summary>
        /// Update hex dump display with virtualized line-by-line rendering.
        /// This method populates HexLines collection for ListView virtualization.
        /// </summary>
        private void UpdateHexDump(byte[] data, uint startAddress)
        {
            const int bytesPerLine = 16;
            
            // Pre-calculate line count to avoid dynamic resizing
            var lineCount = (data.Length + bytesPerLine - 1) / bytesPerLine;
            var lines = new System.Collections.Generic.List<HexLineData>(lineCount);

            // Process data off UI thread to avoid freezing
            Task.Run(() =>
            {
                for (var i = 0; i < data.Length; i += bytesPerLine)
                {
                    // Address
                    var address = $"{startAddress + i:X4}:";

                    // Hex bytes - create list of hex strings
                    var lineLength = Math.Min(bytesPerLine, data.Length - i);
                    var byteValues = new System.Collections.Generic.List<string>(lineLength);
                    
                    for (var j = 0; j < lineLength; j++)
                    {
                        byteValues.Add($"{data[i + j]:X2}");
                    }

                    lines.Add(new HexLineData(address, byteValues));
                }

                // Update UI on dispatcher thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    HexLines.Clear();
                    
                    // Batch add all lines to collection (more efficient than adding one by one)
                    foreach (var line in lines)
                    {
                        HexLines.Add(line);
                    }
                });
            });
        }

        private void AppendLog(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogOutput += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            });
        }

        private async Task ClearFlashAsync()
        {
            // Show confirmation dialog with warning
            var result = MessageBox.Show(
                "This will erase all data on the chip. Continue?",
                "Clear Flash Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            IsOperationInProgress = true;
            ProgressPercentage = 0;
            ProgressText = "Starting...";
            StatusMessage = "Clearing flash...";

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                AppendLog("Starting clear flash operation...");

                // Create progress reporter
                var progress = new Progress<ProgressInfo>(progressInfo =>
                {
                    ProgressPercentage = progressInfo.Percentage;
                    ProgressText = $"{progressInfo.Percentage:F1}% - {progressInfo.Status}";
                    StatusMessage = $"Clearing... {progressInfo.Percentage:F0}%";
                });

                var clearResult = await _service.ClearFlashWholeRomAsync(progress, _cancellationTokenSource.Token);

                if (clearResult.Success)
                {
                    StatusMessage = "Clear flash completed successfully";
                    ProgressText = "Complete";
                    AppendLog($"Clear flash completed: {clearResult.Message}");
                    
                    // Show completion message
                    MessageBox.Show(
                        clearResult.Message,
                        "Clear Flash Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = $"Clear flash failed: {clearResult.Message}";
                    ProgressText = "Failed";
                    AppendLog($"Clear flash failed: {clearResult.Message}");
                    
                    // Show error message
                    MessageBox.Show(
                        $"Clear flash failed: {clearResult.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                ProgressText = "Error";
                AppendLog($"Error during clear flash: {ex.Message}");
                Logger.Error(ex, "Clear flash error in UI");
                
                MessageBox.Show(
                    $"Clear flash error: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                // Keep progress bar visible for 2 seconds so user can see final status
                await Task.Delay(2000);
                IsOperationInProgress = false;
                ProgressPercentage = 0;
            }
        }

        private async Task FlashAsync()
        {
            // Show file picker
            var filePath = BrowseFlashFile();
            if (string.IsNullOrEmpty(filePath))
                return;

            // Show confirmation dialog
            var result = MessageBox.Show(
                $"This will write the binary file to the chip starting at address 0x000000.\n\nFile: {filePath}\n\nContinue?",
                "Flash ROM Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            IsOperationInProgress = true;
            ProgressPercentage = 0;
            ProgressText = "Starting...";
            StatusMessage = "Flashing ROM...";

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                AppendLog($"Starting flash operation for file: {filePath}");

                // Create progress reporter
                var progress = new Progress<ProgressInfo>(progressInfo =>
                {
                    ProgressPercentage = progressInfo.Percentage;
                    ProgressText = $"{progressInfo.Percentage:F1}% - {progressInfo.Status}";
                    StatusMessage = $"Flashing... {progressInfo.Percentage:F0}%";
                });

                var flashResult = await _service.FlashAsync(
                    filePath,
                    progress,
                    retryCount: _retryCount,
                    cancellationToken: _cancellationTokenSource.Token);

                if (flashResult.Success)
                {
                    StatusMessage = "Flash ROM completed successfully";
                    ProgressText = "Complete";
                    AppendLog($"Flash ROM completed: {flashResult.Message}");
                    
                    // Show completion message
                    MessageBox.Show(
                        flashResult.Message,
                        "Flash ROM Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = $"Flash ROM failed: {flashResult.Message}";
                    ProgressText = "Failed";
                    AppendLog($"Flash ROM failed: {flashResult.Message}");
                    
                    // Show error message
                    MessageBox.Show(
                        $"Flash ROM failed: {flashResult.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                ProgressText = "Error";
                AppendLog($"Error during flash ROM: {ex.Message}");
                Logger.Error(ex, "Flash ROM error in UI");
                
                MessageBox.Show(
                    $"Flash ROM error: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                // Keep progress bar visible for 2 seconds so user can see final status
                await Task.Delay(2000);
                IsOperationInProgress = false;
                ProgressPercentage = 0;
            }
        }

        private async Task FlashWithVerifyAsync()
        {
            // Show file picker
            var filePath = BrowseFlashFile();
            if (string.IsNullOrEmpty(filePath))
                return;

            // Show confirmation dialog
            var result = MessageBox.Show(
                $"This will write the binary file to the chip with immediate verification.\n\nFile: {filePath}\n\nThis will take longer but ensures data integrity.\n\nContinue?",
                "Flash ROM with Verify Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            IsOperationInProgress = true;
            ProgressPercentage = 0;
            ProgressText = "Starting...";
            StatusMessage = "Flashing ROM with verify...";

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                AppendLog($"Starting flash with verify operation for file: {filePath}");

                // Create progress reporter
                var progress = new Progress<ProgressInfo>(progressInfo =>
                {
                    ProgressPercentage = progressInfo.Percentage;
                    ProgressText = $"{progressInfo.Percentage:F1}% - {progressInfo.Status}";
                    StatusMessage = $"Flashing with verify... {progressInfo.Percentage:F0}%";
                });

                var flashResult = await _service.FlashWithVerifyAsync(
                    filePath,
                    progress,
                    retryCount: _retryCount,
                    cancellationToken: _cancellationTokenSource.Token);

                if (flashResult.Success)
                {
                    StatusMessage = "Flash ROM with verify completed successfully";
                    ProgressText = "Complete";
                    AppendLog($"Flash ROM with verify completed: {flashResult.Message}");
                    
                    // Show completion message
                    MessageBox.Show(
                        flashResult.Message,
                        "Flash ROM with Verify Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = $"Flash ROM with verify failed: {flashResult.Message}";
                    ProgressText = "Failed";
                    AppendLog($"Flash ROM with verify failed: {flashResult.Message}");
                    
                    // Show error message
                    MessageBox.Show(
                        $"Flash ROM with verify failed: {flashResult.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                ProgressText = "Error";
                AppendLog($"Error during flash ROM with verify: {ex.Message}");
                Logger.Error(ex, "Flash ROM with verify error in UI");
                
                MessageBox.Show(
                    $"Flash ROM with verify error: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                // Keep progress bar visible for 2 seconds so user can see final status
                await Task.Delay(2000);
                IsOperationInProgress = false;
                ProgressPercentage = 0;
            }
        }

        private async Task ToggleWriteProtectionAsync()
        {
            var isCurrentlyLocked = IsWriteProtected;
            var action = isCurrentlyLocked ? "unlock" : "lock";
            
            // Show confirmation dialog
            var result = MessageBox.Show(
                isCurrentlyLocked
                    ? "This will disable write protection on the flash chip (unlock it).\n\nOnce unlocked, the chip can be written to.\n\nContinue?"
                    : "This will enable write protection on the flash chip (lock it).\n\nOnce locked, the chip cannot be written to until unlocked.\n\nContinue?",
                $"{char.ToUpper(action[0])}{action.Substring(1)} Flash Confirmation",
                MessageBoxButton.YesNo,
                isCurrentlyLocked ? MessageBoxImage.Question : MessageBoxImage.Warning);
            
            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            StatusMessage = isCurrentlyLocked ? "Unlocking flash..." : "Locking flash...";

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                AppendLog($"Starting {action} flash operation...");

                OperationResult toggleResult;
                if (isCurrentlyLocked)
                {
                    toggleResult = await _service.UnlockFlashAsync(_cancellationTokenSource.Token);
                }
                else
                {
                    toggleResult = await _service.LockFlashAsync(_cancellationTokenSource.Token);
                }

                if (toggleResult.Success)
                {
                    StatusMessage = $"Flash {action}ed successfully";
                    AppendLog($"Flash {action}ed: {toggleResult.Message}");
                    
                    // Show completion message
                    MessageBox.Show(
                        toggleResult.Message,
                        $"{char.ToUpper(action[0])}{action.Substring(1)} Flash Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    
                    // Refresh chip info to show updated write protection status
                    await DetectChipAsync();
                }
                else
                {
                    StatusMessage = $"Flash {action} failed: {toggleResult.Message}";
                    AppendLog($"Flash {action} failed: {toggleResult.Message}");
                    
                    // Show error message
                    MessageBox.Show(
                        $"Flash {action} failed: {toggleResult.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                AppendLog($"Error during flash {action}: {ex.Message}");
                Logger.Error(ex, $"Flash {action} error in UI");
                
                MessageBox.Show(
                    $"Flash {action} error: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string BrowseFlashFile()
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select Binary File to Flash",
                    Filter = "Binary Files (*.bin)|*.bin|All Files (*.*)|*.*",
                    DefaultExt = "bin"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var filePath = openFileDialog.FileName;
                    AppendLog($"Selected flash file: {filePath}");
                    return filePath;
                }
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error opening file dialog");
                AppendLog($"Error opening file dialog: {ex.Message}");
                MessageBox.Show(
                    $"Error opening file dialog: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return null;
            }
        }

        #endregion

        #region USB Auto-Detection

        /// <summary>
        /// Called when a USB device is connected
        /// </summary>
        public async void OnUsbDeviceArrived(string vendorId, string productId)
        {
            // Check if this is a CH341 device (VID 1A86)
            if (!string.Equals(vendorId, CH341_VENDOR_ID, StringComparison.OrdinalIgnoreCase))
            {
                return; // Not a CH341 device, ignore
            }

            Logger.Info($"CH341 device detected (VID:{vendorId}, PID:{productId})");
            AppendLog($"CH341 device detected (VID:{vendorId}, PID:{productId})");

            // If already connected, ignore
            if (IsConnected)
            {
                Logger.Debug("Already connected to a device, ignoring new arrival");
                return;
            }

            // Wait a moment for the device to fully initialize
            await Task.Delay(500);

            // Try to auto-connect
            await AutoConnectAsync();
        }

        /// <summary>
        /// Called when a USB device is disconnected
        /// </summary>
        public async void OnUsbDeviceRemoved(string vendorId, string productId)
        {
            // Check if this is a CH341 device
            if (!string.Equals(vendorId, CH341_VENDOR_ID, StringComparison.OrdinalIgnoreCase))
            {
                return; // Not a CH341 device, ignore
            }

            Logger.Info($"CH341 device removed (VID:{vendorId}, PID:{productId})");
            AppendLog($"CH341 device removed (VID:{vendorId}, PID:{productId})");

            // If connected, disconnect
            if (IsConnected)
            {
                await DisconnectAsync();
            }
        }

        private async Task AutoConnectAsync()
        {
            try
            {
                AppendLog("Attempting auto-connect to CH341 device...");

                // Re-enumerate hardware to find newly connected device
                // Pass false to prevent infinite loop - don't auto-check during auto-connect
                await EnumerateDevicesAsync(checkForAutoConnect: false);

                // If we have a device selected and not already connected, connect
                if (SelectedDevice != null && !IsConnected && !IsBusy)
                {
                    await ConnectAsync();

                    if (IsConnected)
                    {
                        AppendLog("Auto-connect successful!");
                    }
                }
                else
                {
                    AppendLog("Auto-connect failed: No device available");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during auto-connect");
                AppendLog($"Auto-connect error: {ex.Message}");
            }
        }

        #endregion
    }
}
