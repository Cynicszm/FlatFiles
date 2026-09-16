using System.Globalization;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column containing singles.
    /// </summary>
    public sealed class SingleColumn : NumberColumn<float>
    {
        /// <summary>
        ///     Initializes a new instance of a SingleColumn.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        public SingleColumn( string columnName )
            : base( columnName, NumberStyles.Float )
        {
        }
    }
}
