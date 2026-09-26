using System;
using System.Buffers;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column of <see cref="DateOnly" /> values: a calendar date with no time of day and no offset.
    /// </summary>
    /// <param name="columnName">The name of the column.</param>
    public sealed class DateOnlyColumn( string columnName ) : ColumnDefinition<DateOnly>( columnName )
    {
        /// <summary>
        ///     Gets or sets the format string to use when parsing the date. When null, the date is parsed in any form
        ///     the format provider accepts.
        /// </summary>
        public string? InputFormat { get; set; }

        /// <summary>
        ///     Gets or sets the format string to use when converting the value to a string.
        /// </summary>
        public string? OutputFormat { get; set; }

        /// <summary>
        ///     Gets or sets the format provider to use when parsing and formatting the date. When null, the options'
        ///     provider is used, then the current culture.
        /// </summary>
        public IFormatProvider? FormatProvider { get; set; }

        /// <summary>
        ///     Parses the given value and returns a DateOnly instance.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <returns>The parsed DateOnly instance.</returns>
        protected override DateOnly OnParse( IColumnContext? context, string value )
        {
            var provider = GetFormatProvider( context, FormatProvider );
            return InputFormat is null ? DateOnly.Parse( value, provider ) : DateOnly.ParseExact( value, InputFormat, provider );
        }

        /// <summary>
        ///     Parses the given value without copying it out of the record it sits in.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <returns>The parsed value.</returns>
        protected override DateOnly OnParse( IColumnContext? context, ReadOnlySpan<char> value )
        {
            var provider = GetFormatProvider( context, FormatProvider );
            return InputFormat is null ? DateOnly.Parse( value, provider ) : DateOnly.ParseExact( value, InputFormat, provider );
        }

        /// <summary>
        ///     Formats the given object.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The object to format.</param>
        /// <returns>The formatted value.</returns>
        protected override string OnFormat( IColumnContext? context, DateOnly value )
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
        protected override void OnFormat( IColumnContext? context, DateOnly value, IBufferWriter<char> destination )
        {
            var provider = GetFormatProvider( context, FormatProvider );
            WriteFormatted( destination, value, OutputFormat, provider );
        }

        /// <inheritdoc />
        /// <remarks>It asks, so a provider on the options has to reach it.</remarks>
        internal override bool UsesFormatProvider => true;

        /// <inheritdoc />
        internal override bool TrySetInputFormat( string format )
        {
            InputFormat = format;
            return true;
        }

        /// <inheritdoc />
        internal override bool TrySetOutputFormat( string format )
        {
            OutputFormat = format;
            return true;
        }
    }
}
