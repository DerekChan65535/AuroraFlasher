# AuroraFlasher Console Test

Simple console application to test CH341A programmer with SPI flash chips.

## Prerequisites

1. **CH341A Programmer** - USB programmer device
2. **CH341DLL.dll** - Must be in the same directory as the executable
3. **SPI Flash Chip** - W25Q32, W25Q64, MX25L3233F, or compatible
4. **CH341 Drivers** - Install from CH341 driver package

## CH341DLL.dll Location

The native CH341DLL.dll must be placed in one of these locations:
- Same directory as AuroraFlasher.ConsoleTest.exe
- System32 directory (not recommended)
- Any directory in PATH environment variable

**Download:** CH341DLL.dll is typically included with CH341 driver package.

## How to Run

### From Visual Studio
1. Copy CH341DLL.dll to `bin\Debug` or `bin\Release`
2. Connect CH341A programmer to USB
3. Connect SPI flash chip to CH341A (check pin diagram)
4. Set AuroraFlasher.ConsoleTest as startup project
5. Press F5 to run

### From Command Line
```powershell
cd AuroraFlasher.ConsoleTest\bin\Debug
.\AuroraFlasher.ConsoleTest.exe
```

## Test Sequence

The application will automatically perform these tests:

1. **Enumerate Hardware** - Find CH341 hardware type
2. **Scan Devices** - Detect connected CH341 devices
3. **Connect** - Open device and get firmware version
4. **Detect Chip** - Read JEDEC ID and identify chip
5. **Read Data** - Read first 256 bytes and display hex dump
6. **Blank Check** - Check if first 4KB is erased (all 0xFF)
7. **Read Address** - Read 256 bytes from 0x1000
8. **Disconnect** - Clean shutdown

## Expected Output

```
========================================
  AuroraFlasher Console Test
  CH341A + SPI Flash Test
========================================

[1] Enumerating hardware...
   Found 1 hardware type(s)
   Using: CH341A USB Programmer

[2] Scanning for CH341 devices...
   Found 1 device(s):
   [0] CH341A Device #0

[3] Connecting to device...
   Opened CH341A USB Programmer (device #0)
   Version: DLL: 3.5.0, Driver: 3.5.0, Chip: CH341A

[4] Detecting SPI chip...
   Chip: W25Q64
   Manufacturer: Winbond
   Size: 8192KB (8.00MB)
   Page Size: 256 bytes
   Sector Size: 4096 bytes
   Block Size: 65536 bytes
   JEDEC ID: EF4017

[5] Reading first 256 bytes...
   Read 256 bytes from 0x000000

   Hex Dump:
   000000: EB 3C 90 4D 53 44 4F 53 35 2E 30 00 02 01 01 00  ë<.MSDOS5.0.....
   000010: 02 E0 00 40 0B F0 09 00 12 00 02 00 00 00 00 00  .à.@.ð..........
   ...

[6] Checking if first 4KB is blank...
   Not blank: found 0xEB at offset 0 (0x000000)

[7] Reading 256 bytes from address 0x001000...
   Read 256 bytes from 0x001000

   Hex Dump:
   001000: FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF  ................
   ...

[8] Disconnecting...
   CH341 device closed

========================================
  Test completed successfully!
========================================
```

## Troubleshooting

### Error: "No CH341 devices found"
**Possible causes:**
- CH341A not connected to USB
- CH341 drivers not installed
- CH341DLL.dll missing or wrong version
- Device in use by another application

**Solutions:**
1. Check USB cable connection
2. Install CH341 drivers from manufacturer
3. Verify CH341DLL.dll is in application directory
4. Close any other programmer software
5. Try different USB port

### Error: "Failed to read chip ID"
**Possible causes:**
- SPI chip not connected properly
- Wrong wiring
- Chip not powered
- Unsupported chip type

**Solutions:**
1. Verify chip connections (CS, CLK, MISO, MOSI, GND, VCC)
2. Check pin diagram for your CH341A model
3. Ensure 3.3V power jumper is set correctly
4. Try different SPI flash chip

### Error: "Access Denied" or "Device in use"
**Cause:** Another application is using the CH341 device

**Solution:** Close other programmer software (AsProgrammer, flashrom, etc.)

## CH341A Wiring

### Standard SPI Connection
```
CH341A Pin  →  SPI Chip Pin
--------------------------------
CS  (D0)    →  CS  (Pin 1)
MISO        →  DO  (Pin 2)
NC          →  WP  (Pin 3) - Pull to VCC
GND         →  GND (Pin 4)
MOSI        →  DI  (Pin 5)
SCK         →  CLK (Pin 6)
NC          →  HOLD(Pin 7) - Pull to VCC
VCC (3.3V)  →  VCC (Pin 8)
```

**Important:** Most SPI flash chips require 3.3V, not 5V!

## Supported Chips (Hardcoded Database)

Currently supports 11 common chips:

**Winbond:**
- W25Q32 (4MB) - JEDEC: EF4016
- W25Q64 (8MB) - JEDEC: EF4017
- W25Q128 (16MB) - JEDEC: EF4018
- W25Q256 (32MB) - JEDEC: EF4019

**Macronix:**
- MX25L3233F (4MB) - JEDEC: C22016
- MX25L6433F (8MB) - JEDEC: C22017

**GigaDevice:**
- GD25Q32 (4MB) - JEDEC: C84016
- GD25Q64 (8MB) - JEDEC: C84017

**SST/Microchip:**
- SST25VF032B (4MB) - JEDEC: BF254A

**Atmel/Microchip:**
- AT25DF321 (4MB) - JEDEC: 1F4700

**Note:** Unlisted chips will show as "Unknown SPI Chip" but may still work for basic operations.

## Exit Codes

- **0** - Test completed successfully
- **1** - Test failed (see error message)

## Limitations (Minimal Version)

This is a **minimal working version** with these limitations:

- Only CH341/CH341A hardware supported
- Only SPI 25-series chips supported
- Read operations only (no write/erase testing)
- Hardcoded chip database (11 chips)
- No configuration options
- No logging to file

Full features will be available in complete version after hardware validation.

## Next Steps

After successful hardware testing:
1. Report any issues or incompatibilities
2. Test with additional chip types
3. Proceed to WPF UI implementation
4. Add write/erase testing
5. Expand chip database

## Support

For issues or questions:
- Check Documentation folder for detailed reports
- Review Phase2A_Completion_Report.md for implementation details
- Verify CH341 driver installation
- Test with known-good SPI flash chip

## License

Part of AuroraFlasher project - see main LICENSE file.
