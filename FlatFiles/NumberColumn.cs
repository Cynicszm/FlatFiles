using System;
using System.Buffers;
using System.Globalization;
using System.Numerics;

namespace FlatFiles
{
    /// <summary>
    ///     The base for the columns holding a numeric type. Parsing and formatting go through the generic maths
    ///     interfaces the numeric types implement, so a concrete column only names its type and the number styles it
    ///     accepts by default.
    /// </summary>
    /// <typeparam name="T">The numeric type of the values in the column.</typeparam>
    public abstract class NumberColumn<T> : ColumnDefinition<T>
        where T : struct, INumber<T>
    {
        /// <summary>
        ///     Initialises a new instance of a NumberColumn.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        /// <param name="numberStyles">The number styles accepted when parsing unless the caller sets others.</param>
        protected NumberColumn( string columnName, NumberStyles numberStyles )
            : base( columnName )
        {
            NumberStyles = numberStyles;
        }

        /// <summary>
        ///     Gets or sets the format provider to use when parsing.
        /// </summary>
        public IFormatProvider? FormatProvider { get; set; }

        /// <summary>
        ///     Gets or sets the number styles to use when parsing.
        /// </summary>
        public NumberStyles NumberStyles { get; set; }

        /// <summary>
        ///     Gets or sets the format string to use when converting the value to a string.
        /// </summary>
        public string? OutputFormat { get; set; }

        /// <summary>
        ///     Parses the given value.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <returns>The parsed value.</returns>
        protected override T OnParse( IColumnContext? context, string value )
        {
            var provider = GetFormatProvider( context, FormatProvider );
            return T.Parse( value, NumberStyles, provider );
        }

        /// <summary>
        ///     Formats the given value.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to format.</param>
        /// <returns>The formatted value.</returns>
        protected override string OnFormat( IColumnContext? context, T value )
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
        protected override void OnFormat( IColumnContext? context, T value, IBufferWriter<char> destination )
        {
            var provider = GetFormatProvider( context, FormatProvider );
            WriteFormatted( destination, value, OutputFormat, provider );
        }
    }
}
