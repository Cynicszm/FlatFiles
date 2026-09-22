using System;
using System.Buffers;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column of Guid values.
    /// </summary>
    public sealed class GuidColumn : ColumnDefinition<Guid>
    {
        /// <summary>
        ///     Initialises a new instance of a GuidColumn.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        public GuidColumn( string columnName )
            : base( columnName )
        {
        }

        /// <summary>
        ///     Gets or sets the format string to use when parsing the Guid.
        /// </summary>
        public string? InputFormat { get; set; }

        /// <summary>
        ///     Gets or sets the format string to use when converting the value to a string.
        /// </summary>
        public string? OutputFormat { get; set; }

        /// <summary>
        ///     Parses the given value and returns a Guid instance.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <returns>The parsed Guid.</returns>
        protected override Guid OnParse( IColumnContext? context, string value )
        {
            return InputFormat is null ? Guid.Parse( value ) : Guid.ParseExact( value, InputFormat );
        }

        /// <summary>
        ///     Parses the given value without copying it out of the record it sits in.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <returns>The parsed value.</returns>
        protected override Guid OnParse( IColumnContext? context, ReadOnlySpan<char> value )
        {
            return InputFormat is null ? Guid.Parse( value ) : Guid.ParseExact( value, InputFormat );
        }

        /// <summary>
        ///     Formats the given object.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The object to format.</param>
        /// <returns>The formatted value.</returns>
        protected override string OnFormat( IColumnContext? context, Guid value )
        {
            return value.ToString( OutputFormat );
        }

        /// <summary>
        ///     Formats the given value straight into the destination buffer.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to format.</param>
        /// <param name="destination">The buffer to append the formatted value to.</param>
        protected override void OnFormat( IColumnContext? context, Guid value, IBufferWriter<char> destination )
        {
            WriteFormatted( destination, value, OutputFormat, null );
        }
    }
}
