using Microsoft.VisualStudio.TestTools.UnitTesting;
using AuroraFlasher.Hardware;
using AuroraFlasher.Models;
using System;
using System.Threading.Tasks;

namespace AuroraFlasher.UnitTest.Hardware
{
    [TestClass]
    public class CH341HardwareTests
    {
        private CH341Hardware _hardware;

        [TestInitialize]
        public void Setup()
        {
            _hardware = new CH341Hardware();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _hardware?.Dispose();
        }

        #region Properties Tests

        [TestMethod]
        public void Type_ReturnsCorrectType()
        {
            // Assert
            Assert.AreEqual(HardwareType.CH341, _hardware.Type);
        }

        [TestMethod]
        public void Name_ReturnsCorrectName()
        {
            // Act
            var name = _hardware.Name;

            // Assert
            Assert.IsNotNull(name);
            Assert.IsTrue(name.Contains("CH341"));
        }

        [TestMethod]
        public void Capabilities_HasCorrectCapabilities()
        {
            // Act
            var capabilities = _hardware.Capabilities;

            // Assert
            Assert.IsNotNull(capabilities);
            Assert.IsTrue(capabilities.SupportsSpi, "Should support SPI");
            Assert.IsTrue(capabilities.SupportsI2C, "Should support I2C");
            Assert.IsTrue(capabilities.SupportsMicroWire, "Should support MicroWire");
            Assert.AreEqual(4096, capabilities.MaxSpiTransferSize);
            Assert.AreEqual(256, capabilities.MaxI2CTransferSize);
            Assert.IsFalse(capabilities.SupportsGpio, "Should not support GPIO");
            Assert.AreEqual(0, capabilities.GpioPinCount);
            Assert.IsTrue(capabilities.HasFirmwareVersion);
            Assert.IsFalse(capabilities.RequiresExternalPower);
        }

        [TestMethod]
        public void Capabilities_HasValidSpiSpeeds()
        {
            // Act
            var capabilities = _hardware.Capabilities;

            // Assert
            Assert.IsNotNull(capabilities.AvailableSpiSpeeds);
            Assert.IsTrue(capabilities.AvailableSpiSpeeds.Length > 0);
            CollectionAssert.Contains(capabilities.AvailableSpiSpeeds, SpiSpeed.Low);
            CollectionAssert.Contains(capabilities.AvailableSpiSpeeds, SpiSpeed.Normal);
            CollectionAssert.Contains(capabilities.AvailableSpiSpeeds, SpiSpeed.High);
        }

        [TestMethod]
        public void Capabilities_HasValidI2CSpeeds()
        {
            // Act
            var capabilities = _hardware.Capabilities;

            // Assert
            Assert.IsNotNull(capabilities.AvailableI2CSpeeds);
            Assert.IsTrue(capabilities.AvailableI2CSpeeds.Length > 0);
            CollectionAssert.Contains(capabilities.AvailableI2CSpeeds, 20);
            CollectionAssert.Contains(capabilities.AvailableI2CSpeeds, 100);
            CollectionAssert.Contains(capabilities.AvailableI2CSpeeds, 400);
            CollectionAssert.Contains(capabilities.AvailableI2CSpeeds, 750);
        }

        #endregion

        #region Connection Tests

        [TestMethod]
        public async Task EnumerateDevicesAsync_ReturnsDeviceArray()
        {
            // Act
            var devices = await _hardware.EnumerateDevicesAsync();

            // Assert
            Assert.IsNotNull(devices);
            // Note: May be empty if no hardware is connected
        }

        [TestMethod]
        public async Task OpenAsync_WhenNotConnected_ReturnsResult()
        {
            // Note: This will likely fail without actual hardware
            // Testing that it returns a proper result structure
            
            // Act
            var result = await _hardware.OpenAsync();

            // Assert
            Assert.IsNotNull(result);
            // Can't assert success without actual hardware
        }

        [TestMethod]
        public async Task CloseAsync_WhenNotOpen_ReturnsSuccess()
        {
            // Act
            var result = await _hardware.CloseAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.AreEqual("Device was not open", result.Message);
        }

        [TestMethod]
        public async Task IsConnected_InitiallyFalse()
        {
            // Assert
            Assert.IsFalse(_hardware.IsConnected);
        }

        #endregion

        #region SPI Operation Tests

        [TestMethod]
        public async Task SpiInitAsync_WhenNotConnected_ThrowsException()
        {
            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await _hardware.SpiInitAsync()
            );
        }

        [TestMethod]
        public async Task SpiDeinitAsync_ReturnsSuccess()
        {
            // Act
            var result = await _hardware.SpiDeinitAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
        }

        [TestMethod]
        public async Task SpiSendCommandAsync_WhenNotConnected_ThrowsException()
        {
            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await _hardware.SpiSendCommandAsync(0x9F)
            );
        }

        [TestMethod]
        public async Task SpiReadAsync_WhenNotConnected_ThrowsException()
        {
            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await _hardware.SpiReadAsync(256)
            );
        }

        [TestMethod]
        public async Task SpiWriteAsync_WhenNotConnected_ThrowsException()
        {
            // Arrange
            var data = new byte[] { 0x01, 0x02, 0x03 };

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await _hardware.SpiWriteAsync(data)
            );
        }

        [TestMethod]
        public async Task SpiTransferAsync_WhenNotConnected_ThrowsException()
        {
            // Arrange
            var writeData = new byte[] { 0x9F };

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await _hardware.SpiTransferAsync(writeData, 3)
            );
        }

        #endregion

        #region I2C Operation Tests

        [TestMethod]
        public async Task I2CInitAsync_WhenNotConnected_ThrowsException()
        {
            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await _hardware.I2CInitAsync(100)
            );
        }

        [TestMethod]
        public async Task I2CDeinitAsync_ReturnsSuccess()
        {
            // Act
            var result = await _hardware.I2CDeinitAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
        }

        #endregion

        #region Utility Tests

        [TestMethod]
        public async Task GetFirmwareVersionAsync_ReturnsVersion()
        {
            // Act
            var result = await _hardware.GetFirmwareVersionAsync();

            // Assert
            Assert.IsNotNull(result);
            // Should return something even if not connected (DLL/driver versions)
        }

        #endregion

        #region Dispose Tests

        [TestMethod]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Act & Assert - Should not throw
            _hardware.Dispose();
            _hardware.Dispose();
        }

        [TestMethod]
        public void Dispose_AfterDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            _hardware.Dispose();

            // Act & Assert
            Assert.ThrowsException<ObjectDisposedException>(
                () => _ = _hardware.IsConnected
            );
        }

        #endregion
    }
}
