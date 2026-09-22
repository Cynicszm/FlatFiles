using System.Globalization;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column containing unsigned 16-bit integers.
    /// </summary>
    public sealed class UInt16Column : NumberColumn<ushort>
    {
        /// <summary>
        ///     Initialises a new instance of an UInt16Column.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        public UInt16Column( string columnName )
            : base( columnName, NumberStyles.Integer )
        {
        }
    }
}
