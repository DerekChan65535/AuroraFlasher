using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AuroraFlasher.NativeInterop
{
    /// <summary>
    /// P/Invoke wrapper for CH341DLL.dll
    /// CH341/CH341A USB interface chip for Parallel, I2C, SPI, JTAG
    /// </summary>
    public static class CH341Dll
    {
        private const string DllName = "CH341DLL.dll";

        #region Constants

        // CH341 packet lengths
        public const int CH341_PACKET_LENGTH = 32;
        public const int CH341_PKT_LEN_SHORT = 8;

        // CH341 endpoint addresses
        public const byte CH341_ENDP_INTER_UP = 0x81;
        public const byte CH341_ENDP_INTER_DOWN = 0x01;
        public const byte CH341_ENDP_DATA_UP = 0x82;
        public const byte CH341_ENDP_DATA_DOWN = 0x02;

        // CH341 parallel mode
        public const uint CH341_PARA_MODE_EPP = 0x00;    // EPP mode
        public const uint CH341_PARA_MODE_EPP17 = 0x00;  // EPP mode V1.7
        public const uint CH341_PARA_MODE_EPP19 = 0x01;  // EPP mode V1.9
        public const uint CH341_PARA_MODE_MEM = 0x02;    // MEM mode

        // CH341A command codes
        public const byte CH341A_CMD_SET_OUTPUT = 0xA1;  // Set parallel output
        public const byte CH341A_CMD_IO_ADDR = 0xA2;     // MEM address read/write
        public const byte CH341A_CMD_SPI_STREAM = 0xA8;  // SPI interface command
        public const byte CH341A_CMD_SIO_STREAM = 0xA9;  // SIO interface command
        public const byte CH341A_CMD_I2C_STREAM = 0xAA;  // I2C interface command
        public const byte CH341A_CMD_UIO_STREAM = 0xAB;  // UIO interface command

        // CH341A control transfer vendor commands
        public const byte CH341A_BUF_CLEAR = 0xB2;       // Clear unfinished data
        public const byte CH341A_I2C_CMD_X = 0x54;       // I2C command immediate execute
        public const byte CH341A_DELAY_MS = 0x5E;        // Delay in milliseconds
        public const byte CH341A_GET_VER = 0x5F;         // Get chip version

        // Status bit definitions
        public const uint StateBitERR = 0x00000100;      // ERR# pin state
        public const uint StateBitPEMP = 0x00000200;     // PEMP pin state
        public const uint StateBitINT = 0x00000400;      // INT# pin state
        public const uint StateBitSLCT = 0x00000800;     // SLCT pin state
        public const uint StateBitSDA = 0x00800000;      // SDA pin state

        // I2C command stream codes
        public const byte CH341A_CMD_I2C_STM_STA = 0x74; // Generate START bit
        public const byte CH341A_CMD_I2C_STM_STO = 0x75; // Generate STOP bit
        public const byte CH341A_CMD_I2C_STM_OUT = 0x80; // Output data
        public const byte CH341A_CMD_I2C_STM_IN = 0xC0;  // Input data
        public const byte CH341A_CMD_I2C_STM_SET = 0x60; // Set parameters
        public const byte CH341A_CMD_I2C_STM_END = 0x00; // End command

        #endregion

        #region Basic Device Operations

        /// <summary>
        /// Open CH341 device
        /// </summary>
        /// <param name="iIndex">Device index, 0 = first device</param>
        /// <returns>Device handle, or negative value on error</returns>
        [DllImport(DllName, SetLastError = true)]
        public static extern int CH341OpenDevice(uint iIndex);

        /// <summary>
        /// Close CH341 device
        /// </summary>
        /// <param name="iIndex">Device index</param>
        [DllImport(DllName)]
        public static extern void CH341CloseDevice(uint iIndex);

        /// <summary>
        /// Get DLL version
        /// </summary>
        /// <returns>Version number</returns>
        [DllImport(DllName)]
        public static extern uint CH341GetVersion();

        /// <summary>
        /// Get driver version
        /// </summary>
        /// <returns>Driver version, or 0 on error</returns>
        [DllImport(DllName)]
        public static extern uint CH341GetDrvVersion();

        /// <summary>
        /// Reset USB device
        /// </summary>
        /// <param name="iIndex">Device index</param>
        /// <returns>True on success</returns>
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH341ResetDevice(uint iIndex);

        /// <summary>
        /// Get CH341 chip version
        /// </summary>
        /// <param name="iIndex">Device index</param>
        /// <returns>0 = invalid, 0x10 = CH341, 0x20 = CH341A</returns>
        [DllImport(DllName)]
        public static extern uint CH341GetVerIC(uint iIndex);

        /// <summary>
        /// Flush CH341 buffer
        /// </summary>
        /// <param name="iIndex">Device index</param>
        /// <returns>True on success</returns>
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH341FlushBuffer(uint iIndex);

        #endregion

        #region Parallel Port Operations

        /// <summary>
        /// Set parallel port mode
        /// </summary>
        /// <param name="iIndex">Device index</param>
        /// <param name="iMode">Mode: 0=EPP, 1=EPP V1.9, 2=MEM</param>
        /// <returns>True on success</returns>
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH341SetParaMode(uint iIndex, uint iMode);

        /// <summary>
        /// Initialize parallel port with RST# pulse
        /// </summary>
        /// <param name="iIndex">Device index</param>
        /// <param name="iMode">Mode: 0=EPP, 1=EPP V1.9, 2=MEM, >= 0x100 = keep current mode</param>
        /// <returns>True on success</returns>
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH341InitParallel(uint iIndex, uint iMode);

        /// <summary>
        /// Get port status
        /// </summary>
        /// <param name="iIndex">Device index</param>
        /// <param name="iStatus">Pointer to status DWORD</param>
        /// <returns>True on success</returns>
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH341GetStatus(uint iIndex, ref uint iStatus);

        /// <summary>
        /// Set output pins
        /// </summary>
        /// <param name="iIndex">Device index</param>
        /// <param name="iEnable">Enable bits (which outputs to set)</param>
        /// <param name="iSetDirOut">Direction bits (0=input, 1=output)</param>
        /// <param name="iSetDataOut">Data bits</param>
        /// <returns>True on success</returns>
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH341SetOutput(uint iIndex, uint iEnable, uint iSetDirOut, uint iSetDataOut);

        #endregion

        #region SPI Operations

        /// <summary>
        /// Set stream mode (I2C/SPI configuration)
        /// </summary>
        /// <param name="iIndex">Device index</param>
        /// <param name="iMode">Mode settings (bits 1-0: I2C speed, bit 2: SPI I/O, bit 7: SPI bit order)</param>
        /// <returns>True on success</returns>
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH341SetStream(uint iIndex, uint iMode);

        /// <summary>
        /// 4-wire SPI stream (CLK/D3, DOUT/D5, DIN/D7, CS/D0-D2)
        /// SPI Mode 0: CPOL=0, CPHA=0, ~68KB/s
        /// </summary>
        /// <param name="iIndex">Device index</param>
        /// <param name="iChipSelect">CS control (bit 7: enable, bits 1-0: D0/D1/D2 selection)</param>
        /// <param name="iLength">Data length</param>
        /// <param name="ioBuffer">Buffer for write data (input) and read data (output)</param>
        /// <returns>True on success</returns>
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH341StreamSPI4(uint iIndex, uint iChipSelect, uint iLength, byte[] ioBuffer);

        /// <summary>
        /// 5-wire SPI stream (dual I/O mode)
        /// </summary>
        /// <param name="iIndex">Device index</param>
        /// <param name="iChipSelect">CS control</param>
        /// <param name="iLength">Data length</param>
        /// <param name="ioBuffer">First buffer (DOUT/DIN)</param>
        /// <param name="ioBuffer2">Second buffer (DOUT2/DIN2)</param>
        /// <returns>True on success</returns>
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH341StreamSPI5(uint iIndex, uint iChipSelect, uint iLength, byte[] ioBuffer, byte[] ioBuffer2);

        #endregion

        #region I2C Operations

        /// <summary>
        /// I2C stream transfer
        /// </summary>
        /// <param name="iIndex">Device index</param>
        /// <param name="iWriteLength">Number of bytes to write</param>
        /// <param name="iWriteBuffer">Write buffer (first byte is usually device address + R/W bit)</param>
        /// <param name="iReadLength">Number of bytes to read</param>
        /// <param name="oReadBuffer">Read buffer</param>
        /// <returns>True on success</returns>
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH341StreamI2C(uint iIndex, uint iWriteLength, byte[] iWriteBuffer, uint iReadLength, byte[] oReadBuffer);

        /// <summary>
        /// Read one byte from I2C EEPROM
        /// </summary>
        /// <param name="iIndex">Device index</param>
        /// <param name="iDevice">I2C device address (7-bit)</param>
        /// <param name="iAddr">Memory address</param>
        /// <param name="oByte">Output byte</param>
        /// <returns>True on success</returns>
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH341ReadI2C(uint iIndex, byte iDevice, byte iAddr, byte[] oByte);

        /// <summary>
        /// Write one byte to I2C EEPROM
        /// </summary>
        /// <param name="iIndex">Device index</param>
        /// <param name="iDevice">I2C device address (7-bit)</param>
        /// <param name="iAddr">Memory address</param>
        /// <param name="iByte">Byte to write</param>
        /// <returns>True on success</returns>
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH341WriteI2C(uint iIndex, byte iDevice, byte iAddr, byte iByte);

        #endregion

        #region Data Transfer

        /// <summary>
        /// Read data block
        /// </summary>
        /// <param name="iIndex">Device index</param>
        /// <param name="oBuffer">Output buffer</param>
        /// <param name="ioLength">Length pointer (input: bytes to read, output: bytes actually read)</param>
        /// <returns>True on success</returns>
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH341ReadData(uint iIndex, byte[] oBuffer, ref uint ioLength);

        /// <summary>
        /// Write data block
        /// </summary>
        /// <param name="iIndex">Device index</param>
        /// <param name="iBuffer">Input buffer</param>
        /// <param name="ioLength">Length pointer (input: bytes to write, output: bytes actually written)</param>
        /// <returns>True on success</returns>
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH341WriteData(uint iIndex, byte[] iBuffer, ref uint ioLength);

        /// <summary>
        /// Write then read (stream command execution)
        /// </summary>
        /// <param name="iIndex">Device index</param>
        /// <param name="iWriteLength">Write length</param>
        /// <param name="iWriteBuffer">Write buffer</param>
        /// <param name="iReadStep">Read step size</param>
        /// <param name="iReadTimes">Read times (total read = iReadStep * iReadTimes)</param>
        /// <param name="oReadLength">Actual read length</param>
        /// <param name="oReadBuffer">Read buffer</param>
        /// <returns>True on success</returns>
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH341WriteRead(uint iIndex, uint iWriteLength, byte[] iWriteBuffer, 
            uint iReadStep, uint iReadTimes, ref uint oReadLength, byte[] oReadBuffer);

        #endregion

        #region Timeout and Buffer Control

        /// <summary>
        /// Set USB timeout
        /// </summary>
        /// <param name="iIndex">Device index</param>
        /// <param name="iWriteTimeout">Write timeout in ms (0xFFFFFFFF = no timeout)</param>
        /// <param name="iReadTimeout">Read timeout in ms (0xFFFFFFFF = no timeout)</param>
        /// <returns>True on success</returns>
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH341SetTimeout(uint iIndex, uint iWriteTimeout, uint iReadTimeout);

        /// <summary>
        /// Set hardware async delay (delays before next stream operation)
        /// </summary>
        /// <param name="iIndex">Device index</param>
        /// <param name="iDelay">Delay in milliseconds</param>
        /// <returns>True on success</returns>
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH341SetDelaymS(uint iIndex, uint iDelay);

        #endregion

        #region Helper Methods

        /// <summary>
        /// Check if device is CH341A (vs original CH341)
        /// </summary>
        public static bool IsCH341A(uint iIndex)
        {
            var version = CH341GetVerIC(iIndex);
            return version == 0x20; // 0x20 = CH341A, 0x10 = CH341
        }

        /// <summary>
        /// Build chip select parameter for SPI functions
        /// </summary>
        /// <param name="csPin">CS pin: 0=D0, 1=D1, 2=D2</param>
        /// <param name="enable">Enable CS control</param>
        /// <returns>Chip select parameter</returns>
        public static uint MakeChipSelect(int csPin, bool enable = true)
        {
            if (!enable)
                return 0x00; // Bit 7 = 0: ignore CS

            return 0x80 | ((uint)csPin & 0x03); // Bit 7 = 1: enable, bits 1-0 = pin
        }

        /// <summary>
        /// Build stream mode parameter
        /// </summary>
        /// <param name="i2cSpeed">I2C speed: 0=20kHz, 1=100kHz, 2=400kHz, 3=750kHz</param>
        /// <param name="spiDualIO">SPI dual I/O mode</param>
        /// <param name="spiMsbFirst">SPI MSB first (vs LSB first)</param>
        /// <returns>Stream mode parameter</returns>
        public static uint MakeStreamMode(int i2cSpeed = 1, bool spiDualIO = false, bool spiMsbFirst = true)
        {
            var mode = (uint)(i2cSpeed & 0x03); // Bits 1-0: I2C speed
            if (spiDualIO)
                mode |= 0x04; // Bit 2: SPI dual I/O
            if (spiMsbFirst)
                mode |= 0x80; // Bit 7: MSB first
            return mode;
        }

        #endregion
    }
}
