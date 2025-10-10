using Microsoft.VisualStudio.TestTools.UnitTesting;
using AuroraFlasher.Models;
using System;
using System.Threading;

namespace AuroraFlasher.Tests.Models
{
    [TestClass]
    public class ProgressInfoTests
    {
        [TestMethod]
        public void ProgressInfo_DefaultConstructor_InitializesCorrectly()
        {
            // Arrange & Act
            var progress = new ProgressInfo();

            // Assert
            Assert.AreEqual(0, progress.Percentage);
            Assert.AreEqual(0, progress.BytesProcessed);
            Assert.AreEqual(0, progress.TotalBytes);
            Assert.IsNotNull(progress.Status);
            Assert.AreEqual(string.Empty, progress.Status);
        }

        [TestMethod]
        public void ProgressInfo_ConstructorWithData_CalculatesPercentage()
        {
            // Arrange
            long bytesProcessed = 50;
            long totalBytes = 100;
            string status = "Processing";

            // Act
            var progress = new ProgressInfo(bytesProcessed, totalBytes, status);

            // Assert
            Assert.AreEqual(50.0, progress.Percentage, 0.01);
            Assert.AreEqual(bytesProcessed, progress.BytesProcessed);
            Assert.AreEqual(totalBytes, progress.TotalBytes);
            Assert.AreEqual(status, progress.Status);
        }

        [TestMethod]
        public void ProgressInfo_Update_UpdatesPercentageAndStatus()
        {
            // Arrange
            var progress = new ProgressInfo(0, 100);
            
            // Act
            progress.Update(75, "Almost done");

            // Assert
            Assert.AreEqual(75.0, progress.Percentage, 0.01);
            Assert.AreEqual(75, progress.BytesProcessed);
            Assert.AreEqual("Almost done", progress.Status);
        }

        [TestMethod]
        public void ProgressInfo_Speed_CalculatesCorrectly()
        {
            // Arrange
            var progress = new ProgressInfo(0, 10000);
            Thread.Sleep(10); // Small delay to ensure elapsed time
            
            // Act
            progress.Update(1000, "Reading");
            double speed = progress.Speed;

            // Assert
            Assert.IsTrue(speed > 0, "Speed should be greater than zero");
        }

        [TestMethod]
        public void ProgressInfo_ToString_FormatsCorrectly()
        {
            // Arrange
            var progress = new ProgressInfo(5000, 10000, "Reading memory");

            // Act
            string result = progress.ToString();

            // Assert
            Assert.IsTrue(result.Contains("50"), "Should contain percentage");
            Assert.IsTrue(result.Contains("Reading memory"), "Should contain status");
            Assert.IsTrue(result.Contains("KB/s"), "Should contain speed unit");
        }

        [TestMethod]
        public void ProgressInfo_ZeroTotal_HandlesGracefully()
        {
            // Arrange & Act
            var progress = new ProgressInfo(10, 0);

            // Assert
            Assert.AreEqual(0, progress.Percentage);
        }
    }
}
