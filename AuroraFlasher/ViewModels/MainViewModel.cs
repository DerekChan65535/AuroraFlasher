using System;
using System.Collections.ObjectModel;
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
                if (SetProperty(ref _isConnected, value))
                {
                    OnPropertyChanged(nameof(CanRead));
                }
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(CanRead));
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
            set => SetProperty(ref _chipInfo, value);
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

        private string _readAddress;
        public string ReadAddress
        {
            get => _readAddress;
            set => SetProperty(ref _readAddress, value);
        }

        private string _readLength;
        public string ReadLength
        {
            get => _readLength;
            set => SetProperty(ref _readLength, value);
        }

        private string _flashFilePath;
        public string FlashFilePath
        {
            get => _flashFilePath;
            set => SetProperty(ref _flashFilePath, value);
        }

        public ObservableCollection<IHardware> AvailableDevices { get; }

        private IHardware _selectedDevice;
        public IHardware SelectedDevice
        {
            get => _selectedDevice;
            set => SetProperty(ref _selectedDevice, value);
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

        #endregion

        #region Can Execute Properties

        public bool CanRead => IsConnected && !IsBusy && !string.IsNullOrEmpty(ChipInfo);
        public bool CanClearFlash => IsConnected && !IsBusy && !string.IsNullOrEmpty(ChipInfo);
        public bool CanFlash => IsConnected && !IsBusy && !string.IsNullOrEmpty(ChipInfo) && !string.IsNullOrEmpty(FlashFilePath);

        #endregion

        #region Commands

        public ICommand ReadMemoryCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand ClearFlashCommand { get; }
        public ICommand BrowseFlashFileCommand { get; }
        public ICommand FlashCommand { get; }
        public ICommand FlashWithVerifyCommand { get; }

        #endregion

        public MainViewModel()
        {
            _service = new ProgrammerService();
            AvailableDevices = new ObservableCollection<IHardware>();

            // Initialize properties
            StatusMessage = "Ready";
            DeviceInfo = "No device connected";
            ChipInfo = "No chip detected";
            LogOutput = string.Empty;
            HexLines = new ObservableCollection<HexLineData>();
            ReadAddress = "0x000000";
            ReadLength = "256";

            // Initialize commands
            ReadMemoryCommand = new RelayCommand(async () => await ReadMemoryAsync(), () => CanRead);
            ClearLogCommand = new RelayCommand(() => LogOutput = string.Empty);
            ClearFlashCommand = new RelayCommand(async () => await ClearFlashAsync(), () => CanClearFlash);
            BrowseFlashFileCommand = new RelayCommand(() => BrowseFlashFile());
            FlashCommand = new RelayCommand(async () => await FlashAsync(), () => CanFlash);
            FlashWithVerifyCommand = new RelayCommand(async () => await FlashWithVerifyAsync(), () => CanFlash);

            // Auto-enumerate on startup (will auto-connect and auto-detect if device present)
            Task.Run(async () => await EnumerateDevicesAsync());
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

                    ChipInfo = chipInfoBuilder.ToString();
                    
                    // Set read length to detected chip size
                    ReadLength = chip.Size.ToString();
                    AppendLog($"Default read size set to full chip ({chip.SizeKB}KB)");
                }
                else
                {
                    StatusMessage = $"Detection failed: {result.Message}";
                    ChipInfo = "Detection failed";
                    AppendLog($"Detection failed: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                ChipInfo = "Detection error";
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
                // Parse address
                if (!TryParseHex(ReadAddress, out uint address))
                {
                    StatusMessage = "Invalid address format";
                    AppendLog("Invalid address format. Use hex format like 0x000000");
                    return;
                }

                // Parse length (support up to 64MB for large chips)
                if (!int.TryParse(ReadLength, out int length) || length <= 0 || length > 67108864)
                {
                    StatusMessage = "Invalid length (must be 1-67108864 bytes / 64MB)";
                    AppendLog("Invalid length. Must be between 1 and 67108864 bytes (64MB max)");
                    return;
                }

                _cancellationTokenSource = new CancellationTokenSource();
                AppendLog($"Reading {length} bytes from 0x{address:X6}...");

                // Create progress reporter
                var progress = new Progress<ProgressInfo>(progressInfo =>
                {
                    ProgressPercentage = progressInfo.Percentage;
                    ProgressText = $"{progressInfo.Percentage:F1}% - {progressInfo.BytesProcessed:N0} / {progressInfo.TotalBytes:N0} bytes - {progressInfo.Speed / 1024:F1} KB/s";
                    StatusMessage = $"Reading... {progressInfo.Percentage:F0}%";
                });

                var result = await _service.ReadMemoryAsync(address, length, progress, _cancellationTokenSource.Token);

                if (result.Success && result.Data != null)
                {
                    StatusMessage = $"Read {result.Data.Length} bytes successfully";
                    UpdateHexDump(result.Data, address);
                    AppendLog($"Read {result.Data.Length} bytes from 0x{address:X6}");
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

        private bool TryParseHex(string input, out uint value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            string cleaned = input.Trim().ToLower();
            if (cleaned.StartsWith("0x"))
                cleaned = cleaned.Substring(2);

            return uint.TryParse(cleaned, System.Globalization.NumberStyles.HexNumber, null, out value);
        }

        /// <summary>
        /// Update hex dump display with virtualized line-by-line rendering.
        /// This method populates HexLines collection for ListView virtualization.
        /// </summary>
        private void UpdateHexDump(byte[] data, uint startAddress)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                HexLines.Clear();

                int bytesPerLine = 16;
                var lines = new System.Collections.Generic.List<HexLineData>();

                for (int i = 0; i < data.Length; i += bytesPerLine)
                {
                    var sb = new StringBuilder();

                    // Address
                    sb.AppendFormat("{0:X4}:  ", startAddress + i);

                    // Hex bytes
                    int lineLength = Math.Min(bytesPerLine, data.Length - i);
                    for (int j = 0; j < lineLength; j++)
                    {
                        sb.AppendFormat("{0:X2} ", data[i + j]);
                        if (j == 7) sb.Append(" "); // Extra space in middle
                    }

                    // Padding if incomplete line
                    if (lineLength < bytesPerLine)
                    {
                        int padding = (bytesPerLine - lineLength) * 3;
                        if (lineLength <= 7) padding++; // Account for middle space
                        sb.Append(new string(' ', padding));
                    }

                    // ASCII representation
                    sb.Append("  ");
                    for (int j = 0; j < lineLength; j++)
                    {
                        byte b = data[i + j];
                        sb.Append(b >= 32 && b <= 126 ? (char)b : '.');
                    }

                    lines.Add(new HexLineData(sb.ToString()));
                }

                // Batch add all lines to collection (more efficient than adding one by one)
                foreach (var line in lines)
                {
                    HexLines.Add(line);
                }
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
            // Show confirmation dialog
            var result = MessageBox.Show(
                $"This will write the binary file to the chip starting at address 0x000000.\n\nFile: {FlashFilePath}\n\nContinue?",
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
                AppendLog($"Starting flash operation for file: {FlashFilePath}");

                // Create progress reporter
                var progress = new Progress<ProgressInfo>(progressInfo =>
                {
                    ProgressPercentage = progressInfo.Percentage;
                    ProgressText = $"{progressInfo.Percentage:F1}% - {progressInfo.Status}";
                    StatusMessage = $"Flashing... {progressInfo.Percentage:F0}%";
                });

                var flashResult = await _service.FlashAsync(FlashFilePath, progress, _cancellationTokenSource.Token);

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
            // Show confirmation dialog
            var result = MessageBox.Show(
                $"This will write the binary file to the chip with immediate verification.\n\nFile: {FlashFilePath}\n\nThis will take longer but ensures data integrity.\n\nContinue?",
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
                AppendLog($"Starting flash with verify operation for file: {FlashFilePath}");

                // Create progress reporter
                var progress = new Progress<ProgressInfo>(progressInfo =>
                {
                    ProgressPercentage = progressInfo.Percentage;
                    ProgressText = $"{progressInfo.Percentage:F1}% - {progressInfo.Status}";
                    StatusMessage = $"Flashing with verify... {progressInfo.Percentage:F0}%";
                });

                var flashResult = await _service.FlashWithVerifyAsync(FlashFilePath, progress, _cancellationTokenSource.Token);

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

        private void BrowseFlashFile()
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
                    FlashFilePath = openFileDialog.FileName;
                    AppendLog($"Selected flash file: {FlashFilePath}");
                }
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
