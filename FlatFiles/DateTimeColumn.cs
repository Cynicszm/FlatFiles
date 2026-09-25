using System;
using System.Buffers;
using System.Globalization;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column of DateTime values.
    /// </summary>
    public sealed class DateTimeColumn : ColumnDefinition<DateTime>
    {
        /// <summary>
        ///     Initialises a new instance of a DateTimeColumn.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        public DateTimeColumn( string columnName )
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

        /// <summary>
        ///     Parses the given value and returns a DateTime instance.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <returns>The parsed DateTime instance.</returns>
        protected override DateTime OnParse( IColumnContext? context, string value )
        {
            var provider = FormatProvider ?? CultureInfo.CurrentCulture;
            return InputFormat is null ? DateTime.Parse( value, provider ) : DateTime.ParseExact( value, InputFormat, provider );
        }

        /// <summary>
        ///     Parses the given value without copying it out of the record it sits in.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <returns>The parsed value.</returns>
        protected override DateTime OnParse( IColumnContext? context, ReadOnlySpan<char> value )
        {
            var provider = FormatProvider ?? CultureInfo.CurrentCulture;
            return InputFormat is null ? DateTime.Parse( value, provider ) : DateTime.ParseExact( value, InputFormat, provider );
        }

        /// <summary>
        ///     Formats the given object.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The object to format.</param>
        /// <returns>The formatted value.</returns>
        protected override string OnFormat( IColumnContext? context, DateTime value )
        {
            var provider = GetFormatProvider( context, FormatProvider );
            return value.ToString( OutputFormat, provider );
        }

        /// <summary>
        ///     Formats the given value straight into the destination buffer.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to format.</param>
        /// <param name="destination">The buffer to append the formatted value to.</param>
        protected override void OnFormat( IColumnContext? context, DateTime value, IBufferWriter<char> destination )
        {
            var provider = GetFormatProvider( context, FormatProvider );
            WriteFormatted( destination, value, OutputFormat, provider );
        }

        /// <inheritdoc />
        /// <remarks>It asks, so a provider on the options has to reach it.</remarks>
        internal override bool UsesFormatProvider => true;
    }
}
