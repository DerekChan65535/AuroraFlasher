namespace AuroraFlasher.Models
{
    /// <summary>
    /// Represents a single line in the hex dump viewer.
    /// Used for virtualized rendering of large hex dumps.
    /// </summary>
    public class HexLineData
    {
        /// <summary>
        /// The formatted hex dump line (address + hex bytes + ASCII)
        /// </summary>
        public string FormattedLine { get; set; }

        public HexLineData(string formattedLine)
        {
            FormattedLine = formattedLine;
        }
    }
}
