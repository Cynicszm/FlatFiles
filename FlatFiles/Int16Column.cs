using System.Globalization;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column containing 16-bit integers.
    /// </summary>
    public sealed class Int16Column : NumberColumn<short>
    {
        /// <summary>
        ///     Initializes a new instance of an Int16Column.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        public Int16Column( string columnName )
            : base( columnName, NumberStyles.Integer )
        {
        }
    }
}
