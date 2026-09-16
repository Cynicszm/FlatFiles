using System.Globalization;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column of byte values.
    /// </summary>
    public sealed class ByteColumn : NumberColumn<byte>
    {
        /// <summary>
        ///     Initializes a new instance of a ByteColumn.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        public ByteColumn( string columnName )
            : base( columnName, NumberStyles.Integer )
        {
        }
    }
}
