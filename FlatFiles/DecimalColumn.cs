using System.Globalization;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column containing decimals.
    /// </summary>
    public sealed class DecimalColumn : NumberColumn<decimal>
    {
        /// <summary>
        ///     Initializes a new instance of a DecimalColumn.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        public DecimalColumn( string columnName )
            : base( columnName, NumberStyles.Number )
        {
        }
    }
}
