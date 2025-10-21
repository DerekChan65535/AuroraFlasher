using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuroraFlasher.Interfaces;
using AuroraFlasher.Logging;
using AuroraFlasher.Models;
using AuroraFlasher.Utilities;

namespace AuroraFlasher.Protocols
{
    /// <summary>
    /// SPI 25-series flash memory protocol implementation
    /// Supports W25Q, MX25L, GD25Q, AT25, SST25, EN25, and other 25-series chips
    /// </summary>
    public class Spi25Protocol : ISpiProtocol
    {
        private readonly IHardware _hardware;
        private bool _disposed = false;
        private bool _is4ByteMode = false;

        // Standard SPI25 command codes
        private const byte CMD_WRITE_ENABLE = 0x06;
        private const byte CMD_WRITE_DISABLE = 0x04;
        private const byte CMD_READ_STATUS_REG = 0x05;
        private const byte CMD_WRITE_STATUS_REG = 0x01;
        private const byte CMD_READ_STATUS_REG2 = 0x35;
        private const byte CMD_WRITE_STATUS_REG2 = 0x31;
        private const byte CMD_READ_STATUS_REG3 = 0x15;
        private const byte CMD_WRITE_STATUS_REG3 = 0x11;
        private const byte CMD_READ_DATA = 0x03;
        private const byte CMD_FAST_READ = 0x0B;
        private const byte CMD_PAGE_PROGRAM = 0x02;
        private const byte CMD_SECTOR_ERASE = 0x20;
        private const byte CMD_BLOCK_ERASE_32K = 0x52;
        private const byte CMD_BLOCK_ERASE_64K = 0xD8;
        private const byte CMD_CHIP_ERASE = 0xC7;
        private const byte CMD_CHIP_ERASE_ALT = 0x60;
        private const byte CMD_CHIP_ERASE_ATMEL = 0x62;
        private const byte CMD_READ_JEDEC_ID = 0x9F;
        private const byte CMD_READ_MFR_DEVICE_ID = 0x90;
        private const byte CMD_READ_ELECTRONIC_ID = 0xAB;
        private const byte CMD_READ_UNIQUE_ID = 0x4B;
        private const byte CMD_RELEASE_POWER_DOWN = 0xAB;
        private const byte CMD_POWER_DOWN = 0xB9;
        private const byte CMD_ENABLE_WRITE_STATUS = 0x50;
        private const byte CMD_ENTER_4BYTE_MODE = 0xB7;
        private const byte CMD_EXIT_4BYTE_MODE = 0xE9;
        private const byte CMD_WRITE_EXT_ADDR_REG = 0xC5;
        private const byte CMD_READ_EXT_ADDR_REG = 0xC8;

        // SST-specific AAI commands
        private const byte CMD_SST_AAI_BYTE = 0xAF;
        private const byte CMD_SST_AAI_WORD = 0xAD;

        // Status register bits
        private const byte STATUS_BUSY = 0x01;
        private const byte STATUS_WEL = 0x02;
        private const byte STATUS_BP0 = 0x04;
        private const byte STATUS_BP1 = 0x08;
        private const byte STATUS_BP2 = 0x10;
        private const byte STATUS_BP3 = 0x20;
        private const byte STATUS_SRP = 0x80;

        public IHardware Hardware => _hardware;
        public SpiCommandSet CommandSet { get; }
        public ChipInfo ChipInfo { get; set; }

        public event EventHandler<ProgressInfo> ProgressChanged;

        public Spi25Protocol(IHardware hardware, SpiCommandSet commandSet = SpiCommandSet.Standard)
        {
            _hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
            CommandSet = commandSet;
        }

        #region Lifecycle

        public async Task<OperationResult> EnterProgrammingModeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_hardware.IsConnected)
                    return OperationResult.FailureResult("Hardware not connected");

                var result = await _hardware.SpiInitAsync(cancellationToken);
                if (!result.Success)
                    return result;

                // Wait a bit for initialization
                await Task.Delay(50, cancellationToken);

                // Release power-down mode if chip was sleeping
                await _hardware.SpiSendCommandAsync(CMD_RELEASE_POWER_DOWN, cancellationToken);
                await Task.Delay(2, cancellationToken);

