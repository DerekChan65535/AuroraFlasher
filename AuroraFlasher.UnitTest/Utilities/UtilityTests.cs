using Microsoft.VisualStudio.TestTools.UnitTesting;
using AuroraFlasher.Utilities;
using System;

namespace AuroraFlasher.UnitTest.Utilities
{
    [TestClass]
    public class BitOperationsTests
    {
        [TestMethod]
        public void IsBitSet_BitIsSet_ReturnsTrue()
        {
            // Arrange
            byte value = 0b10101010; // Bits 1, 3, 5, 7 are set

            // Act & Assert
            Assert.IsTrue(BitOperations.IsBitSet(value, 1));
            Assert.IsTrue(BitOperations.IsBitSet(value, 3));
            Assert.IsTrue(BitOperations.IsBitSet(value, 5));
            Assert.IsTrue(BitOperations.IsBitSet(value, 7));
        }

        [TestMethod]
        public void IsBitSet_BitIsNotSet_ReturnsFalse()
        {
            // Arrange
            byte value = 0b10101010;

            // Act & Assert
            Assert.IsFalse(BitOperations.IsBitSet(value, 0));
            Assert.IsFalse(BitOperations.IsBitSet(value, 2));
            Assert.IsFalse(BitOperations.IsBitSet(value, 4));
            Assert.IsFalse(BitOperations.IsBitSet(value, 6));
        }

        [TestMethod]
        public void SetBit_SetBitTrue_SetsCorrectBit()
        {
            // Arrange
            byte value = 0b00000000;

            // Act
            value = BitOperations.SetBit(value, 3, true);

            // Assert
            Assert.AreEqual(0b00001000, value);
        }

        [TestMethod]
        public void SetBit_SetBitFalse_ClearsBit()
        {
            // Arrange
            byte value = 0b11111111;

            // Act
            value = BitOperations.SetBit(value, 3, false);

            // Assert
            Assert.AreEqual(0b11110111, value);
        }

        [TestMethod]
        public void GetBits_ExtractsCorrectBits()
        {
            // Arrange
            byte value = 0b11010110;

            // Act
            var result = BitOperations.GetBits(value, 2, 3); // Extract 3 bits starting at bit 2

            // Assert
            Assert.AreEqual(0b101, result);
        }

