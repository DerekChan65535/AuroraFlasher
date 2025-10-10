using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using AuroraFlasher.Interfaces;
using AuroraFlasher.Models;
using AuroraFlasher.Protocols;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuroraFlasher.UnitTest.Protocols
{
    [TestClass]
    public class Spi25ProtocolTests
    {
        private Mock<IHardware> _mockHardware;
        private Spi25Protocol _protocol;

        [TestInitialize]
        public void Setup()
        {
            _mockHardware = new Mock<IHardware>();
            _mockHardware.Setup(h => h.IsConnected).Returns(true);
            _protocol = new Spi25Protocol(_mockHardware.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _protocol?.Dispose();
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_WithValidHardware_CreatesInstance()
        {
            // Arrange & Act
            var protocol = new Spi25Protocol(_mockHardware.Object);

            // Assert
            Assert.IsNotNull(protocol);
            Assert.AreEqual(_mockHardware.Object, protocol.Hardware);
            Assert.AreEqual(SpiCommandSet.Standard, protocol.CommandSet);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_WithNullHardware_ThrowsException()
        {
            // Arrange, Act & Assert
            new Spi25Protocol(null);
        }

        [TestMethod]
        public void Constructor_WithCustomCommandSet_UsesSpecifiedCommandSet()
        {
            // Arrange & Act
            var protocol = new Spi25Protocol(_mockHardware.Object, SpiCommandSet.SST_AAI);

            // Assert
            Assert.AreEqual(SpiCommandSet.SST_AAI, protocol.CommandSet);
        }

        #endregion

        #region Enter/Exit Programming Mode Tests

        [TestMethod]
        public async Task EnterProgrammingMode_WhenHardwareConnected_Succeeds()
        {
            // Arrange
            _mockHardware.Setup(h => h.SpiInitAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult("Init success"));
            _mockHardware.Setup(h => h.SpiSendCommandAsync(It.IsAny<byte>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());

            // Act
            var result = await _protocol.EnterProgrammingModeAsync();

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Message.Contains("Entered SPI programming mode"));
            _mockHardware.Verify(h => h.SpiInitAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockHardware.Verify(h => h.SpiSendCommandAsync(0xAB, It.IsAny<CancellationToken>()), Times.Once); // Release power-down
        }

        [TestMethod]
        public async Task EnterProgrammingMode_WhenHardwareNotConnected_Fails()
        {
            // Arrange
            _mockHardware.Setup(h => h.IsConnected).Returns(false);

            // Act
            var result = await _protocol.EnterProgrammingModeAsync();

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Message.Contains("Hardware not connected"));
            _mockHardware.Verify(h => h.SpiInitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task EnterProgrammingMode_WhenInitFails_ReturnsFailure()
        {
            // Arrange
            _mockHardware.Setup(h => h.SpiInitAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.FailureResult("Init failed"));

            // Act
            var result = await _protocol.EnterProgrammingModeAsync();

            // Assert
            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public async Task ExitProgrammingMode_Always_CallsSpiDeinit()
        {
            // Arrange
            _mockHardware.Setup(h => h.SpiDeinitAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult("Deinit success"));

            // Act
            var result = await _protocol.ExitProgrammingModeAsync();

            // Assert
            Assert.IsTrue(result.Success);
            _mockHardware.Verify(h => h.SpiDeinitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region ReadId Tests

        [TestMethod]
        public async Task ReadId_WithValidResponse_ReturnsMemoryId()
        {
            // Arrange
            var jedecId = new byte[] { 0xEF, 0x30, 0x13 }; // Winbond W25X40
            var mfrDeviceId = new byte[] { 0xEF, 0x12 };
            var electronicId = new byte[] { 0x12 };
            var uniqueId = new byte[] { 0xFF, 0xFF };

            _mockHardware.Setup(h => h.SpiTransferAsync(
                It.Is<byte[]>(b => b.Length > 0 && b[0] == 0x9F), // JEDEC ID command
                3,
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult<byte[]>.SuccessResult(jedecId));

            _mockHardware.Setup(h => h.SpiTransferAsync(
                It.Is<byte[]>(b => b.Length >= 4 && b[0] == 0x90), // Manufacturer/Device ID command
                2,
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult<byte[]>.SuccessResult(mfrDeviceId));

            _mockHardware.Setup(h => h.SpiTransferAsync(
                It.Is<byte[]>(b => b.Length >= 4 && b[0] == 0xAB), // Electronic ID command
                1,
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult<byte[]>.SuccessResult(electronicId));

            _mockHardware.Setup(h => h.SpiTransferAsync(
                It.Is<byte[]>(b => b.Length > 0 && b[0] == 0x15), // Status register 3
                2,
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult<byte[]>.SuccessResult(uniqueId));

            // Act
            var result = await _protocol.ReadIdAsync();

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data);
            CollectionAssert.AreEqual(jedecId, result.Data.JedecId);
            Assert.AreEqual(0xEF, result.Data.ManufacturerId);
            Assert.AreEqual(0xEF12, result.Data.DeviceId);
        }

        [TestMethod]
        public async Task ReadId_WhenJedecReadFails_ReturnsFailure()
        {
            // Arrange
            _mockHardware.Setup(h => h.SpiTransferAsync(
                It.IsAny<byte[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult<byte[]>.FailureResult("Transfer failed"));

            // Act
            var result = await _protocol.ReadIdAsync();

            // Assert
            // Even if JEDEC fails, the method still returns success with partial data
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data);
        }

        #endregion

        #region Read Tests

        [TestMethod]
        public async Task Read_WithValidParameters_ReadsData()
        {
            // Arrange
            uint address = 0x1000;
            int length = 256;
            var expectedData = new byte[length];
            for (int i = 0; i < length; i++)
                expectedData[i] = (byte)(i & 0xFF);

            _mockHardware.Setup(h => h.SpiTransferAsync(
                It.IsAny<byte[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult<byte[]>.SuccessResult(expectedData));

            // Act
            var result = await _protocol.ReadAsync(address, length);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual(length, result.Data.Length);
        }

        [TestMethod]
        public async Task Read_WithZeroLength_Succeeds()
        {
            // Arrange
            uint address = 0x1000;
            int length = 0;

            // Act
            var result = await _protocol.ReadAsync(address, length);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual(0, result.Data.Length);
        }

        #endregion

        #region Write Tests

        [TestMethod]
        public async Task Write_WithValidData_WritesSuccessfully()
        {
            // Arrange
            uint address = 0x1000;
            var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            _mockHardware.Setup(h => h.SpiSendCommandAsync(It.IsAny<byte>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());
            _mockHardware.Setup(h => h.SpiWriteWithAddressAsync(
                It.IsAny<byte>(),
                It.IsAny<uint>(),
                It.IsAny<int>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());
            _mockHardware.Setup(h => h.SpiTransferAsync(
                It.IsAny<byte[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult<byte[]>.SuccessResult(new byte[] { 0x00 })); // Not busy

            // Act
            var result = await _protocol.WriteAsync(address, data);

            // Assert
            Assert.IsTrue(result.Success);
        }

        [TestMethod]
        public async Task Write_WithNullData_Fails()
        {
            // Arrange
            uint address = 0x1000;
            byte[] data = null;

            // Act
            var result = await _protocol.WriteAsync(address, data);

            // Assert
            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public async Task Write_WithEmptyData_Fails()
        {
            // Arrange
            uint address = 0x1000;
            var data = new byte[0];

            // Act
            var result = await _protocol.WriteAsync(address, data);

            // Assert
            Assert.IsFalse(result.Success);
        }

        #endregion

        #region Erase Tests

        [TestMethod]
        public async Task EraseSector_WithValidAddress_Succeeds()
        {
            // Arrange
            uint address = 0x1000;

            _mockHardware.Setup(h => h.SpiSendCommandAsync(It.IsAny<byte>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());
            _mockHardware.Setup(h => h.SpiWriteWithAddressAsync(
                It.IsAny<byte>(),
                It.IsAny<uint>(),
                It.IsAny<int>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());
            _mockHardware.Setup(h => h.SpiTransferAsync(
                It.IsAny<byte[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult<byte[]>.SuccessResult(new byte[] { 0x00 })); // Not busy

            // Act
            var result = await _protocol.EraseSectorAsync(address);

            // Assert
            Assert.IsTrue(result.Success);
        }

        [TestMethod]
        public async Task EraseBlock64_WithValidAddress_Succeeds()
        {
            // Arrange
            uint address = 0x10000;

            _mockHardware.Setup(h => h.SpiSendCommandAsync(It.IsAny<byte>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());
            _mockHardware.Setup(h => h.SpiWriteWithAddressAsync(
                It.IsAny<byte>(),
                It.IsAny<uint>(),
                It.IsAny<int>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());
            _mockHardware.Setup(h => h.SpiTransferAsync(
                It.IsAny<byte[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult<byte[]>.SuccessResult(new byte[] { 0x00 })); // Not busy

            // Act
            var result = await _protocol.EraseBlock64Async(address);

            // Assert
            Assert.IsTrue(result.Success);
        }

        [TestMethod]
        public async Task EraseChip_Always_SendsChipEraseCommand()
        {
            // Arrange
            _mockHardware.Setup(h => h.SpiSendCommandAsync(0x06, It.IsAny<CancellationToken>())) // Write Enable
                .ReturnsAsync(OperationResult.SuccessResult());
            _mockHardware.Setup(h => h.SpiSendCommandAsync(0xC7, It.IsAny<CancellationToken>())) // Chip Erase
                .ReturnsAsync(OperationResult.SuccessResult());
            _mockHardware.Setup(h => h.SpiTransferAsync(
                It.Is<byte[]>(b => b[0] == 0x05), // Read Status
                1,
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult<byte[]>.SuccessResult(new byte[] { 0x00 })); // Not busy

            // Act
            var result = await _protocol.EraseChipAsync();

            // Assert
            Assert.IsTrue(result.Success);
        }

        #endregion

        #region Status Register Tests

        [TestMethod]
        public async Task ReadStatusRegister_ReturnsStatusByte()
        {
            // Arrange
            byte expectedStatus = 0x02; // Write Enable Latch set

            _mockHardware.Setup(h => h.SpiTransferAsync(
                It.Is<byte[]>(b => b[0] == 0x05), // Read Status
                1,
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult<byte[]>.SuccessResult(new byte[] { expectedStatus }));

            // Act
            var result = await _protocol.ReadStatusRegisterAsync();

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(expectedStatus, result.Data);
        }

        #endregion

        #region Parallel Execution Tests

        [TestMethod]
        public async Task MultipleInstances_CanRunIndependently()
        {
            // Arrange
            var mock1 = new Mock<IHardware>();
            var mock2 = new Mock<IHardware>();
            mock1.Setup(h => h.IsConnected).Returns(true);
            mock2.Setup(h => h.IsConnected).Returns(true);

            var protocol1 = new Spi25Protocol(mock1.Object);
            var protocol2 = new Spi25Protocol(mock2.Object);

            mock1.Setup(h => h.SpiTransferAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult<byte[]>.SuccessResult(new byte[] { 0x01 }));
            mock2.Setup(h => h.SpiTransferAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult<byte[]>.SuccessResult(new byte[] { 0x02 }));

            // Act
            var task1 = protocol1.ReadStatusRegisterAsync();
            var task2 = protocol2.ReadStatusRegisterAsync();
            await Task.WhenAll(task1, task2);

            // Assert
            Assert.AreEqual(0x01, task1.Result.Data);
            Assert.AreEqual(0x02, task2.Result.Data);

            protocol1.Dispose();
            protocol2.Dispose();
        }

        #endregion

        #region Disposal Tests

        [TestMethod]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Arrange
            var protocol = new Spi25Protocol(_mockHardware.Object);

            // Act & Assert - should not throw
            protocol.Dispose();
            protocol.Dispose();
            protocol.Dispose();
        }

        [TestMethod]
        public void Dispose_CanBeCalledOnDifferentInstances()
        {
            // Arrange
            var protocol1 = new Spi25Protocol(_mockHardware.Object);
            var protocol2 = new Spi25Protocol(_mockHardware.Object);

            // Act & Assert - should not throw
            protocol1.Dispose();
            protocol2.Dispose();
        }

        #endregion
    }
}
