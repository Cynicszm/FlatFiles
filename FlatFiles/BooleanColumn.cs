using System;
using System.Buffers;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column containing boolean values.
    /// </summary>
    public sealed class BooleanColumn : ColumnDefinition<bool>
    {
        /// <summary>
        ///     Initializes a new instance of a BooleanColumn.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        public BooleanColumn( string columnName )
            : base( columnName )
        {
        }

        /// <summary>
        ///     Gets or sets the value representing true.
        /// </summary>
        public string? TrueString { get; set; } = bool.TrueString;

        /// <summary>
        ///     Gets or sets the value representing false.
        /// </summary>
        public string? FalseString { get; set; } = bool.FalseString;

        /// <summary>
        ///     Parses the given value into its equivalent boolean value.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <returns>True if the value equals the TrueString; otherwise, false.</returns>
        protected override bool OnParse( IColumnContext? context, string value )
        {
            if (string.Equals( value, TrueString, StringComparison.CurrentCultureIgnoreCase ))
            {
                return true;
            }

            return string.Equals( value, FalseString, StringComparison.CurrentCultureIgnoreCase ) ?
                false :
                throw new InvalidCastException();
        }

        /// <summary>
        ///     Parses the given value into its equivalent boolean value, without copying it out of the record it sits
        ///     in.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <returns>True if the value equals the TrueString; otherwise, false.</returns>
        protected override bool OnParse( IColumnContext? context, ReadOnlySpan<char> value )
        {
            if (TrueString is not null && value.Equals( TrueString, StringComparison.CurrentCultureIgnoreCase ))
            {
                return true;
            }

            return FalseString is not null && value.Equals( FalseString, StringComparison.CurrentCultureIgnoreCase ) ?
                false :
                throw new InvalidCastException();
        }

        /// <summary>
        ///     Formats the given object.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The object to format.</param>
        /// <returns>The formatted value.</returns>
        protected override string OnFormat( IColumnContext? context, bool value )
        {
            var formatted = value ? TrueString : FalseString;
            return formatted ?? string.Empty;
        }

        /// <summary>
        ///     Formats the given value straight into the destination buffer.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to format.</param>
        /// <param name="destination">The buffer to append the formatted value to.</param>
        protected override void OnFormat( IColumnContext? context, bool value, IBufferWriter<char> destination )
        {
            destination.Write( ((value ? TrueString : FalseString) ?? string.Empty).AsSpan() );
        }
    }
}
