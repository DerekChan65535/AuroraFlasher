using Microsoft.VisualStudio.TestTools.UnitTesting;
using AuroraFlasher.Models;
using System;

namespace AuroraFlasher.UnitTest.Models
{
    [TestClass]
    public class SettingsTests
    {
        [TestMethod]
        public void Settings_DefaultConstructor_InitializesWithDefaults()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            Assert.IsNotNull(settings);
            Assert.AreEqual(SpiSpeed.Normal, settings.SpiSpeed);
            Assert.AreEqual(true, settings.VerifyAfterWrite);
            Assert.AreEqual(true, settings.AutoDetectChip);
        }

        [TestMethod]
        public void Settings_SpiSpeed_CanBeSetAndRetrieved()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.SpiSpeed = SpiSpeed.High;

            // Assert
            Assert.AreEqual(SpiSpeed.High, settings.SpiSpeed);
        }

        [TestMethod]
        public void Settings_VerifyAfterWrite_CanBeToggled()
        {
            // Arrange
            var settings = new Settings { VerifyAfterWrite = true };

            // Act
            settings.VerifyAfterWrite = false;

            // Assert
            Assert.IsFalse(settings.VerifyAfterWrite);
        }

        [TestMethod]
        public void Settings_AutoDetectChip_CanBeToggled()
        {
            // Arrange
            var settings = new Settings { AutoDetectChip = false };

            // Act
            settings.AutoDetectChip = true;

            // Assert
            Assert.IsTrue(settings.AutoDetectChip);
        }

        [TestMethod]
        public void Settings_AllSpiSpeedValues_AreValid()
        {
            // Arrange
            var settings = new Settings();
            var speeds = new[] { SpiSpeed.Low, SpiSpeed.Normal, SpiSpeed.High, SpiSpeed.Maximum };

            // Act & Assert
            foreach (var speed in speeds)
            {
                settings.SpiSpeed = speed;
                Assert.AreEqual(speed, settings.SpiSpeed);
            }
        }
    }
}
