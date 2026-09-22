using System.Globalization;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column of byte values.
    /// </summary>
    public sealed class ByteColumn : NumberColumn<byte>
    {
        /// <summary>
        ///     Initialises a new instance of a ByteColumn.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        public ByteColumn( string columnName )
            : base( columnName, NumberStyles.Integer )
        {
        }
    }
}
