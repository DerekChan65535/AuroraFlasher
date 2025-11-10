using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AuroraFlasher.Models
{
    /// <summary>
    /// Represents a single hex byte cell in the hex dump viewer.
    /// Reads directly from memory buffer on-demand (no caching).
    /// </summary>
    public class HexByteCell : INotifyPropertyChanged
    {
        private readonly byte[] _memoryBuffer;
        private readonly bool[] _validityFlags;
        private readonly int _offset;
        private readonly object _bufferLock;

        /// <summary>
        /// Gets the hex value from the memory buffer on-demand
        /// </summary>
        public string Value
        {
            get
            {
                if (_memoryBuffer == null || _validityFlags == null || 
                    _offset < 0 || _offset >= _memoryBuffer.Length)
                    return "";

                lock (_bufferLock)
                {
                    // Show blank for unread bytes
                    if (!_validityFlags[_offset])
                        return "";
                    
                    // Show blank for 0xFF (erased/empty flash)
                    if (_memoryBuffer[_offset] == 0xFF)
                        return "";
                        
                    return $"{_memoryBuffer[_offset]:X2}";
                }
            }
        }

        public HexByteCell(byte[] memoryBuffer, bool[] validityFlags, int offset, object bufferLock)
        {
            _memoryBuffer = memoryBuffer;
            _validityFlags = validityFlags;
            _offset = offset;
            _bufferLock = bufferLock;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notify UI that value may have changed (called when memory is updated)
        /// </summary>
        public void Refresh()
        {
            OnPropertyChanged(nameof(Value));
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

