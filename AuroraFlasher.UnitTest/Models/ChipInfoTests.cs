using Microsoft.VisualStudio.TestTools.UnitTesting;
using AuroraFlasher.Models;
using System;
using System.ComponentModel;

namespace AuroraFlasher.UnitTest.Models
{
    [TestClass]
    public class ChipInfoTests
    {
        [TestMethod]
        public void ChipInfo_DefaultConstructor_InitializesWithDefaults()
        {
            // Arrange & Act
            var chip = new ChipInfo();

            // Assert
            Assert.IsNotNull(chip.Id);
            Assert.AreEqual("Unknown", chip.Name);
            Assert.AreEqual(ChipManufacturer.Unknown, chip.Manufacturer);
            Assert.AreEqual(ProtocolType.SPI, chip.ProtocolType);
            Assert.AreEqual(3300, chip.Voltage);
        }

        [TestMethod]
        public void ChipInfo_SetProperty_RaisesPropertyChanged()
        {
            // Arrange
            var chip = new ChipInfo();
            bool eventRaised = false;
            string propertyName = null;

            chip.PropertyChanged += (sender, e) =>
            {
                eventRaised = true;
                propertyName = e.PropertyName;
            };

            // Act
            chip.Name = "W25Q32";

            // Assert
            Assert.IsTrue(eventRaised);
            Assert.AreEqual(nameof(ChipInfo.Name), propertyName);
        }

        [TestMethod]
        public void ChipInfo_SizeKB_CalculatesCorrectly()
        {
            // Arrange
            var chip = new ChipInfo { Size = 4096 * 1024 }; // 4MB

            // Act
            int sizeKB = chip.SizeKB;

            // Assert
            Assert.AreEqual(4096, sizeKB);
        }

        [TestMethod]
        public void ChipInfo_SizeMB_CalculatesCorrectly()
        {
            // Arrange
            var chip = new ChipInfo { Size = 4 * 1024 * 1024 }; // 4MB

            // Act
            double sizeMB = chip.SizeMB;

            // Assert
            Assert.AreEqual(4.0, sizeMB, 0.01);
        }

        [TestMethod]
        public void ChipInfo_PageCount_CalculatesCorrectly()
        {
            // Arrange
            var chip = new ChipInfo
            {
                Size = 4 * 1024 * 1024, // 4MB
                PageSize = 256 // 256 bytes per page
            };

            // Act
            int pageCount = chip.PageCount;

            // Assert
            Assert.AreEqual(16384, pageCount); // 4MB / 256 bytes
        }

        [TestMethod]
        public void ChipInfo_DisplayName_FormatsCorrectly()
        {
            // Arrange
            var chip = new ChipInfo
            {
                Name = "W25Q32",
                Size = 4 * 1024 * 1024
            };

            // Act
            string displayName = chip.DisplayName;

            // Assert
            Assert.IsTrue(displayName.Contains("W25Q32"));
            Assert.IsTrue(displayName.Contains("4096KB"));
        }

        [TestMethod]
        public void ChipInfo_MemoryId_CreatesCorrectly()
        {
            // Arrange
            var chip = new ChipInfo
            {
                ManufacturerId = 0xEF,
                DeviceId = 0x4016
            };

            // Act
            var memId = chip.MemoryId;

            // Assert
            Assert.IsNotNull(memId);
            Assert.AreEqual(0xEF, memId.ManufacturerId);
            Assert.AreEqual(0x4016, memId.DeviceId);
        }

        [TestMethod]
        public void ChipInfo_Clone_CreatesDeepCopy()
        {
            // Arrange
            var original = new ChipInfo
            {
                Name = "W25Q32",
                Manufacturer = ChipManufacturer.Winbond,
                Size = 4 * 1024 * 1024,
                PageSize = 256
            };

            // Act
            var clone = original.Clone();
            clone.Name = "Modified";

            // Assert
            Assert.AreNotEqual(original.Name, clone.Name);
            Assert.AreEqual(original.Manufacturer, clone.Manufacturer);
            Assert.AreEqual(original.Size, clone.Size);
        }

        [TestMethod]
        public void ChipInfo_ToString_ReturnsDisplayName()
        {
            // Arrange
            var chip = new ChipInfo
            {
                Name = "W25Q32",
                Size = 4 * 1024 * 1024
            };

            // Act
            string result = chip.ToString();

            // Assert
            Assert.AreEqual(chip.DisplayName, result);
        }

        [TestMethod]
        public void ChipInfo_MultiplePropertyChanges_RaisesEventsForEach()
        {
            // Arrange
            var chip = new ChipInfo();
            int eventCount = 0;

            chip.PropertyChanged += (sender, e) => eventCount++;

            // Act
            chip.Name = "Test1";
            chip.Size = 1024;
            chip.PageSize = 256;

            // Assert
            Assert.AreEqual(3, eventCount);
        }
    }
}
