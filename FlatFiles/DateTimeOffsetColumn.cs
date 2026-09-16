using System;
using System.Buffers;
using System.Globalization;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column of DateTime values.
    /// </summary>
    public sealed class DateTimeOffsetColumn : ColumnDefinition<DateTimeOffset>
    {
        /// <summary>
        ///     Initializes a new instance of a DateTimeOffsetColumn.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        public DateTimeOffsetColumn( string columnName )
            : base( columnName )
        {
        }

        /// <summary>
        ///     Gets or sets the format string to use when parsing the date and time.
        /// </summary>
        public string? InputFormat { get; set; }

        /// <summary>
        ///     Gets or sets the format string to use when converting the value to a string.
        /// </summary>
        public string? OutputFormat { get; set; }

        /// <summary>
        ///     Gets or sets the format provider to use when parsing the date and time.
        /// </summary>
        public IFormatProvider? FormatProvider { get; set; }

        /// <inheritdoc />
        protected override DateTimeOffset OnParse( IColumnContext? context, string value )
        {
            var provider = GetFormatProvider( context, FormatProvider );
            if (InputFormat is null)
            {
                return DateTimeOffset.Parse( value, provider );
            }
            return DateTimeOffset.ParseExact( value, InputFormat, provider );
        }

        /// <inheritdoc />
        protected override string OnFormat( IColumnContext? context, DateTimeOffset value )
        {
            var provider = GetFormatProvider( context, FormatProvider );
            if (OutputFormat is null)
            {
                return value.ToString( provider );
            }
            return value.ToString( OutputFormat, provider );
        }

        /// <summary>
        ///     Formats the given value straight into the destination buffer.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to format.</param>
        /// <param name="destination">The buffer to append the formatted value to.</param>
        protected override void OnFormat( IColumnContext? context, DateTimeOffset value, IBufferWriter<char> destination )
        {
            var provider = GetFormatProvider( context, FormatProvider );
            WriteFormatted( destination, value, OutputFormat, provider );
        }
    }
}
