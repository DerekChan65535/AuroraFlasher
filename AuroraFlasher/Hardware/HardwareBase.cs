using System;
using System.Threading;
using System.Threading.Tasks;
using AuroraFlasher.Interfaces;
using AuroraFlasher.Models;

namespace AuroraFlasher.Hardware
{
    /// <summary>
    /// Abstract base class for all hardware implementations
    /// </summary>
    public abstract class HardwareBase : IHardware
    {
        private bool _disposed = false;
        protected bool _isConnected = false;

        #region Properties

        public abstract HardwareType Type { get; }
        public abstract string Name { get; }
        public virtual bool IsConnected => _isConnected;
        public abstract HardwareCapabilities Capabilities { get; }
        
        private SpiSpeed _speed = SpiSpeed.Normal;
        public virtual SpiSpeed Speed
        {
            get => _speed;
            set => _speed = value;
        }

        #endregion

        #region Connection Management

        public abstract Task<string[]> EnumerateDevicesAsync(CancellationToken cancellationToken = default);
        public abstract Task<OperationResult> OpenAsync(string devicePath = null, CancellationToken cancellationToken = default);
        public abstract Task<OperationResult> CloseAsync(CancellationToken cancellationToken = default);

        #endregion

        #region SPI Operations

        public abstract Task<OperationResult> SpiInitAsync(CancellationToken cancellationToken = default);
        public abstract Task<OperationResult> SpiDeinitAsync(CancellationToken cancellationToken = default);
        public abstract Task<OperationResult> SpiSendCommandAsync(byte command, CancellationToken cancellationToken = default);
        public abstract Task<OperationResult<byte[]>> SpiReadAsync(int length, CancellationToken cancellationToken = default);
        public abstract Task<OperationResult> SpiWriteAsync(byte[] data, CancellationToken cancellationToken = default);
        public abstract Task<OperationResult<byte[]>> SpiTransferAsync(byte[] writeData, int readLength, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send SPI command with address and read data
        /// Default implementation combines command, address bytes, and read
        /// </summary>
        public virtual async Task<OperationResult<byte[]>> SpiReadWithAddressAsync(byte command, uint address, int addressBytes, int dataLength, CancellationToken cancellationToken = default)
        {
            try
            {
                // Build command buffer: [CMD] [ADDR...] 
                byte[] cmdBuffer = new byte[1 + addressBytes];
                cmdBuffer[0] = command;

                // Add address bytes (big-endian)
                for (int i = 0; i < addressBytes; i++)
                {
                    cmdBuffer[1 + i] = (byte)(address >> ((addressBytes - 1 - i) * 8));
                }

                // Transfer: write command+address, read data
                return await SpiTransferAsync(cmdBuffer, dataLength, cancellationToken);
            }
            catch (Exception ex)
            {
                return OperationResult<byte[]>.FailureResult($"SPI read with address failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Send SPI command with address and write data
        /// Default implementation combines command, address bytes, and data
        /// </summary>
        public virtual async Task<OperationResult> SpiWriteWithAddressAsync(byte command, uint address, int addressBytes, byte[] data, CancellationToken cancellationToken = default)
        {
            try
            {
                // Build command buffer: [CMD] [ADDR...] [DATA...]
                byte[] cmdBuffer = new byte[1 + addressBytes + data.Length];
                cmdBuffer[0] = command;

                // Add address bytes (big-endian)
                for (int i = 0; i < addressBytes; i++)
                {
                    cmdBuffer[1 + i] = (byte)(address >> ((addressBytes - 1 - i) * 8));
                }

                // Add data
                Array.Copy(data, 0, cmdBuffer, 1 + addressBytes, data.Length);

                // Write entire buffer
                return await SpiWriteAsync(cmdBuffer, cancellationToken);
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"SPI write with address failed: {ex.Message}", ex);
            }
        }

        #endregion

        #region I2C Operations

        public virtual Task<OperationResult> I2CInitAsync(int speedKHz = 100, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OperationResult.FailureResult("I2C not supported by this hardware"));
        }

        public virtual Task<OperationResult> I2CDeinitAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OperationResult.FailureResult("I2C not supported by this hardware"));
        }

        public virtual Task<OperationResult<byte[]>> I2CScanAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OperationResult<byte[]>.FailureResult("I2C not supported by this hardware"));
        }

        public virtual Task<OperationResult<byte[]>> I2CReadAsync(byte deviceAddress, int length, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OperationResult<byte[]>.FailureResult("I2C not supported by this hardware"));
        }

        public virtual Task<OperationResult> I2CWriteAsync(byte deviceAddress, byte[] data, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OperationResult.FailureResult("I2C not supported by this hardware"));
        }

        public virtual Task<OperationResult<byte[]>> I2CReadFromAddressAsync(byte deviceAddress, uint memoryAddress, int addressBytes, int dataLength, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OperationResult<byte[]>.FailureResult("I2C not supported by this hardware"));
        }

        public virtual Task<OperationResult> I2CWriteToAddressAsync(byte deviceAddress, uint memoryAddress, int addressBytes, byte[] data, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OperationResult.FailureResult("I2C not supported by this hardware"));
        }

        #endregion

        #region MicroWire Operations

        public virtual Task<OperationResult> MicroWireInitAsync(int addressBits, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OperationResult.FailureResult("MicroWire not supported by this hardware"));
        }

        public virtual Task<OperationResult> MicroWireDeinitAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OperationResult.FailureResult("MicroWire not supported by this hardware"));
        }

        public virtual Task<OperationResult> MicroWireEnableAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OperationResult.FailureResult("MicroWire not supported by this hardware"));
        }

        public virtual Task<OperationResult> MicroWireDisableAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OperationResult.FailureResult("MicroWire not supported by this hardware"));
        }

        public virtual Task<OperationResult<byte[]>> MicroWireReadAsync(uint address, int length, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OperationResult<byte[]>.FailureResult("MicroWire not supported by this hardware"));
        }

        public virtual Task<OperationResult> MicroWireWriteAsync(uint address, byte[] data, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OperationResult.FailureResult("MicroWire not supported by this hardware"));
        }

        public virtual Task<OperationResult> MicroWireEraseAsync(uint address, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OperationResult.FailureResult("MicroWire not supported by this hardware"));
        }

        public virtual Task<OperationResult> MicroWireEraseAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OperationResult.FailureResult("MicroWire not supported by this hardware"));
        }

        #endregion

        #region Utility Methods

        public virtual Task<OperationResult<string>> GetFirmwareVersionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OperationResult<string>.SuccessResult("N/A", "Firmware version not available"));
        }

        public virtual Task<OperationResult> SetGpioPinAsync(int pin, bool state, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OperationResult.FailureResult("GPIO not supported by this hardware"));
        }

        public virtual Task<OperationResult<bool>> GetGpioPinAsync(int pin, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OperationResult<bool>.FailureResult("GPIO not supported by this hardware"));
        }

        public virtual Task DelayAsync(int milliseconds, CancellationToken cancellationToken = default)
        {
            return Task.Delay(milliseconds, cancellationToken);
        }

        #endregion

        #region IDisposable Implementation

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    if (_isConnected)
                    {
                        CloseAsync().GetAwaiter().GetResult();
                    }
                }

                // Dispose unmanaged resources
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~HardwareBase()
        {
            Dispose(false);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Throw if disposed
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        /// <summary>
        /// Throw if not connected
        /// </summary>
        protected void ThrowIfNotConnected()
        {
            ThrowIfDisposed();
            if (!_isConnected)
                throw new InvalidOperationException("Hardware not connected");
        }

        #endregion
    }
}
