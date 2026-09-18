using System;
using System.Buffers;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column of <see cref="TimeOnly" /> values: a time of day with no date and no offset.
    /// </summary>
    /// <param name="columnName">The name of the column.</param>
    public sealed class TimeOnlyColumn( string columnName ) : ColumnDefinition<TimeOnly>( columnName )
    {
        /// <summary>
        ///     Gets or sets the format string to use when parsing the time. When null, the time is parsed in any form
        ///     the format provider accepts.
        /// </summary>
        public string? InputFormat { get; set; }

        /// <summary>
        ///     Gets or sets the format string to use when converting the value to a string.
        /// </summary>
        public string? OutputFormat { get; set; }

        /// <summary>
        ///     Gets or sets the format provider to use when parsing and formatting the time. When null, the options'
        ///     provider is used, then the current culture.
        /// </summary>
        public IFormatProvider? FormatProvider { get; set; }

        /// <summary>
        ///     Parses the given value and returns a TimeOnly instance.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <returns>The parsed TimeOnly instance.</returns>
        protected override TimeOnly OnParse( IColumnContext? context, string value )
        {
            var provider = GetFormatProvider( context, FormatProvider );
            return InputFormat is null ? TimeOnly.Parse( value, provider ) : TimeOnly.ParseExact( value, InputFormat, provider );
        }

        /// <summary>
        ///     Formats the given object.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The object to format.</param>
        /// <returns>The formatted value.</returns>
        protected override string OnFormat( IColumnContext? context, TimeOnly value )
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
        protected override void OnFormat( IColumnContext? context, TimeOnly value, IBufferWriter<char> destination )
        {
            var provider = GetFormatProvider( context, FormatProvider );
            WriteFormatted( destination, value, OutputFormat, provider );
        }
    }
}
