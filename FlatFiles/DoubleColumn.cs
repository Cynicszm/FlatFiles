using System.Globalization;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column containing doubles.
    /// </summary>
    public sealed class DoubleColumn : NumberColumn<double>
    {
        /// <summary>
        ///     Initialises a new instance of a DoubleColumn.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        public DoubleColumn( string columnName )
            : base( columnName, NumberStyles.Float )
        {
        }
    }
}