                return OperationResult.SuccessResult("Entered SPI programming mode");
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"Failed to enter programming mode: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult> ExitProgrammingModeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Exit 4-byte mode if enabled
                if (_is4ByteMode)
                    await Exit4ByteAddressModeAsync(cancellationToken);

                var result = await _hardware.SpiDeinitAsync(cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"Failed to exit programming mode: {ex.Message}", ex);
            }
        }

        #endregion

        #region Identification

        public async Task<OperationResult<MemoryId>> ReadIdAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.Info("Reading SPI chip ID...");
                var memoryId = new MemoryId();

                // Read JEDEC ID (9Fh) - 3 bytes
                Logger.Debug("Sending CMD_READ_JEDEC_ID (0x9F)");
                var jedecResult = await _hardware.SpiTransferAsync(new byte[] { CMD_READ_JEDEC_ID }, 3, cancellationToken);
                if (jedecResult.Success && jedecResult.Data != null)
                {
                    memoryId.JedecId = jedecResult.Data;
                    Logger.Debug($"JEDEC ID: {BitConverter.ToString(jedecResult.Data)}");
                }

                // Read Manufacturer/Device ID (90h) - 2 bytes
                Logger.Debug("Sending CMD_READ_MFR_DEVICE_ID (0x90)");
                var mfrResult = await _hardware.SpiTransferAsync(new byte[] { CMD_READ_MFR_DEVICE_ID, 0x00, 0x00, 0x00 }, 2, cancellationToken);
                if (mfrResult.Success && mfrResult.Data != null && mfrResult.Data.Length >= 2)
                {
                    memoryId.ManufacturerId = mfrResult.Data[0];
                    memoryId.DeviceId = (ushort)((mfrResult.Data[0] << 8) | mfrResult.Data[1]);
                    Logger.Debug($"Manufacturer ID: 0x{memoryId.ManufacturerId:X2}, Device ID: 0x{memoryId.DeviceId:X4}");
                }

                // Read Electronic ID (ABh) - 1 byte
                Logger.Debug("Sending CMD_READ_ELECTRONIC_ID (0xAB)");
                var elecResult = await _hardware.SpiTransferAsync(new byte[] { CMD_READ_ELECTRONIC_ID, 0x00, 0x00, 0x00 }, 1, cancellationToken);
                if (elecResult.Success && elecResult.Data != null && elecResult.Data.Length > 0)
                {
                    memoryId.ElectronicSignature = elecResult.Data[0];
                    Logger.Debug($"Electronic Signature: 0x{memoryId.ElectronicSignature:X2}");
                }

                // Read Unique ID (15h) - 2 bytes
                Logger.Debug("Reading unique ID (STATUS_REG3)");
                var uniqueResult = await _hardware.SpiTransferAsync(new byte[] { CMD_READ_STATUS_REG3 }, 2, cancellationToken);
                if (uniqueResult.Success && uniqueResult.Data != null)
                {
                    memoryId.UniqueId = uniqueResult.Data;
                    Logger.Debug($"Unique ID: {BitConverter.ToString(uniqueResult.Data)}");
                }

                Logger.Info("Successfully read chip ID");
                return OperationResult<MemoryId>.SuccessResult(memoryId, "Successfully read chip ID");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to read chip ID");
                return OperationResult<MemoryId>.FailureResult($"Failed to read chip ID: {ex.Message}", ex);
            }
        }

        #endregion

        #region Read Operations

        public async Task<OperationResult<byte[]>> ReadAsync(uint address, int length, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.Info($"Reading {length} bytes from address 0x{address:X6}");
                var stopwatch = Stopwatch.StartNew();
                var data = new byte[length];
                var bytesRead = 0;
                const int chunkSize = 2048; // Read in 2KB chunks (CH341 hardware limit)

                while (bytesRead < length)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var remaining = length - bytesRead;
                    var toRead = Math.Min(remaining, chunkSize);
                    var currentAddress = address + (uint)bytesRead;

                    Logger.Debug($"Reading chunk: address=0x{currentAddress:X6}, size={toRead}, progress={bytesRead}/{length}");

                    // Build command with address
                    var cmd = _is4ByteMode
                        ? new byte[] { CMD_READ_DATA, (byte)(currentAddress >> 24), (byte)(currentAddress >> 16), (byte)(currentAddress >> 8), (byte)currentAddress }
                        : new byte[] { CMD_READ_DATA, (byte)(currentAddress >> 16), (byte)(currentAddress >> 8), (byte)currentAddress };

                    var chunkStartTime = DateTime.Now;
                    var result = await _hardware.SpiTransferAsync(cmd, toRead, cancellationToken);
                    var chunkElapsed = DateTime.Now - chunkStartTime;
                    
                    Logger.Debug($"Chunk read completed in {chunkElapsed.TotalMilliseconds:F1}ms");
                    
                    if (!result.Success)
                        return OperationResult<byte[]>.FailureResult($"Read failed at address 0x{currentAddress:X6}: {result.Message}");

                    Array.Copy(result.Data, 0, data, bytesRead, toRead);
                    bytesRead += toRead;

                    // Report progress
                    if (progress != null)
                    {
                        var progressInfo = new ProgressInfo(bytesRead, length, $"Reading... {stopwatch.Elapsed.TotalSeconds:F1}s");
                        progress.Report(progressInfo);
                        OnProgressChanged(progressInfo);
                    }
                }

                stopwatch.Stop();
                Logger.Info($"Successfully read {length} bytes from 0x{address:X6} in {stopwatch.Elapsed.TotalSeconds:F2}s ({length / stopwatch.Elapsed.TotalSeconds / 1024:F1} KB/s)");
                return OperationResult<byte[]>.SuccessResult(data, $"Read {length} bytes from 0x{address:X6}");
            }
            catch (OperationCanceledException)
            {
                Logger.Warn($"Read operation cancelled at address 0x{address:X6}");
                return OperationResult<byte[]>.FailureResult("Read operation cancelled");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Read failed at address 0x{address:X6}, length {length}");
                return OperationResult<byte[]>.FailureResult($"Read failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult<byte[]>> FastReadAsync(uint address, int length, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var data = new byte[length];
                var bytesRead = 0;
                const int chunkSize = 2048; // 2KB chunks for CH341 hardware limit

                while (bytesRead < length)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var remaining = length - bytesRead;
                    var toRead = Math.Min(remaining, chunkSize);
                    var currentAddress = address + (uint)bytesRead;

                    // Fast read requires dummy byte after address
                    var cmd = _is4ByteMode
                        ? new byte[] { CMD_FAST_READ, (byte)(currentAddress >> 24), (byte)(currentAddress >> 16), (byte)(currentAddress >> 8), (byte)currentAddress, 0x00 }
                        : new byte[] { CMD_FAST_READ, (byte)(currentAddress >> 16), (byte)(currentAddress >> 8), (byte)currentAddress, 0x00 };

                    var result = await _hardware.SpiTransferAsync(cmd, toRead, cancellationToken);
                    if (!result.Success)
                        return OperationResult<byte[]>.FailureResult($"Fast read failed at address 0x{currentAddress:X6}: {result.Message}");

                    Array.Copy(result.Data, 0, data, bytesRead, toRead);
                    bytesRead += toRead;

                    if (progress != null)
                    {
                        var progressInfo = new ProgressInfo(bytesRead, length, $"Fast reading... {stopwatch.Elapsed.TotalSeconds:F1}s");
                        progress.Report(progressInfo);
                        OnProgressChanged(progressInfo);
                    }
                }

                return OperationResult<byte[]>.SuccessResult(data, $"Fast read {length} bytes from 0x{address:X6}");
            }
            catch (OperationCanceledException)
            {
                return OperationResult<byte[]>.FailureResult("Fast read operation cancelled");
            }
            catch (Exception ex)
            {
                return OperationResult<byte[]>.FailureResult($"Fast read failed: {ex.Message}", ex);
            }
        }

        #endregion

        #region Write Operations

        public async Task<OperationResult> WriteAsync(uint address, byte[] data, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (data == null || data.Length == 0)
                {
                    Logger.Warn("WriteAsync called with null or empty data");
                    return OperationResult.FailureResult("No data to write");
                }

                Logger.Info($"Writing {data.Length} bytes to address 0x{address:X6}");
                var stopwatch = Stopwatch.StartNew();
                var bytesWritten = 0;
                var pageSize = ChipInfo?.PageSize ?? 256;
                Logger.Debug($"Using page size: {pageSize} bytes");

                while (bytesWritten < data.Length)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var currentAddress = address + (uint)bytesWritten;
                    
                    // Calculate bytes to write in this page (respect page boundaries)
                    var pageOffset = (int)(currentAddress % pageSize);
                    var bytesInPage = Math.Min(pageSize - pageOffset, data.Length - bytesWritten);

                    // Extract page data
                    var pageData = new byte[bytesInPage];
                    Array.Copy(data, bytesWritten, pageData, 0, bytesInPage);

                    // Write page
                    var result = await WritePageAsync(currentAddress, pageData, cancellationToken);
                    if (!result.Success)
                        return OperationResult.FailureResult($"Write failed at address 0x{currentAddress:X6}: {result.Message}");

                    bytesWritten += bytesInPage;

                    if (progress != null)
                    {
                        var progressInfo = new ProgressInfo(bytesWritten, data.Length, $"Writing... {stopwatch.Elapsed.TotalSeconds:F1}s");
                        progress.Report(progressInfo);
                        OnProgressChanged(progressInfo);
                    }
                }

                stopwatch.Stop();
                Logger.Info($"Successfully wrote {data.Length} bytes to 0x{address:X6} in {stopwatch.Elapsed.TotalSeconds:F2}s ({data.Length / stopwatch.Elapsed.TotalSeconds / 1024:F1} KB/s)");
                return OperationResult.SuccessResult($"Wrote {data.Length} bytes to 0x{address:X6}");
            }
            catch (OperationCanceledException)
            {
                Logger.Warn($"Write operation cancelled at address 0x{address:X6}");
                return OperationResult.FailureResult("Write operation cancelled");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Write failed at address 0x{address:X6}, length {data?.Length ?? 0}");
                return OperationResult.FailureResult($"Write failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult> WritePageAsync(uint address, byte[] data, CancellationToken cancellationToken = default)
        {
            try
            {
                if (data == null || data.Length == 0)
                    return OperationResult.FailureResult("No data to write");

                // Write enable
                var wenResult = await WriteEnableAsync(cancellationToken);
                if (!wenResult.Success)
                    return wenResult;

                // Build page program command with address
                var cmd = _is4ByteMode
                    ? new byte[] { CMD_PAGE_PROGRAM, (byte)(address >> 24), (byte)(address >> 16), (byte)(address >> 8), (byte)address }
                    : new byte[] { CMD_PAGE_PROGRAM, (byte)(address >> 16), (byte)(address >> 8), (byte)address };

                // Combine command and data
                var fullCmd = new byte[cmd.Length + data.Length];
                Array.Copy(cmd, 0, fullCmd, 0, cmd.Length);
                Array.Copy(data, 0, fullCmd, cmd.Length, data.Length);

                var writeResult = await _hardware.SpiWriteAsync(fullCmd, cancellationToken);
                if (!writeResult.Success)
                    return writeResult;

                // Wait for write to complete
                var waitResult = await WaitNotBusyAsync(5000, cancellationToken);
                if (!waitResult.Success)
                    return waitResult;

                return OperationResult.SuccessResult($"Wrote page at 0x{address:X6}");
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"Page write failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult> AaiByteWriteAsync(uint address, byte[] data, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (data == null || data.Length == 0)
                    return OperationResult.FailureResult("No data to write");

                // SST AAI byte programming for SST25VF series
                var stopwatch = Stopwatch.StartNew();

                // First byte - full address
                var wenResult = await WriteEnableAsync(cancellationToken);
                if (!wenResult.Success)
                    return wenResult;

                var firstCmd = new byte[] { CMD_SST_AAI_BYTE, (byte)(address >> 16), (byte)(address >> 8), (byte)address, data[0] };
                var result = await _hardware.SpiWriteAsync(firstCmd, cancellationToken);
                if (!result.Success)
                    return result;

                await WaitNotBusyAsync(5000, cancellationToken);

                // Subsequent bytes - data only
                for (var i = 1; i < data.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var nextCmd = new byte[] { CMD_SST_AAI_BYTE, data[i] };
                    result = await _hardware.SpiWriteAsync(nextCmd, cancellationToken);
                    if (!result.Success)
                        return result;

                    await WaitNotBusyAsync(5000, cancellationToken);

                    if (progress != null && i % 256 == 0)
                    {
                        var progressInfo = new ProgressInfo(i, data.Length, $"AAI writing... {stopwatch.Elapsed.TotalSeconds:F1}s");
                        progress.Report(progressInfo);
                        OnProgressChanged(progressInfo);
                    }
                }

                // Write disable to end AAI mode
                await WriteDisableAsync(cancellationToken);

                return OperationResult.SuccessResult($"AAI byte write completed: {data.Length} bytes");
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"AAI byte write failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult> AaiWordWriteAsync(uint address, byte[] data, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (data == null || data.Length == 0)
                    return OperationResult.FailureResult("No data to write");

                if (data.Length % 2 != 0)
                    return OperationResult.FailureResult("AAI word write requires even number of bytes");

                var stopwatch = Stopwatch.StartNew();

                // First word - full address
                var wenResult = await WriteEnableAsync(cancellationToken);
                if (!wenResult.Success)
                    return wenResult;

                var firstCmd = new byte[] { CMD_SST_AAI_WORD, (byte)(address >> 16), (byte)(address >> 8), (byte)address, data[0], data[1] };
                var result = await _hardware.SpiWriteAsync(firstCmd, cancellationToken);
                if (!result.Success)
                    return result;

                await WaitNotBusyAsync(5000, cancellationToken);

                // Subsequent words
                for (var i = 2; i < data.Length; i += 2)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var nextCmd = new byte[] { CMD_SST_AAI_WORD, data[i], data[i + 1] };
                    result = await _hardware.SpiWriteAsync(nextCmd, cancellationToken);
                    if (!result.Success)
                        return result;

                    await WaitNotBusyAsync(5000, cancellationToken);

                    if (progress != null && i % 256 == 0)
                    {
                        var progressInfo = new ProgressInfo(i, data.Length, $"AAI word writing... {stopwatch.Elapsed.TotalSeconds:F1}s");
                        progress.Report(progressInfo);
                        OnProgressChanged(progressInfo);
                    }
                }

                await WriteDisableAsync(cancellationToken);

                return OperationResult.SuccessResult($"AAI word write completed: {data.Length} bytes");
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"AAI word write failed: {ex.Message}", ex);
            }
        }

        #endregion

        #region Erase Operations

        public async Task<OperationResult> EraseChipAsync(IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();

                var wenResult = await WriteEnableAsync(cancellationToken);
                if (!wenResult.Success)
                    return wenResult;

                // Try multiple chip erase commands for compatibility
                await _hardware.SpiSendCommandAsync(CMD_CHIP_ERASE_ATMEL, cancellationToken); // Atmel chips (0x62)
                await _hardware.SpiSendCommandAsync(CMD_CHIP_ERASE_ALT, cancellationToken);   // Old SST chips (0x60)
                await _hardware.SpiSendCommandAsync(CMD_CHIP_ERASE, cancellationToken);       // Standard (0xC7)

                // Chip erase can take a very long time (10-60 seconds)
                var waitResult = await WaitNotBusyAsync(120000, cancellationToken); // 2 minute timeout
                if (!waitResult.Success)
                    return waitResult;

                if (progress != null)
                {
                    var progressInfo = new ProgressInfo(1, 1, $"Chip erase completed in {stopwatch.Elapsed.TotalSeconds:F1}s");
                    progress.Report(progressInfo);
                    OnProgressChanged(progressInfo);
                }

                return OperationResult.SuccessResult($"Chip erased in {stopwatch.Elapsed.TotalSeconds:F1}s");
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"Chip erase failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult> EraseSectorAsync(uint address, CancellationToken cancellationToken = default)
        {
            try
            {
                var wenResult = await WriteEnableAsync(cancellationToken);
                if (!wenResult.Success)
                    return wenResult;

                var cmd = _is4ByteMode
                    ? new byte[] { CMD_SECTOR_ERASE, (byte)(address >> 24), (byte)(address >> 16), (byte)(address >> 8), (byte)address }
                    : new byte[] { CMD_SECTOR_ERASE, (byte)(address >> 16), (byte)(address >> 8), (byte)address };

                var result = await _hardware.SpiWriteAsync(cmd, cancellationToken);
                if (!result.Success)
                    return result;

                var waitResult = await WaitNotBusyAsync(3000, cancellationToken);
                if (!waitResult.Success)
                    return waitResult;

                return OperationResult.SuccessResult($"Erased 4KB sector at 0x{address:X6}");
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"Sector erase failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult> EraseBlock32Async(uint address, CancellationToken cancellationToken = default)
        {
            try
            {
                var wenResult = await WriteEnableAsync(cancellationToken);
                if (!wenResult.Success)
                    return wenResult;

                var cmd = _is4ByteMode
                    ? new byte[] { CMD_BLOCK_ERASE_32K, (byte)(address >> 24), (byte)(address >> 16), (byte)(address >> 8), (byte)address }
                    : new byte[] { CMD_BLOCK_ERASE_32K, (byte)(address >> 16), (byte)(address >> 8), (byte)address };

                var result = await _hardware.SpiWriteAsync(cmd, cancellationToken);
                if (!result.Success)
                    return result;

                var waitResult = await WaitNotBusyAsync(5000, cancellationToken);
                if (!waitResult.Success)
                    return waitResult;

                return OperationResult.SuccessResult($"Erased 32KB block at 0x{address:X6}");
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"32KB block erase failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult> EraseBlock64Async(uint address, CancellationToken cancellationToken = default)
        {
            try
            {
                var wenResult = await WriteEnableAsync(cancellationToken);
                if (!wenResult.Success)
                    return wenResult;

                var cmd = _is4ByteMode
                    ? new byte[] { CMD_BLOCK_ERASE_64K, (byte)(address >> 24), (byte)(address >> 16), (byte)(address >> 8), (byte)address }
                    : new byte[] { CMD_BLOCK_ERASE_64K, (byte)(address >> 16), (byte)(address >> 8), (byte)address };

                var result = await _hardware.SpiWriteAsync(cmd, cancellationToken);
                if (!result.Success)
                    return result;

                var waitResult = await WaitNotBusyAsync(10000, cancellationToken);
                if (!waitResult.Success)
                    return waitResult;

                return OperationResult.SuccessResult($"Erased 64KB block at 0x{address:X6}");
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"64KB block erase failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult> EraseRangeAsync(uint startAddress, uint length, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var address = startAddress;
                var endAddress = startAddress + length;
                uint totalErased = 0;

                while (address < endAddress)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var remaining = endAddress - address;

                    // Use largest possible erase size
                    if (remaining >= 65536 && (address % 65536) == 0)
                    {
                        var result = await EraseBlock64Async(address, cancellationToken);
                        if (!result.Success)
                            return result;
                        address += 65536;
                        totalErased += 65536;
                    }
                    else if (remaining >= 32768 && (address % 32768) == 0)
                    {
                        var result = await EraseBlock32Async(address, cancellationToken);
                        if (!result.Success)
                            return result;
                        address += 32768;
                        totalErased += 32768;
                    }
                    else
                    {
                        var result = await EraseSectorAsync(address, cancellationToken);
                        if (!result.Success)
                            return result;
                        address += 4096;
                        totalErased += 4096;
                    }

                    if (progress != null)
                    {
                        var progressInfo = new ProgressInfo((int)totalErased, (int)length, $"Erasing... {stopwatch.Elapsed.TotalSeconds:F1}s");
                        progress.Report(progressInfo);
                        OnProgressChanged(progressInfo);
                    }
                }

                return OperationResult.SuccessResult($"Erased range 0x{startAddress:X6}-0x{endAddress:X6}");
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"Erase range failed: {ex.Message}", ex);
            }
        }

        #endregion

        #region Status and Control

        public async Task<OperationResult<byte>> ReadStatusRegisterAsync(int registerIndex = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                var cmd = registerIndex switch
                {
                    1 => CMD_READ_STATUS_REG,
                    2 => CMD_READ_STATUS_REG2,
                    3 => CMD_READ_STATUS_REG3,
                    _ => CMD_READ_STATUS_REG
                };

                var result = await _hardware.SpiTransferAsync(new byte[] { cmd }, 1, cancellationToken);
                if (!result.Success || result.Data == null || result.Data.Length == 0)
                    return OperationResult<byte>.FailureResult("Failed to read status register");

                return OperationResult<byte>.SuccessResult(result.Data[0], $"Status register {registerIndex}: 0x{result.Data[0]:X2}");
            }
            catch (Exception ex)
            {
                return OperationResult<byte>.FailureResult($"Read status register failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult> WriteStatusRegisterAsync(byte value, int registerIndex = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                // Some chips (SST) require EWSR command first
                await _hardware.SpiSendCommandAsync(CMD_ENABLE_WRITE_STATUS, cancellationToken);

                var wenResult = await WriteEnableAsync(cancellationToken);
                if (!wenResult.Success)
                    return wenResult;

                var cmd = registerIndex switch
                {
                    1 => CMD_WRITE_STATUS_REG,
                    2 => CMD_WRITE_STATUS_REG2,
                    3 => CMD_WRITE_STATUS_REG3,
                    _ => CMD_WRITE_STATUS_REG
                };

                var result = await _hardware.SpiWriteAsync(new byte[] { cmd, value }, cancellationToken);
                if (!result.Success)
                    return result;

                await WaitNotBusyAsync(5000, cancellationToken);

                return OperationResult.SuccessResult($"Wrote 0x{value:X2} to status register {registerIndex}");
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"Write status register failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult> WaitNotBusyAsync(int timeoutMs = 30000, CancellationToken cancellationToken = default)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();

                while (stopwatch.ElapsedMilliseconds < timeoutMs)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var statusResult = await ReadStatusRegisterAsync(1, cancellationToken);
                    if (!statusResult.Success)
                        return OperationResult.FailureResult("Failed to read status register");

                    if ((statusResult.Data & STATUS_BUSY) == 0)
                        return OperationResult.SuccessResult("Chip ready");

                    await Task.Delay(10, cancellationToken);
                }

                return OperationResult.FailureResult($"Timeout waiting for chip to be ready ({timeoutMs}ms)");
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"Wait not busy failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult> WriteEnableAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _hardware.SpiSendCommandAsync(CMD_WRITE_ENABLE, cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"Write enable failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult> WriteDisableAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _hardware.SpiSendCommandAsync(CMD_WRITE_DISABLE, cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"Write disable failed: {ex.Message}", ex);
            }
        }

        #endregion

        #region Protection

        public async Task<OperationResult<bool>> IsWriteProtectedAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var statusResult = await ReadStatusRegisterAsync(1, cancellationToken);
                if (!statusResult.Success)
                    return OperationResult<bool>.FailureResult("Failed to read status register");

                var isProtected = (statusResult.Data & (STATUS_BP0 | STATUS_BP1 | STATUS_BP2 | STATUS_BP3)) != 0;
                return OperationResult<bool>.SuccessResult(isProtected, $"Write protection: {(isProtected ? "ON" : "OFF")}");
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.FailureResult($"Check write protection failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult> SetBlockProtectionAsync(byte level, CancellationToken cancellationToken = default)
        {
            try
            {
                var statusResult = await ReadStatusRegisterAsync(1, cancellationToken);
                if (!statusResult.Success)
                    return OperationResult.FailureResult("Failed to read current status");

                // Preserve other bits, set BP bits
                var newStatus = (byte)((statusResult.Data & ~(STATUS_BP0 | STATUS_BP1 | STATUS_BP2 | STATUS_BP3)) | (level & 0x0F) << 2);
                
                return await WriteStatusRegisterAsync(newStatus, 1, cancellationToken);
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"Set block protection failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult> ClearBlockProtectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var statusResult = await ReadStatusRegisterAsync(1, cancellationToken);
                if (!statusResult.Success)
                    return OperationResult.FailureResult("Failed to read current status");

                // Clear BP bits
                var newStatus = (byte)(statusResult.Data & ~(STATUS_BP0 | STATUS_BP1 | STATUS_BP2 | STATUS_BP3));
                
                return await WriteStatusRegisterAsync(newStatus, 1, cancellationToken);
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"Clear block protection failed: {ex.Message}", ex);
            }
        }

        #endregion

        #region 4-Byte Addressing

        public async Task<OperationResult> Enter4ByteAddressModeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var wenResult = await WriteEnableAsync(cancellationToken);
                if (!wenResult.Success)
                    return wenResult;

                var result = await _hardware.SpiSendCommandAsync(CMD_ENTER_4BYTE_MODE, cancellationToken);
                if (!result.Success)
                    return result;

                // For Spansion chips, set EXTADD bit
                var extAddrCmd = new byte[] { CMD_WRITE_EXT_ADDR_REG, 0x80 };
                await _hardware.SpiWriteAsync(extAddrCmd, cancellationToken);

                _is4ByteMode = true;
                return OperationResult.SuccessResult("Entered 4-byte address mode");
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"Enter 4-byte mode failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult> Exit4ByteAddressModeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var wenResult = await WriteEnableAsync(cancellationToken);
                if (!wenResult.Success)
                    return wenResult;

                var result = await _hardware.SpiSendCommandAsync(CMD_EXIT_4BYTE_MODE, cancellationToken);
                if (!result.Success)
                    return result;

                _is4ByteMode = false;
                return OperationResult.SuccessResult("Exited 4-byte address mode");
            }
            catch (Exception ex)
            {
                return OperationResult.FailureResult($"Exit 4-byte mode failed: {ex.Message}", ex);
            }
        }

        #endregion

        #region Verification

        public async Task<OperationResult<bool>> VerifyAsync(uint address, byte[] data, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var readResult = await ReadAsync(address, data.Length, progress, cancellationToken);
                if (!readResult.Success)
                    return OperationResult<bool>.FailureResult($"Verify failed: could not read data - {readResult.Message}");

                var matches = readResult.Data.SequenceEqual(data);
                
                if (!matches)
                {
                    // Find first mismatch for debugging
                    for (var i = 0; i < data.Length; i++)
                    {
                        if (readResult.Data[i] != data[i])
                        {
                            return OperationResult<bool>.SuccessResult(false, 
                                $"Verify failed at offset {i} (0x{(address + i):X6}): expected 0x{data[i]:X2}, got 0x{readResult.Data[i]:X2}");
                        }
                    }
                }

                return OperationResult<bool>.SuccessResult(true, "Verification successful");
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.FailureResult($"Verify failed: {ex.Message}", ex);
            }
        }

        public async Task<OperationResult<bool>> IsBlankAsync(uint address, int length, IProgress<ProgressInfo> progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var readResult = await ReadAsync(address, length, progress, cancellationToken);
                if (!readResult.Success)
                    return OperationResult<bool>.FailureResult($"Blank check failed: could not read data - {readResult.Message}");

                var isBlank = readResult.Data.IsBlank();
                
                if (!isBlank)
                {
                    var nonBlankIndex = Array.FindIndex(readResult.Data, b => b != 0xFF);
                    return OperationResult<bool>.SuccessResult(false, 
                        $"Not blank: found 0x{readResult.Data[nonBlankIndex]:X2} at offset {nonBlankIndex} (0x{(address + nonBlankIndex):X6})");
                }

                return OperationResult<bool>.SuccessResult(true, "Range is blank (all 0xFF)");
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.FailureResult($"Blank check failed: {ex.Message}", ex);
            }
        }

        #endregion

        #region IDisposable

        protected virtual void OnProgressChanged(ProgressInfo progressInfo)
        {
            ProgressChanged?.Invoke(this, progressInfo);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Cleanup managed resources
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
