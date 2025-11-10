using System;
using System.Collections.ObjectModel;

namespace AuroraFlasher.Models
{
    /// <summary>
    /// Represents a single line in the hex dump viewer.
    /// Reads directly from memory buffer on-demand (no storage of hex strings).
    /// </summary>
    public class HexLineData
    {
        private readonly byte[] _memoryBuffer;
        private readonly bool[] _validityFlags;
        private readonly int _startOffset;
        private readonly int _length;
        private readonly object _bufferLock;
        
        /// <summary>
        /// The address for this line (e.g., "0000:")
        /// </summary>
        public string Address { get; }

        /// <summary>
        /// The hex byte cells for this line (up to 16 bytes)
        /// Reads from buffer on-demand when accessed
        /// </summary>
        public ObservableCollection<HexByteCell> ByteValues { get; }

        public HexLineData(byte[] memoryBuffer, bool[] validityFlags, int startOffset, int length, object bufferLock)
        {
            _memoryBuffer = memoryBuffer;
            _validityFlags = validityFlags;
            _startOffset = startOffset;
            _length = length;
            _bufferLock = bufferLock;
            
            Address = $"{startOffset:X4}:";
            
            // Create cells that read from buffer
            ByteValues = new ObservableCollection<HexByteCell>();
            for (var i = 0; i < length; i++)
            {
                ByteValues.Add(new HexByteCell(memoryBuffer, validityFlags, startOffset + i, bufferLock));
            }
        }
    }
}
