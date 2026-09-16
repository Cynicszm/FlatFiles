using System;
using System.Diagnostics.CodeAnalysis;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column containing enumeration values.
    /// </summary>
    /// <typeparam name="TEnum">The type of the enumeration.</typeparam>
    public sealed class EnumColumn<TEnum> : ColumnDefinition<TEnum>
    {
        /// <summary>
        ///     Initialises a new instance of an EnumColumn.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        public EnumColumn( string columnName )
            : base( columnName )
        {
        }

        /// <summary>
        ///     Gets or sets the parser used to convert string values into enumeration values. Setting null restores
        ///     the default, a case-insensitive parse of the value's name or number.
        /// </summary>
        [AllowNull]
        public Func<string, TEnum> Parser
        {
            get;
            set => field = value ?? DefaultParser;
        } = DefaultParser;

        /// <summary>
        ///     Gets or sets the formatter used to convert enumeration values into strings. Setting null restores the
        ///     default, the value's underlying number.
        /// </summary>
        [AllowNull]
        public Func<TEnum, string?> Formatter
        {
            get;
            set => field = value ?? DefaultFormatter;
        } = DefaultFormatter;

        private static TEnum DefaultParser( string value )
        {
            // The type mappers create this column for any TEnum, so it cannot demand the struct constraint the
            // generic Enum.Parse<TEnum> needs.
            return (TEnum) Enum.Parse( typeof( TEnum ), value, true );
        }

        private static string DefaultFormatter( TEnum value )
        {
            return Convert.ToInt32( value ).ToString();
        }

        /// <summary>
        ///     Parses the given value and returns the enumeration value.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <returns>The parsed enumeration value.</returns>
        protected override TEnum OnParse( IColumnContext? context, string value )
        {
            return Parser( value );
        }

        /// <summary>
        ///     Formats the given enumeration value.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The enumeration value to format.</param>
        /// <returns>The formatted value.</returns>
        protected override string OnFormat( IColumnContext? context, TEnum value )
        {
            return Formatter( value ) ?? String.Empty;
        }
    }
}