        [TestMethod]
        public void ReverseBits_ReversesByte()
        {
            // Arrange
            byte value = 0b10110001;
            byte expected = 0b10001101;

            // Act
            var result = BitOperations.ReverseBits(value);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void PopCount_CountsSetBits()
        {
            // Arrange
            byte value1 = 0b10101010; // 4 bits set
            byte value2 = 0b11111111; // 8 bits set
            byte value3 = 0b00000000; // 0 bits set

            // Act
            var count1 = BitOperations.PopCount(value1);
            var count2 = BitOperations.PopCount(value2);
            var count3 = BitOperations.PopCount(value3);

            // Assert
            Assert.AreEqual(4, count1);
            Assert.AreEqual(8, count2);
            Assert.AreEqual(0, count3);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void IsBitSet_InvalidBitIndex_ThrowsException()
        {
            // Arrange
            byte value = 0b10101010;

            // Act
            BitOperations.IsBitSet(value, 8); // Invalid index

            // Assert - Exception expected
        }
    }

    [TestClass]
    public class HexConverterTests
    {
        [TestMethod]
        public void ToHexString_ConvertsCorrectly()
        {
            // Arrange
            var data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

            // Act
            var result = HexConverter.ToHexString(data);

            // Assert
            Assert.AreEqual("DEADBEEF", result);
        }

        [TestMethod]
        public void ToHexString_WithSeparator_ConvertsCorrectly()
        {
            // Arrange
            var data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

            // Act
            var result = HexConverter.ToHexString(data, " ");

            // Assert
            Assert.AreEqual("DE AD BE EF", result);
        }

        [TestMethod]
        public void FromHexString_ConvertsCorrectly()
        {
            // Arrange
            var hex = "DEADBEEF";

            // Act
            var result = HexConverter.FromHexString(hex);

            // Assert
            Assert.AreEqual(4, result.Length);
            Assert.AreEqual(0xDE, result[0]);
            Assert.AreEqual(0xAD, result[1]);
            Assert.AreEqual(0xBE, result[2]);
            Assert.AreEqual(0xEF, result[3]);
        }

        [TestMethod]
        public void FromHexString_WithSpaces_ConvertsCorrectly()
        {
            // Arrange
            var hex = "DE AD BE EF";

            // Act
            var result = HexConverter.FromHexString(hex);

            // Assert
            Assert.AreEqual(4, result.Length);
            Assert.AreEqual(0xDE, result[0]);
        }

        [TestMethod]
        public void FromHexString_With0xPrefix_ConvertsCorrectly()
        {
            // Arrange
            var hex = "0xDEADBEEF";

            // Act
            var result = HexConverter.FromHexString(hex);

            // Assert
            Assert.AreEqual(4, result.Length);
            Assert.AreEqual(0xDE, result[0]);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void FromHexString_OddLength_ThrowsException()
        {
            // Arrange
            var hex = "DEA"; // Odd length

            // Act
            HexConverter.FromHexString(hex);

            // Assert - Exception expected
        }

        [TestMethod]
        public void ToHexDump_FormatsCorrectly()
        {
            // Arrange
            var data = new byte[32];
            for (var i = 0; i < 32; i++)
                data[i] = (byte)i;

            // Act
            var result = HexConverter.ToHexDump(data, 16);

            // Assert
            Assert.IsTrue(result.Contains("00000000:"));
            Assert.IsTrue(result.Contains("00000010:"));
        }

        [TestMethod]
        public void TryFromHexString_ValidHex_ReturnsTrue()
        {
            // Arrange
            var hex = "DEADBEEF";

            // Act
            var success = HexConverter.TryFromHexString(hex, out var result);

            // Assert
            Assert.IsTrue(success);
            Assert.IsNotNull(result);
            Assert.AreEqual(4, result.Length);
        }

        [TestMethod]
        public void TryFromHexString_InvalidHex_ReturnsFalse()
        {
            // Arrange
            var hex = "XYZ";

            // Act
            var success = HexConverter.TryFromHexString(hex, out var result);

            // Assert
            Assert.IsFalse(success);
            Assert.IsNull(result);
        }
    }

    [TestClass]
    public class ByteArrayExtensionsTests
    {
        [TestMethod]
        public void IsAll_AllSameValue_ReturnsTrue()
        {
            // Arrange
            var data = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };

            // Act
            var result = data.IsAll(0xFF);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsAll_NotAllSame_ReturnsFalse()
        {
            // Arrange
            var data = new byte[] { 0xFF, 0xFF, 0x00, 0xFF };

            // Act
            var result = data.IsAll(0xFF);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsBlank_AllFF_ReturnsTrue()
        {
            // Arrange
            var data = new byte[256];
            data.Fill(0xFF);

            // Act
            var result = data.IsBlank();

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsBlank_NotAllFF_ReturnsFalse()
        {
            // Arrange
            var data = new byte[256];
            data.Fill(0xFF);
            data[100] = 0x00;

            // Act
            var result = data.IsBlank();

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Fill_FillsArrayWithValue()
        {
            // Arrange
            var data = new byte[10];

            // Act
            data.Fill(0xAA);

            // Assert
            Assert.IsTrue(data.IsAll(0xAA));
        }

        [TestMethod]
        public void SequenceEqual_SameArrays_ReturnsTrue()
        {
            // Arrange
            var data1 = new byte[] { 1, 2, 3, 4, 5 };
            var data2 = new byte[] { 1, 2, 3, 4, 5 };

            // Act
            var result = data1.SequenceEqual(data2);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void SequenceEqual_DifferentArrays_ReturnsFalse()
        {
            // Arrange
            var data1 = new byte[] { 1, 2, 3, 4, 5 };
            var data2 = new byte[] { 1, 2, 3, 4, 6 };

            // Act
            var result = data1.SequenceEqual(data2);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void SequenceEqual_DifferentLengths_ReturnsFalse()
        {
            // Arrange
            var data1 = new byte[] { 1, 2, 3, 4, 5 };
            var data2 = new byte[] { 1, 2, 3, 4 };

            // Act
            var result = data1.SequenceEqual(data2);

            // Assert
            Assert.IsFalse(result);
        }
    }
}
