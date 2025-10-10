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
                    OnPropertyChanged(nameof(CanConnect));
                    OnPropertyChanged(nameof(CanDisconnect));
                    OnPropertyChanged(nameof(CanDetect));
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
                    OnPropertyChanged(nameof(CanConnect));
                    OnPropertyChanged(nameof(CanDisconnect));
                    OnPropertyChanged(nameof(CanDetect));
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

        private string _hexOutput;
        public string HexOutput
        {
            get => _hexOutput;
            set => SetProperty(ref _hexOutput, value);
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

        public ObservableCollection<IHardware> AvailableDevices { get; }

        private IHardware _selectedDevice;
        public IHardware SelectedDevice
        {
            get => _selectedDevice;
            set => SetProperty(ref _selectedDevice, value);
        }

        #endregion

        #region Can Execute Properties

        public bool CanConnect => !IsConnected && !IsBusy;
        public bool CanDisconnect => IsConnected && !IsBusy;
        public bool CanDetect => IsConnected && !IsBusy;
        public bool CanRead => IsConnected && !IsBusy && !string.IsNullOrEmpty(ChipInfo);

        #endregion

        #region Commands

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand DetectChipCommand { get; }
        public ICommand ReadMemoryCommand { get; }
        public ICommand ClearLogCommand { get; }

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
            HexOutput = string.Empty;
            ReadAddress = "0x000000";
            ReadLength = "256";

            // Initialize commands
            ConnectCommand = new RelayCommand(async () => await ConnectAsync(), () => CanConnect);
            DisconnectCommand = new RelayCommand(async () => await DisconnectAsync(), () => CanDisconnect);
            DetectChipCommand = new RelayCommand(async () => await DetectChipAsync(), () => CanDetect);
            ReadMemoryCommand = new RelayCommand(async () => await ReadMemoryAsync(), () => CanRead);
            ClearLogCommand = new RelayCommand(() => LogOutput = string.Empty);

            // Auto-enumerate on startup
            Task.Run(async () => await EnumerateDevicesAsync());
        }

        #region Methods

        private async Task EnumerateDevicesAsync()
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
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error enumerating hardware: {ex.Message}");
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
                HexOutput = string.Empty;
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
                var result = await _service.DetectChipAsync(ProtocolType.SPI, _cancellationTokenSource.Token);

                if (result.Success && result.Data != null)
                {
                    var chip = result.Data;
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
                    AppendLog($"Chip detected: {chip.Name} ({chip.SizeKB}KB)");
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

                // Parse length
                if (!int.TryParse(ReadLength, out int length) || length <= 0 || length > 65536)
                {
                    StatusMessage = "Invalid length (must be 1-65536)";
                    AppendLog("Invalid length. Must be between 1 and 65536 bytes");
                    return;
                }

                _cancellationTokenSource = new CancellationTokenSource();
                AppendLog($"Reading {length} bytes from 0x{address:X6}...");

                var result = await _service.ReadMemoryAsync(address, length, null, _cancellationTokenSource.Token);

                if (result.Success && result.Data != null)
                {
                    StatusMessage = $"Read {result.Data.Length} bytes successfully";
                    HexOutput = FormatHexDump(result.Data, address);
                    AppendLog($"Read {result.Data.Length} bytes from 0x{address:X6}");
                }
                else
                {
                    StatusMessage = $"Read failed: {result.Message}";
                    AppendLog($"Read failed: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                AppendLog($"Error reading memory: {ex.Message}");
                Logger.Error(ex, "Read error in UI");
            }
            finally
            {
                IsBusy = false;
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

        private string FormatHexDump(byte[] data, uint startAddress)
        {
            var sb = new StringBuilder();
            int bytesPerLine = 16;

            for (int i = 0; i < data.Length; i += bytesPerLine)
            {
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

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private void AppendLog(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogOutput += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            });
        }

        #endregion
    }
}
