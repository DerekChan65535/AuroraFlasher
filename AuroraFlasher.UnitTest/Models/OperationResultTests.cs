using Microsoft.VisualStudio.TestTools.UnitTesting;
using AuroraFlasher.Models;
using System;

namespace AuroraFlasher.UnitTest.Models
{
    [TestClass]
    public class OperationResultTests
    {
        [TestMethod]
        public void OperationResult_DefaultConstructor_CreatesSuccessResult()
        {
            // Arrange & Act
            var result = new OperationResult();

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Message);
            Assert.AreEqual(string.Empty, result.Message);
            Assert.IsNull(result.Exception);
        }

        [TestMethod]
        public void OperationResult_SuccessResult_CreatesSuccessWithMessage()
        {
            // Arrange
            string message = "Operation completed successfully";

            // Act
            var result = OperationResult.SuccessResult(message);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(message, result.Message);
            Assert.IsNull(result.Exception);
        }

        [TestMethod]
        public void OperationResult_FailureResult_CreatesFailureWithMessageAndException()
        {
            // Arrange
            string message = "Operation failed";
            var exception = new InvalidOperationException("Test exception");

            // Act
            var result = OperationResult.FailureResult(message, exception);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual(message, result.Message);
            Assert.AreEqual(exception, result.Exception);
        }

        [TestMethod]
        public void OperationResultGeneric_SuccessResult_ContainsData()
        {
            // Arrange
            string testData = "Test Data";
            string message = "Data retrieved";

            // Act
            var result = OperationResult<string>.SuccessResult(testData, message);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(testData, result.Data);
            Assert.AreEqual(message, result.Message);
        }

        [TestMethod]
        public void OperationResultGeneric_FailureResult_DataIsDefault()
        {
            // Arrange
            string message = "Failed to get data";

            // Act
            var result = OperationResult<string>.FailureResult(message);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Data);
            Assert.AreEqual(message, result.Message);
        }

        [TestMethod]
        public void OperationResult_ToString_FormatsCorrectly()
        {
            // Arrange
            var successResult = OperationResult.SuccessResult("All good");
            var failureResult = OperationResult.FailureResult("Something went wrong");

            // Act
            string successString = successResult.ToString();
            string failureString = failureResult.ToString();

            // Assert
            Assert.IsTrue(successString.Contains("Success"));
            Assert.IsTrue(successString.Contains("All good"));
            Assert.IsTrue(failureString.Contains("Failed"));
            Assert.IsTrue(failureString.Contains("Something went wrong"));
        }
    }
}
