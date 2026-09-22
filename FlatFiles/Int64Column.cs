using System.Globalization;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column containing 64-bit integers.
    /// </summary>
    public sealed class Int64Column : NumberColumn<long>
    {
        /// <summary>
        ///     Initialises a new instance of an Int64Column.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        public Int64Column( string columnName )
            : base( columnName, NumberStyles.Integer )
        {
        }
    }
}
