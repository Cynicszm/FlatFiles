using System.Globalization;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column containing unsigned 32-bit integers.
    /// </summary>
    public sealed class UInt32Column : NumberColumn<uint>
    {
        /// <summary>
        ///     Initializes a new instance of an UInt32Column.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        public UInt32Column( string columnName )
            : base( columnName, NumberStyles.Integer )
        {
        }
    }
}
