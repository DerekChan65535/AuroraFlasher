using System.Collections.Generic;

namespace AuroraFlasher.Models
{
    /// <summary>
    /// Represents a single line in the hex dump viewer.
    /// Used for virtualized rendering of large hex dumps.
    /// </summary>
    public class HexLineData
    {
        /// <summary>
        /// The address for this line (e.g., "0000:")
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// The hex byte values for this line (up to 16 bytes)
        /// </summary>
        public List<string> ByteValues { get; set; }

        public HexLineData(string address, List<string> byteValues)
        {
            Address = address;
            ByteValues = byteValues;
        }
    }
}
