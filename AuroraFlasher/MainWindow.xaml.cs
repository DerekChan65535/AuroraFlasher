using System;
using System.Windows;
using AuroraFlasher.Utilities;
using AuroraFlasher.ViewModels;

namespace AuroraFlasher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private UsbDeviceNotificationListener _usbListener;

        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize USB device notification listener
            _usbListener = new UsbDeviceNotificationListener();
            
            // Subscribe to USB events and wire them to ViewModel
            _usbListener.DeviceArrived += OnUsbDeviceArrived;
            _usbListener.DeviceRemoved += OnUsbDeviceRemoved;
            
            // Start listening once the window is loaded
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Start listening for USB device events
                _usbListener.StartListening(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to start USB device monitoring: {ex.Message}\n\nAuto-detection will not be available.",
                    "USB Monitoring Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Stop listening and cleanup
            _usbListener?.StopListening();
            _usbListener?.Dispose();
        }

        private void OnUsbDeviceArrived(object sender, UsbDeviceEventArgs e)
        {
            // Forward event to ViewModel
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.OnUsbDeviceArrived(e.VendorId, e.ProductId);
            }
        }

        private void OnUsbDeviceRemoved(object sender, UsbDeviceEventArgs e)
        {
            // Forward event to ViewModel
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.OnUsbDeviceRemoved(e.VendorId, e.ProductId);
            }
        }
    }
}