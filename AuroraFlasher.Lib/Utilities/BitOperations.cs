using System;
using System.Text;

namespace AuroraFlasher.Utilities
{
    /// <summary>
    /// Bit manipulation utilities
    /// </summary>
    public static class BitOperations
    {
        /// <summary>
        /// Check if bit is set
        /// </summary>
        public static bool IsBitSet(byte value, int bitIndex)
        {
            if (bitIndex < 0 || bitIndex > 7)
                throw new ArgumentOutOfRangeException(nameof(bitIndex));

            return (value & (1 << bitIndex)) != 0;
        }

        /// <summary>
        /// Set bit
        /// </summary>
        public static byte SetBit(byte value, int bitIndex, bool state)
        {
            if (bitIndex < 0 || bitIndex > 7)
                throw new ArgumentOutOfRangeException(nameof(bitIndex));

            if (state)
                return (byte)(value | (1 << bitIndex));
            else
                return (byte)(value & ~(1 << bitIndex));
        }

        /// <summary>
        /// Get bits from byte
        /// </summary>
        public static byte GetBits(byte value, int startBit, int bitCount)
        {
            if (startBit < 0 || startBit > 7)
                throw new ArgumentOutOfRangeException(nameof(startBit));
            if (bitCount < 1 || bitCount > 8)
                throw new ArgumentOutOfRangeException(nameof(bitCount));

            var mask = (1 << bitCount) - 1;
            return (byte)((value >> startBit) & mask);
        }

        /// <summary>
        /// Reverse byte bits
        /// </summary>
        public static byte ReverseBits(byte value)
        {
            byte result = 0;
            for (var i = 0; i < 8; i++)
            {
                result <<= 1;
                result |= (byte)(value & 1);
                value >>= 1;
            }
            return result;
        }

        /// <summary>
        /// Count set bits (population count)
        /// </summary>
        public static int PopCount(byte value)
        {
            var count = 0;
            while (value != 0)
            {
                count++;
                value &= (byte)(value - 1);
            }
            return count;
        }
    }

    /// <summary>
    /// Hex conversion utilities
    /// </summary>
    public static class HexConverter
    {
        private static readonly char[] HexChars = "0123456789ABCDEF".ToCharArray();

        /// <summary>
        /// Convert byte array to hex string
        /// </summary>
        public static string ToHexString(byte[] data, string separator = "")
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            var sb = new StringBuilder(data.Length * (2 + separator.Length));
            for (var i = 0; i < data.Length; i++)
            {
                if (i > 0 && separator.Length > 0)
                    sb.Append(separator);

                sb.Append(HexChars[data[i] >> 4]);
                sb.Append(HexChars[data[i] & 0x0F]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Convert hex string to byte array
        /// </summary>
        public static byte[] FromHexString(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return Array.Empty<byte>();

            // Remove common separators
            hex = hex.Replace(" ", "").Replace("-", "").Replace(":", "").Replace("0x", "");

            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have even length", nameof(hex));

            var result = new byte[hex.Length / 2];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return result;
        }

        /// <summary>
        /// Format byte array as hex dump
        /// </summary>
        public static string ToHexDump(byte[] data, int bytesPerLine = 16, bool showAddress = true, bool showAscii = true)
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            var sb = new StringBuilder();
            for (var i = 0; i < data.Length; i += bytesPerLine)
            {
                // Address
                if (showAddress)
                {
                    sb.AppendFormat("{0:X8}: ", i);
                }

                // Hex bytes
                var lineLength = Math.Min(bytesPerLine, data.Length - i);
                for (var j = 0; j < bytesPerLine; j++)
                {
                    if (j < lineLength)
                        sb.AppendFormat("{0:X2} ", data[i + j]);
                    else
                        sb.Append("   ");

                    if (j == bytesPerLine / 2 - 1)
                        sb.Append(" ");
                }

                // ASCII representation
                if (showAscii)
                {
                    sb.Append(" | ");
                    for (var j = 0; j < lineLength; j++)
                    {
                        var b = data[i + j];
                        sb.Append(b >= 32 && b < 127 ? (char)b : '.');
                    }
                }

                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>
        /// Try parse hex string
        /// </summary>
        public static bool TryFromHexString(string hex, out byte[] result)
        {
            try
            {
                result = FromHexString(hex);
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }
    }

    /// <summary>
    /// Extension methods for byte arrays
    /// </summary>
    public static class ByteArrayExtensions
    {
        /// <summary>
        /// Check if array is all same value
        /// </summary>
        public static bool IsAll(this byte[] data, byte value)
        {
            if (data == null || data.Length == 0)
                return false;

            for (var i = 0; i < data.Length; i++)
            {
                if (data[i] != value)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Check if array is blank (all 0xFF)
        /// </summary>
        public static bool IsBlank(this byte[] data)
        {
            return IsAll(data, 0xFF);
        }

        /// <summary>
        /// Fill array with value
        /// </summary>
        public static void Fill(this byte[] data, byte value)
        {
            if (data == null)
                return;

            for (var i = 0; i < data.Length; i++)
            {
                data[i] = value;
            }
        }

        /// <summary>
        /// Compare two arrays
        /// </summary>
        public static bool SequenceEqual(this byte[] first, byte[] second)
        {
            if (first == null || second == null)
                return first == second;

            if (first.Length != second.Length)
                return false;

            for (var i = 0; i < first.Length; i++)
            {
                if (first[i] != second[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Fills a portion of an array with a specified value.
        /// (Helper for Array.Fill which is not available in .NET Framework 4.8)
        /// </summary>
        public static void ArrayFill<T>(T[] array, T value, int start, int count)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            if (start < 0 || start >= array.Length)
                throw new ArgumentOutOfRangeException(nameof(start));
            if (count < 0 || start + count > array.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            for (var i = 0; i < count; i++)
                array[start + i] = value;
        }
    }
}
