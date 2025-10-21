using Microsoft.VisualStudio.TestTools.UnitTesting;
using AuroraFlasher.Models;
using System;

namespace AuroraFlasher.UnitTest.Models
{
    [TestClass]
    public class MemoryIdTests
    {
        [TestMethod]
        public void MemoryId_DefaultConstructor_InitializesJedecArray()
        {
            // Arrange & Act
            var memId = new MemoryId();

            // Assert
            Assert.IsNotNull(memId.JedecId);
            Assert.AreEqual(3, memId.JedecId.Length);
        }

        [TestMethod]
        public void MemoryId_ConstructorWithJedec_ParsesCorrectly()
        {
            // Arrange
            var jedecId = new byte[] { 0xEF, 0x40, 0x16 }; // Winbond W25Q32

            // Act
            var memId = new MemoryId(jedecId);

            // Assert
            Assert.AreEqual(0xEF, memId.ManufacturerId);
            Assert.AreEqual(0x4016, memId.DeviceId);
        }

        [TestMethod]
        public void MemoryId_ConstructorWithBytes_CreatesJedecArray()
        {
            // Arrange
            byte manufacturerId = 0xC2;
            ushort deviceId = 0x2016;

            // Act
            var memId = new MemoryId(manufacturerId, deviceId);

            // Assert
            Assert.AreEqual(manufacturerId, memId.JedecId[0]);
            Assert.AreEqual(0x20, memId.JedecId[1]);
            Assert.AreEqual(0x16, memId.JedecId[2]);
        }

        [TestMethod]
        public void MemoryId_Matches_ReturnsTrueForSameId()
        {
            // Arrange
            var memId1 = new MemoryId(0xEF, 0x4016);
            var memId2 = new MemoryId(0xEF, 0x4016);

            // Act
            var matches = memId1.Matches(memId2);

            // Assert
            Assert.IsTrue(matches);
        }

        [TestMethod]
        public void MemoryId_Matches_ReturnsFalseForDifferentId()
        {
            // Arrange
            var memId1 = new MemoryId(0xEF, 0x4016);
            var memId2 = new MemoryId(0xC2, 0x2016);

            // Act
            var matches = memId1.Matches(memId2);

            // Assert
            Assert.IsFalse(matches);
        }

        [TestMethod]
        public void MemoryId_IsBlank_ReturnsTrueForBlankId()
        {
            // Arrange
            var memId = new MemoryId(0xFF, 0xFFFF);

            // Act
            var isBlank = memId.IsBlank();

            // Assert
            Assert.IsTrue(isBlank);
        }

        [TestMethod]
        public void MemoryId_IsBlank_ReturnsFalseForValidId()
        {
            // Arrange
            var memId = new MemoryId(0xEF, 0x4016);

            // Act
            var isBlank = memId.IsBlank();

            // Assert
            Assert.IsFalse(isBlank);
        }

        [TestMethod]
        public void MemoryId_ToString_FormatsHexCorrectly()
        {
            // Arrange
            var memId = new MemoryId(0xEF, 0x4016);

            // Act
            var result = memId.ToString();

            // Assert
            Assert.IsTrue(result.Contains("EF"));
            Assert.IsTrue(result.Contains("40"));
            Assert.IsTrue(result.Contains("16"));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void MemoryId_ConstructorWithInvalidJedec_ThrowsException()
        {
            // Arrange
            var invalidJedec = new byte[] { 0xEF, 0x40 }; // Only 2 bytes

            // Act
            var memId = new MemoryId(invalidJedec);

            // Assert - Exception expected
        }
    }
}
