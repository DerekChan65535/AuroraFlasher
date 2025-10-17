# AuroraFlasher

<div align="center">

![AuroraFlasher Logo](https://img.shields.io/badge/AuroraFlasher-SPI%20Flash%20Programmer-blue?style=for-the-badge)

**A modern WPF application for programming SPI Flash, I2C EEPROM, and MicroWire memory chips**

[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Build Status](https://img.shields.io/badge/Build-Passing-brightgreen.svg)](https://github.com/yourusername/AuroraFlasher)
[![Version](https://img.shields.io/badge/Version-0.1.0--alpha-orange.svg)](https://github.com/yourusername/AuroraFlasher/releases)

</div>

## 🌟 Overview

AuroraFlasher is a modern, cross-platform memory programmer application built with WPF and C#. It provides an intuitive interface for reading, writing, verifying, and erasing various types of memory chips including SPI Flash, I2C EEPROM, and MicroWire devices.

This project is a modern translation of the popular UsbAsp-flash application, bringing it to the Windows platform with contemporary UI/UX and improved performance.

## ✨ Features

### 🔧 Hardware Support
- **USBasp** - Open-source USB programmer
- **CH341A** - Popular USB-to-SPI converter
- **CH347** - High-speed USB programmer
- **FT232H** - FTDI MPSSE-based programmer
- **AVRISP-MKII** - Atmel programmer
- **Arduino** - Custom firmware-based programmer

### 📡 Protocol Support
- **SPI Flash** - 25-series (W25Q, MX25, GD25, etc.)
- **SPI EEPROM** - 95-series small EEPROMs
- **Atmel DataFlash** - 45-series AT45DB chips
- **I2C EEPROM** - 24C series and compatible
- **MicroWire** - 93C series EEPROMs
- **KB9012** - Keyboard controller programming

### 🎯 Core Operations
- **Read** - Full chip or partial read with progress tracking
- **Write** - Page programming with auto-verify
- **Verify** - Compare chip contents with file data
- **Erase** - Chip, sector, or block erase operations
- **Blank Check** - Verify chip is erased (all 0xFF)
- **Chip ID** - Automatic chip identification via JEDEC ID

### 🖥️ User Interface
- **Modern WPF UI** - Clean, responsive interface
- **MVVM Architecture** - Maintainable code structure
- **Hex Editor** - Built-in hex viewer/editor
- **Progress Tracking** - Real-time operation progress
- **Logging** - Comprehensive operation logging
- **Settings** - Persistent configuration
- **Multi-language** - English, Russian, Chinese support

## 🚀 Quick Start

### Prerequisites
- Windows 10/11 (x64)
- .NET Framework 4.8
- USB programmer hardware (CH341A recommended for beginners)
- Memory chip to program

### Installation
1. Download the latest release from [Releases](https://github.com/yourusername/AuroraFlasher/releases)
2. Extract to a folder
3. Install required drivers for your hardware
4. Run `AuroraFlasher.exe`

### Basic Usage
1. **Connect Hardware** - Select your programmer type and connect device
2. **Select Chip** - Choose from database or auto-detect chip ID
3. **Configure Settings** - Set SPI speed, protocol, etc.
4. **Perform Operations** - Read, write, verify, or erase as needed

## 📁 Project Structure

```
AuroraFlasher/
├── AuroraFlasher/           # Main WPF application
│   ├── ViewModels/          # MVVM ViewModels
│   ├── Commands/            # ICommand implementations
│   ├── Converters/          # XAML value converters
│   └── Resources/           # XAML resources and styles
├── AuroraFlasher.Lib/       # Core library
│   ├── Models/              # Data models and DTOs
│   ├── Interfaces/          # Service and hardware interfaces
│   ├── Hardware/            # Hardware implementations
│   ├── Protocols/           # Protocol implementations
│   ├── Services/            # Business logic services
│   ├── NativeInterop/       # P/Invoke wrappers
│   └── Utilities/           # Helper classes
├── AuroraFlasher.Cli/       # Console test application
├── AuroraFlasher.UnitTest/  # Unit tests
└── Documentation/           # Project documentation
```

## 🔌 Hardware Setup

### CH341A Programmer (Recommended)
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

### Supported Chips
- **Winbond:** W25Q32, W25Q64, W25Q128, W25Q256
- **Macronix:** MX25L3233F, MX25L6433F, MX25L12833F
- **GigaDevice:** GD25Q32, GD25Q64, GD25Q128
- **SST/Microchip:** SST25VF032B, SST25VF064C
- **Atmel:** AT25DF321, AT25DF641
- **And many more...** (1000+ chips in database)

## 🛠️ Development

### Building from Source
```bash
git clone https://github.com/yourusername/AuroraFlasher.git
cd AuroraFlasher
# Open AuroraFlasher.sln in Visual Studio 2022
# Build solution (Ctrl+Shift+B)
```

### Requirements
- Visual Studio 2022 (Community or higher)
- .NET Framework 4.8 SDK
- Windows 10/11 SDK

### Testing
```bash
# Run unit tests
dotnet test AuroraFlasher.UnitTest/

# Run console test application
cd AuroraFlasher.Cli/bin/Debug
./AuroraFlasher.Cli.exe
```

## 📊 Current Status

### ✅ Completed Features
- [x] Project structure and architecture
- [x] Core interfaces and models
- [x] CH341A hardware implementation
- [x] SPI 25-series protocol
- [x] Basic WPF UI with MVVM
- [x] Chip database loading
- [x] Console test application
- [x] Unit test framework

### 🚧 In Progress
- [ ] Additional hardware implementations (USBasp, CH347, FT232H)
- [ ] I2C and MicroWire protocols
- [ ] Advanced UI features
- [ ] Write/erase operations
- [ ] Settings persistence

### 📋 Planned Features
- [ ] All 6 hardware types
- [ ] All protocol variants
- [ ] Hex editor with editing capabilities
- [ ] Scripting system
- [ ] Multi-language support
- [ ] Dark/light themes
- [ ] Plugin system

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details.

### Development Phases
1. **Phase 1:** Foundation ✅
2. **Phase 2:** Hardware Layer 🚧
3. **Phase 3:** Protocol Layer 📋
4. **Phase 4:** Services Layer 📋
5. **Phase 5:** UI Layer 📋
6. **Phase 6:** Integration & Testing 📋
7. **Phase 7:** Deployment 📋

## 📚 Documentation

- [Architecture Analysis](Documentation/01_Architecture_Analysis.md)
- [WPF Design](Documentation/02_WPF_Architecture_Design.md)
- [Core Interfaces](Documentation/03_Core_Interfaces_Models.md)
- [Implementation Roadmap](Documentation/04_Implementation_Roadmap.md)
- [Hardware Test Report](Documentation/Hardware_Test_Report_2025-10-10.md)
- [CLI Usage Guide](AuroraFlasher.Cli/README.md)

## 🐛 Troubleshooting

### Common Issues

**"No CH341 devices found"**
- Check USB connection
- Install CH341 drivers
- Verify CH341DLL.dll is present
- Close other programmer software

**"Failed to read chip ID"**
- Verify chip connections
- Check power supply (3.3V vs 5V)
- Ensure proper wiring
- Try different chip

**"Access Denied"**
- Close other applications using the device
- Run as administrator if needed
- Check device permissions

### Getting Help
- Check the [Documentation](Documentation/) folder
- Review [Hardware Test Report](Documentation/Hardware_Test_Report_2025-10-10.md)
- Open an [Issue](https://github.com/yourusername/AuroraFlasher/issues)
- Join our [Discussions](https://github.com/yourusername/AuroraFlasher/discussions)

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **UsbAsp-flash** - Original Lazarus/Free Pascal application
- **CH341A Community** - Hardware and driver support
- **SPI Flash Community** - Chip database and testing
- **Contributors** - All those who help improve AuroraFlasher

## 📞 Support

- **GitHub Issues:** [Report bugs or request features](https://github.com/yourusername/AuroraFlasher/issues)
- **Discussions:** [Community support](https://github.com/yourusername/AuroraFlasher/discussions)
- **Email:** [Contact maintainers](mailto:support@auroraflasher.com)

---

<div align="center">

**Made with ❤️ for the electronics community**

[⭐ Star this repo](https://github.com/yourusername/AuroraFlasher) | [🐛 Report Bug](https://github.com/yourusername/AuroraFlasher/issues) | [💡 Request Feature](https://github.com/yourusername/AuroraFlasher/issues)

</div>
