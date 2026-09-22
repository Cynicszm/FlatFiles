using System.Globalization;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column containing 32-bit integers.
    /// </summary>
    public sealed class Int32Column : NumberColumn<int>
    {
        /// <summary>
        ///     Initialises a new instance of an Int32Column.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        public Int32Column( string columnName )
            : base( columnName, NumberStyles.Integer )
        {
        }
    }
}
