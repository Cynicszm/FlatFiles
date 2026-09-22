using System.Globalization;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column of signed byte values.
    /// </summary>
    public sealed class SByteColumn : NumberColumn<sbyte>
    {
        /// <summary>
        ///     Initialises a new instance of a SByteColumn.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        public SByteColumn( string columnName )
            : base( columnName, NumberStyles.Integer )
        {
        }
    }
}
