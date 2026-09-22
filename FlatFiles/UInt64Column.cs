using System.Globalization;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column containing unsigned 64-bit integers.
    /// </summary>
    public sealed class UInt64Column : NumberColumn<ulong>
    {
        /// <summary>
        ///     Initialises a new instance of an UInt64Column.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        public UInt64Column( string columnName )
            : base( columnName, NumberStyles.Integer )
        {
        }
    }
}
