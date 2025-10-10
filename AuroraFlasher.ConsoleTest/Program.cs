using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AuroraFlasher.Hardware;
using AuroraFlasher.Logging;
using AuroraFlasher.Models;
using AuroraFlasher.Services;

namespace AuroraFlasher.ConsoleTest
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  AuroraFlasher Console Test");
            Console.WriteLine("  CH341A + SPI Flash Test");
            Console.WriteLine("========================================");
            Console.WriteLine();

            Logger.Info("==========================================================");
            Logger.Info("AuroraFlasher Console Test Started");
            Logger.Info("==========================================================");

            try
            {
                var service = new ProgrammerService();

                // Step 1: Enumerate hardware
                Console.WriteLine("[1] Enumerating hardware...");
                var enumResult = await service.EnumerateHardwareAsync(HardwareType.CH341);
                if (!enumResult.Success || enumResult.Data == null || enumResult.Data.Length == 0)
                {
                    Console.WriteLine($"   ERROR: {enumResult.Message}");
                    return 1;
                }

                Console.WriteLine($"   Found {enumResult.Data.Length} hardware type(s)");
                var hardware = enumResult.Data[0];
                Console.WriteLine($"   Using: {hardware.Name}");
                Console.WriteLine();

                // Step 2: Enumerate devices
                Console.WriteLine("[2] Scanning for CH341 devices...");
                var devices = await hardware.EnumerateDevicesAsync();
                if (devices == null || devices.Length == 0)
                {
                    Console.WriteLine("   ERROR: No CH341 devices found!");
                    Console.WriteLine("   Please ensure:");
                    Console.WriteLine("   - CH341A programmer is connected to USB");
                    Console.WriteLine("   - CH341 drivers are installed");
                    Console.WriteLine("   - CH341DLL.dll is in application directory");
                    return 1;
                }

                Console.WriteLine($"   Found {devices.Length} device(s):");
                for (int i = 0; i < devices.Length; i++)
                {
                    Console.WriteLine($"   [{i}] {devices[i]}");
                }
                Console.WriteLine();

                // Step 3: Connect to device
                Console.WriteLine("[3] Connecting to device...");
                var connectResult = await service.ConnectAsync(hardware, devices[0]);
                if (!connectResult.Success)
                {
                    Console.WriteLine($"   ERROR: {connectResult.Message}");
                    return 1;
                }
                Console.WriteLine($"   {connectResult.Message}");

                // Get firmware version
                var versionResult = await hardware.GetFirmwareVersionAsync();
                if (versionResult.Success)
                {
                    Console.WriteLine($"   Version: {versionResult.Data}");
                }
                Console.WriteLine();

                // Step 4: Detect chip
                Console.WriteLine("[4] Detecting SPI chip...");
                var detectResult = await service.DetectChipAsync(ProtocolType.SPI);
                if (!detectResult.Success)
                {
                    Console.WriteLine($"   ERROR: {detectResult.Message}");
                    await service.DisconnectAsync();
                    return 1;
                }

                var chip = detectResult.Data;
                Console.WriteLine($"   Chip: {chip.Name}");
                Console.WriteLine($"   Manufacturer: {chip.Manufacturer}");
                Console.WriteLine($"   Size: {chip.SizeKB}KB ({chip.SizeMB:F2}MB)");
                Console.WriteLine($"   Page Size: {chip.PageSize} bytes");
                Console.WriteLine($"   Sector Size: {chip.SectorSize} bytes");
                Console.WriteLine($"   Block Size: {chip.BlockSize} bytes");
                Console.WriteLine($"   Manufacturer ID: 0x{chip.ManufacturerId:X2}");
                Console.WriteLine($"   Device ID: 0x{chip.DeviceId:X4}");
                Console.WriteLine();

                // Step 5: Read first 256 bytes
                Console.WriteLine("[5] Reading first 256 bytes...");
                var readResult = await service.ReadMemoryAsync(0x000000, 256);
                if (!readResult.Success)
                {
                    Console.WriteLine($"   ERROR: {readResult.Message}");
                    await service.DisconnectAsync();
                    return 1;
                }

                Console.WriteLine($"   {readResult.Message}");
                Console.WriteLine();
                Console.WriteLine("   Hex Dump:");
                Console.WriteLine(ToHexDump(readResult.Data));
                Console.WriteLine();

                // Step 6: Check if blank
                Console.WriteLine("[6] Checking if first 4KB is blank...");
                var blankResult = await service.BlankCheckAsync(0x000000, 4096);
                if (blankResult.Success)
                {
                    Console.WriteLine($"   {blankResult.Message}");
                }
                Console.WriteLine();

                // Step 7: Read a different section (e.g., 0x1000)
                Console.WriteLine("[7] Reading 256 bytes from address 0x001000...");
                var readResult2 = await service.ReadMemoryAsync(0x001000, 256);
                if (readResult2.Success)
                {
                    Console.WriteLine($"   {readResult2.Message}");
                    Console.WriteLine();
                    Console.WriteLine("   Hex Dump:");
                    Console.WriteLine(ToHexDump(readResult2.Data));
                }
                Console.WriteLine();

                // Step 8: Disconnect
                Console.WriteLine("[8] Disconnecting...");
                var disconnectResult = await service.DisconnectAsync();
                Console.WriteLine($"   {disconnectResult.Message}");
                Console.WriteLine();

                Console.WriteLine("========================================");
                Console.WriteLine("  Test completed successfully!");
                Console.WriteLine("========================================");
                Console.WriteLine();
                
                Logger.Info("==========================================================");
                Logger.Info("AuroraFlasher Console Test Completed Successfully");
                Logger.Info("==========================================================");
                
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("========================================");
                Console.WriteLine("  FATAL ERROR");
                Console.WriteLine("========================================");
                Console.WriteLine($"Exception: {ex.GetType().Name}");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"Stack Trace:");
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine();
                
                Logger.Fatal(ex, "Unhandled exception in console test");
                Logger.Info("==========================================================");
                Logger.Info("AuroraFlasher Console Test Ended with Error");
                Logger.Info("==========================================================");
                Logger.Flush();
                
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return 1;
            }
            finally
            {
                Logger.Flush();
                Logger.Shutdown();
            }
        }

        /// <summary>
        /// Convert byte array to hex dump format
        /// </summary>
        static string ToHexDump(byte[] data, int bytesPerLine = 16)
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            var sb = new StringBuilder();
            for (int i = 0; i < data.Length; i += bytesPerLine)
            {
                // Address
                sb.Append($"   {i:X4}:  ");

                // Hex bytes
                int lineLength = Math.Min(bytesPerLine, data.Length - i);
                for (int j = 0; j < bytesPerLine; j++)
                {
                    if (j < lineLength)
                        sb.Append($"{data[i + j]:X2} ");
                    else
                        sb.Append("   ");

                    if (j == 7)
                        sb.Append(" ");
                }

                // ASCII representation
                sb.Append("  ");
                for (int j = 0; j < lineLength; j++)
                {
                    byte b = data[i + j];
                    sb.Append((b >= 32 && b < 127) ? (char)b : '.');
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
