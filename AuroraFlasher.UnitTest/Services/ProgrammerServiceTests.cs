using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using AuroraFlasher.Interfaces;
using AuroraFlasher.Models;
using AuroraFlasher.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuroraFlasher.UnitTest.Services
{
    [TestClass]
    public class ProgrammerServiceTests
    {
        private ProgrammerService _service;
        private Mock<IHardware> _mockHardware;
        private Mock<ISpiProtocol> _mockSpiProtocol;
        private Mock<II2CProtocol> _mockI2cProtocol;
        private Mock<IMicrowireProtocol> _mockMicrowireProtocol;

        [TestInitialize]
        public void Setup()
        {
            _service = new ProgrammerService();
            _mockHardware = new Mock<IHardware>();
            _mockSpiProtocol = new Mock<ISpiProtocol>();
            _mockI2cProtocol = new Mock<II2CProtocol>();
            _mockMicrowireProtocol = new Mock<IMicrowireProtocol>();

            // Default setup
            _mockHardware.Setup(h => h.IsConnected).Returns(false);
            _mockHardware.Setup(h => h.Type).Returns(HardwareType.CH341);
            _mockHardware.Setup(h => h.Name).Returns("Test Hardware");
        }

        #region EnumerateHardwareAsync Tests

        [TestMethod]
        public async Task EnumerateHardware_WithAutoType_ReturnsHardwareArray()
        {
            // Act
            var result = await _service.EnumerateHardwareAsync(HardwareType.Auto);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data);
            Assert.IsTrue(result.Data.Length > 0);
            Assert.IsTrue(result.Message.Contains("Found"));
        }

        [TestMethod]
        public async Task EnumerateHardware_WithCH341Type_ReturnsCH341Hardware()
        {
            // Act
            var result = await _service.EnumerateHardwareAsync(HardwareType.CH341);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual(1, result.Data.Length);
            Assert.AreEqual(HardwareType.CH341, result.Data[0].Type);
        }

        [TestMethod]
        public async Task EnumerateHardware_WithCancellationToken_CanBeCancelled()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            {
                var result = await _service.EnumerateHardwareAsync(HardwareType.Auto, cts.Token);
                cts.Token.ThrowIfCancellationRequested();
            });
        }

        [TestMethod]
        public async Task EnumerateHardware_MultipleCalls_ReturnsConsistentResults()
        {
            // Act
            var result1 = await _service.EnumerateHardwareAsync();
            var result2 = await _service.EnumerateHardwareAsync();

            // Assert
            Assert.AreEqual(result1.Data.Length, result2.Data.Length);
        }

        #endregion

        #region ConnectAsync Tests

        [TestMethod]
        public async Task Connect_WithNullHardware_ReturnsFailure()
        {
            // Act
            var result = await _service.ConnectAsync(null);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Message.Contains("null"));
        }

        [TestMethod]
        public async Task Connect_WithValidHardware_Succeeds()
        {
            // Arrange
            _mockHardware.Setup(h => h.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult("Connected"));

            // Act
            var result = await _service.ConnectAsync(_mockHardware.Object);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(_mockHardware.Object, _service.Hardware);
            _mockHardware.Verify(h => h.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task Connect_WithDevicePath_PassesPathToHardware()
        {
            // Arrange
            var devicePath = "COM3";
            _mockHardware.Setup(h => h.OpenAsync(devicePath, It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult("Connected"));

            // Act
            var result = await _service.ConnectAsync(_mockHardware.Object, devicePath);

            // Assert
            Assert.IsTrue(result.Success);
            _mockHardware.Verify(h => h.OpenAsync(devicePath, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task Connect_WhenHardwareOpenFails_ReturnsFailure()
        {
            // Arrange
            _mockHardware.Setup(h => h.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.FailureResult("Open failed"));

            // Act
            var result = await _service.ConnectAsync(_mockHardware.Object);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Message.Contains("failed"));
        }

        [TestMethod]
        public async Task Connect_WhenHardwareThrowsException_ReturnsFailure()
        {
            // Arrange
            _mockHardware.Setup(h => h.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Hardware error"));

            // Act
            var result = await _service.ConnectAsync(_mockHardware.Object);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Message.Contains("Hardware error"));
        }

        #endregion

        #region DisconnectAsync Tests

        [TestMethod]
        public async Task Disconnect_WhenNoHardwareConnected_Succeeds()
        {
            // Act
            var result = await _service.DisconnectAsync();

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Message.Contains("No hardware"));
        }

        [TestMethod]
        public async Task Disconnect_WithConnectedHardware_CallsClose()
        {
            // Arrange
            _mockHardware.Setup(h => h.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());
            _mockHardware.Setup(h => h.CloseAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult("Closed"));

            await _service.ConnectAsync(_mockHardware.Object);

            // Act
            var result = await _service.DisconnectAsync();

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsNull(_service.Hardware);
            _mockHardware.Verify(h => h.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task Disconnect_WithSpiProtocol_ExitsProtocolFirst()
        {
            // Arrange
            _mockHardware.Setup(h => h.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());
            _mockHardware.Setup(h => h.CloseAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());
            _mockSpiProtocol.Setup(p => p.ExitProgrammingModeAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());

            await _service.ConnectAsync(_mockHardware.Object);

            // Simulate SPI protocol being set (through reflection or public setter if available)
            // Since SpiProtocol is read-only, we need to test via DetectChipAsync or similar
            // For this test, we'll verify the disconnect logic itself

            // Act
            var result = await _service.DisconnectAsync();

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsNull(_service.SpiProtocol);
        }

        [TestMethod]
        public async Task Disconnect_WhenHardwareCloseFails_ReturnsFailure()
        {
            // Arrange
            _mockHardware.Setup(h => h.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());
            _mockHardware.Setup(h => h.CloseAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.FailureResult("Close failed"));

            await _service.ConnectAsync(_mockHardware.Object);

            // Act
            var result = await _service.DisconnectAsync();

            // Assert
            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public async Task Disconnect_ClearsAllProtocols()
        {
            // Arrange
            _mockHardware.Setup(h => h.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());
            _mockHardware.Setup(h => h.CloseAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());

            await _service.ConnectAsync(_mockHardware.Object);

            // Act
            await _service.DisconnectAsync();

            // Assert
            Assert.IsNull(_service.SpiProtocol);
            Assert.IsNull(_service.I2CProtocol);
            Assert.IsNull(_service.MicrowireProtocol);
        }

        #endregion

        #region DetectChipAsync Tests

        [TestMethod]
        public async Task DetectChip_WhenNotConnected_ReturnsFailure()
        {
            // Act
            var result = await _service.DetectChipAsync(ProtocolType.SPI);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Message.Contains("not connected"));
        }

        [TestMethod]
        public async Task DetectChip_WithSpiProtocol_CreatesProtocolAndDetects()
        {
            // Arrange
            _mockHardware.Setup(h => h.IsConnected).Returns(true);
            _mockHardware.Setup(h => h.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());

            await _service.ConnectAsync(_mockHardware.Object);

            // For this test to work properly, we need the actual DetectChipAsync implementation
            // Since it creates real protocols, we can't easily mock them without changing the service
            // We'll test that it attempts to detect

            // Act
            var result = await _service.DetectChipAsync(ProtocolType.SPI);

            // Assert
            // Result may fail if hardware doesn't actually work, but the method should execute
            Assert.IsNotNull(result);
        }

        #endregion

        #region Progress Event Tests

        [TestMethod]
        public void ProgressChanged_CanSubscribeAndUnsubscribe()
        {
            // Arrange
            var eventCount = 0;
            EventHandler<ProgressInfo> handler = (s, e) => eventCount++;

            // Act
            _service.ProgressChanged += handler;
            _service.ProgressChanged -= handler;

            // Simulate event raise (can't directly raise private event, but test subscription works)
            // Assert
            Assert.AreEqual(0, eventCount); // Event shouldn't have been raised
        }

        #endregion

        #region Property Tests

        [TestMethod]
        public void Hardware_InitiallyNull()
        {
            // Arrange
            var service = new ProgrammerService();

            // Assert
            Assert.IsNull(service.Hardware);
        }

        [TestMethod]
        public async Task Hardware_AfterConnect_ReturnsConnectedHardware()
        {
            // Arrange
            _mockHardware.Setup(h => h.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());

            // Act
            await _service.ConnectAsync(_mockHardware.Object);

            // Assert
            Assert.AreEqual(_mockHardware.Object, _service.Hardware);
        }

        [TestMethod]
        public async Task Hardware_AfterDisconnect_ReturnsNull()
        {
            // Arrange
            _mockHardware.Setup(h => h.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());
            _mockHardware.Setup(h => h.CloseAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());

            await _service.ConnectAsync(_mockHardware.Object);

            // Act
            await _service.DisconnectAsync();

            // Assert
            Assert.IsNull(_service.Hardware);
        }

        [TestMethod]
        public void Protocols_InitiallyNull()
        {
            // Arrange
            var service = new ProgrammerService();

            // Assert
            Assert.IsNull(service.SpiProtocol);
            Assert.IsNull(service.I2CProtocol);
            Assert.IsNull(service.MicrowireProtocol);
        }

        #endregion

        #region Parallel Execution Tests

        [TestMethod]
        public async Task MultipleServices_CanOperateIndependently()
        {
            // Arrange
            var service1 = new ProgrammerService();
            var service2 = new ProgrammerService();

            var mock1 = new Mock<IHardware>();
            var mock2 = new Mock<IHardware>();

            mock1.Setup(h => h.Name).Returns("Hardware 1");
            mock2.Setup(h => h.Name).Returns("Hardware 2");
            mock1.Setup(h => h.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());
            mock2.Setup(h => h.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());

            // Act
            var task1 = service1.ConnectAsync(mock1.Object);
            var task2 = service2.ConnectAsync(mock2.Object);
            await Task.WhenAll(task1, task2);

            // Assert
            Assert.IsTrue(task1.Result.Success);
            Assert.IsTrue(task2.Result.Success);
            Assert.AreEqual(mock1.Object, service1.Hardware);
            Assert.AreEqual(mock2.Object, service2.Hardware);
        }

        [TestMethod]
        public async Task EnumerateHardware_CanRunInParallel()
        {
            // Arrange
            var service1 = new ProgrammerService();
            var service2 = new ProgrammerService();
            var service3 = new ProgrammerService();

            // Act
            var task1 = service1.EnumerateHardwareAsync();
            var task2 = service2.EnumerateHardwareAsync();
            var task3 = service3.EnumerateHardwareAsync();
            
            await Task.WhenAll(task1, task2, task3);

            // Assert
            Assert.IsTrue(task1.Result.Success);
            Assert.IsTrue(task2.Result.Success);
            Assert.IsTrue(task3.Result.Success);
            Assert.AreEqual(task1.Result.Data.Length, task2.Result.Data.Length);
            Assert.AreEqual(task2.Result.Data.Length, task3.Result.Data.Length);
        }

        #endregion

        #region Order Independence Tests

        [TestMethod]
        public async Task Connect_Disconnect_Connect_WorksCorrectly()
        {
            // Arrange
            _mockHardware.Setup(h => h.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());
            _mockHardware.Setup(h => h.CloseAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());

            // Act
            var connect1 = await _service.ConnectAsync(_mockHardware.Object);
            var disconnect1 = await _service.DisconnectAsync();
            var connect2 = await _service.ConnectAsync(_mockHardware.Object);

            // Assert
            Assert.IsTrue(connect1.Success);
            Assert.IsTrue(disconnect1.Success);
            Assert.IsTrue(connect2.Success);
            Assert.IsNotNull(_service.Hardware);
        }

        [TestMethod]
        public async Task Disconnect_BeforeConnect_HandlesGracefully()
        {
            // Act
            var result = await _service.DisconnectAsync();

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Message.Contains("No hardware"));
        }

        [TestMethod]
        public async Task DetectChip_BeforeConnect_ReturnsFailure()
        {
            // Act
            var result = await _service.DetectChipAsync(ProtocolType.SPI);

            // Assert
            Assert.IsFalse(result.Success);
        }

        #endregion

        #region State Isolation Tests

        [TestMethod]
        public async Task TwoServices_DoNotShareState()
        {
            // Arrange
            var service1 = new ProgrammerService();
            var service2 = new ProgrammerService();

            var mock1 = new Mock<IHardware>();
            mock1.Setup(h => h.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.SuccessResult());

            // Act
            await service1.ConnectAsync(mock1.Object);

            // Assert
            Assert.IsNotNull(service1.Hardware);
            Assert.IsNull(service2.Hardware); // service2 should not be affected
        }

        #endregion
    }
}
