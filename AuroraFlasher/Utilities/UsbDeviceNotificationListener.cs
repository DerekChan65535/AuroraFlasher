using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using AuroraFlasher.Logging;

namespace AuroraFlasher.Utilities
{
    /// <summary>
    /// Listens for USB device arrival and removal notifications using Windows WM_DEVICECHANGE messages
    /// </summary>
    public class UsbDeviceNotificationListener : IDisposable
    {
        #region Win32 API Declarations

        [DllImport("User32.dll", SetLastError = true)]
        private static extern IntPtr RegisterDeviceNotification(
            IntPtr hRecipient,
            IntPtr NotificationFilter,
            uint Flags);

        [DllImport("User32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterDeviceNotification(IntPtr Handle);

        private const uint DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

        private const int WM_DEVICECHANGE = 0x0219;
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

        [StructLayout(LayoutKind.Sequential)]
        private struct DEV_BROADCAST_HDR
        {
            public uint dbch_size;
            public uint dbch_devicetype;
            public uint dbch_reserved;
        }

        private const uint DBT_DEVTYP_DEVICEINTERFACE = 0x00000005;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEV_BROADCAST_DEVICEINTERFACE
        {
            public uint dbcc_size;
            public uint dbcc_devicetype;
            public uint dbcc_reserved;
            public Guid dbcc_classguid;

            // Must be longer than 1 to get value from lParam of WM_DEVICECHANGE
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
            public string dbcc_name;
        }

        // Standard USB device interface GUID
        private static readonly Guid GUID_DEVINTERFACE_USB_DEVICE = new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED");

        #endregion

        #region Fields and Events

        private HwndSource _handleSource;
        private IntPtr _notificationHandle;
        private bool _isListening;
        private bool _disposed;

        // Regex patterns for extracting VID and PID from device name
        private static readonly Regex _patternVid = new Regex(@"VID_([0-9A-Fa-f]{4})", RegexOptions.Compiled);
        private static readonly Regex _patternPid = new Regex(@"PID_([0-9A-Fa-f]{4})", RegexOptions.Compiled);

        /// <summary>
        /// Event raised when a USB device is connected
        /// </summary>
        public event EventHandler<UsbDeviceEventArgs> DeviceArrived;

        /// <summary>
        /// Event raised when a USB device is disconnected
        /// </summary>
        public event EventHandler<UsbDeviceEventArgs> DeviceRemoved;

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts listening for USB device notifications on the specified window
        /// </summary>
        /// <param name="window">The WPF window to hook into</param>
        public void StartListening(Window window)
        {
            if (_isListening)
            {
                Logger.Warn("USB device notification listener is already listening");
                return;
            }

            if (window == null)
                throw new ArgumentNullException(nameof(window));

            try
            {
                _handleSource = PresentationSource.FromVisual(window) as HwndSource;
                if (_handleSource == null)
                {
                    Logger.Error("Failed to get HwndSource from window");
                    return;
                }

                // Register for USB device notifications
                RegisterUsbDeviceNotification(_handleSource.Handle);

                // Hook into window message processing
                _handleSource.AddHook(WndProc);

                _isListening = true;
                Logger.Info("USB device notification listener started successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start USB device notification listener");
                throw;
            }
        }

        /// <summary>
        /// Stops listening for USB device notifications
        /// </summary>
        public void StopListening()
        {
            if (!_isListening)
                return;

            try
            {
                if (_handleSource != null)
                {
                    UnregisterUsbDeviceNotification();
                    _handleSource.RemoveHook(WndProc);
                    _handleSource = null;
                }

                _isListening = false;
                Logger.Info("USB device notification listener stopped");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error stopping USB device notification listener");
            }
        }

        #endregion

        #region Private Methods

        private void RegisterUsbDeviceNotification(IntPtr windowHandle)
        {
            var dbcc = new DEV_BROADCAST_DEVICEINTERFACE
            {
                dbcc_size = (uint)Marshal.SizeOf(typeof(DEV_BROADCAST_DEVICEINTERFACE)),
                dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE,
                dbcc_classguid = GUID_DEVINTERFACE_USB_DEVICE,
            };

            var notificationFilter = Marshal.AllocHGlobal(Marshal.SizeOf(dbcc));
            try
            {
                Marshal.StructureToPtr(dbcc, notificationFilter, true);

                _notificationHandle = RegisterDeviceNotification(
                    windowHandle,
                    notificationFilter,
                    DEVICE_NOTIFY_WINDOW_HANDLE);

                if (_notificationHandle == IntPtr.Zero)
                {
                    var error = Marshal.GetLastWin32Error();
                    throw new Win32Exception(error, "Failed to register USB device notifications");
                }

                Logger.Debug($"USB device notification registered (handle: 0x{_notificationHandle:X})");
            }
            finally
            {
                Marshal.FreeHGlobal(notificationFilter);
            }
        }

        private void UnregisterUsbDeviceNotification()
        {
            if (_notificationHandle != IntPtr.Zero)
            {
                if (UnregisterDeviceNotification(_notificationHandle))
                {
                    Logger.Debug("USB device notification unregistered successfully");
                }
                else
                {
                    Logger.Warn($"Failed to unregister USB device notification (error: {Marshal.GetLastWin32Error()})");
                }

                _notificationHandle = IntPtr.Zero;
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_DEVICECHANGE)
            {
                var eventType = wParam.ToInt32();

                switch (eventType)
                {
                    case DBT_DEVICEARRIVAL:
                        HandleDeviceEvent(lParam, isArrival: true);
                        break;

                    case DBT_DEVICEREMOVECOMPLETE:
                        HandleDeviceEvent(lParam, isArrival: false);
                        break;
                }
            }

            return IntPtr.Zero;
        }

        private void HandleDeviceEvent(IntPtr lParam, bool isArrival)
        {
            try
            {
                var hdr = (DEV_BROADCAST_HDR)Marshal.PtrToStructure(lParam, typeof(DEV_BROADCAST_HDR));

                if (hdr.dbch_devicetype == DBT_DEVTYP_DEVICEINTERFACE)
                {
                    var deviceInterface = (DEV_BROADCAST_DEVICEINTERFACE)Marshal.PtrToStructure(
                        lParam,
                        typeof(DEV_BROADCAST_DEVICEINTERFACE));

                    var deviceName = deviceInterface.dbcc_name;
                    var vid = GetVid(deviceName);
                    var pid = GetPid(deviceName);

                    Logger.Debug($"USB device {(isArrival ? "arrived" : "removed")}: VID={vid}, PID={pid}");

                    var eventArgs = new UsbDeviceEventArgs(vid, pid, deviceName);

                    if (isArrival)
                    {
                        DeviceArrived?.Invoke(this, eventArgs);
                    }
                    else
                    {
                        DeviceRemoved?.Invoke(this, eventArgs);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling USB device event");
            }
        }

        private static string GetVid(string source)
        {
            if (string.IsNullOrEmpty(source))
                return string.Empty;

            var match = _patternVid.Match(source);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static string GetPid(string source)
        {
            if (string.IsNullOrEmpty(source))
                return string.Empty;

            var match = _patternPid.Match(source);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed)
                return;

            StopListening();
            _disposed = true;
        }

        #endregion
    }

    /// <summary>
    /// Event arguments for USB device arrival/removal events
    /// </summary>
    public class UsbDeviceEventArgs : EventArgs
    {
        /// <summary>
        /// Vendor ID (VID) in hexadecimal format (e.g., "1A86")
        /// </summary>
        public string VendorId { get; }

        /// <summary>
        /// Product ID (PID) in hexadecimal format (e.g., "5512")
        /// </summary>
        public string ProductId { get; }

        /// <summary>
        /// Full device name/path
        /// </summary>
        public string DeviceName { get; }

        public UsbDeviceEventArgs(string vendorId, string productId, string deviceName)
        {
            VendorId = vendorId ?? string.Empty;
            ProductId = productId ?? string.Empty;
            DeviceName = deviceName ?? string.Empty;
        }

        /// <summary>
        /// Checks if this device matches the specified VID
        /// </summary>
        public bool IsVendorId(string vid)
        {
            return string.Equals(VendorId, vid, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if this device matches the specified VID and PID
        /// </summary>
        public bool IsDevice(string vid, string pid)
        {
            return string.Equals(VendorId, vid, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(ProductId, pid, StringComparison.OrdinalIgnoreCase);
        }
    }
}
