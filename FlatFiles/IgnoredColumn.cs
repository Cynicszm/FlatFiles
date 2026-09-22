using System;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column that should be ignored when reading a document and used as a placeholder
    ///     when writing a document.
    /// </summary>
    public sealed class IgnoredColumn : ColumnDefinition
    {
        /// <summary>
        ///     Initialises a new IgnoredColumn.
        /// </summary>
        public IgnoredColumn() 
            : base( string.Empty, true )
        {
        }

        /// <summary>
        ///     Initialises a new IgnoredColumn with a header name.
        /// </summary>
        /// <param name="columnName"></param>
        public IgnoredColumn( string columnName )
            : base( columnName, true )
        {
        }

        /// <summary>
        ///     Gets the type of data in the column.
        /// </summary>
        public override Type ColumnType => typeof( string );

        /// <summary>
        ///     Ignores the values that was parsed from the document.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value that was parsed from the document.</param>
        /// <returns>A null.</returns>
        public override object? Parse( IColumnContext? context, string value )
        {
            if (OnParsing is not null)
            {
                _ = OnParsing( context, value );
            }
            object? result = null;
            if (OnParsed is not null)
            {
                result = OnParsed( context, result );
            }
            return result;
        }

        /// <summary>
        ///     Ignores the value that was parsed from the document, without copying it out of the record it sits in
        ///     unless a hook is waiting to be handed it.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value that was parsed from the document.</param>
        /// <returns>A null.</returns>
        public override object? Parse( IColumnContext? context, ReadOnlySpan<char> value )
        {
            if (OnParsing is not null)
            {
                return Parse( context, value.ToString() );
            }
            return OnParsed is null ? null : OnParsed( context, null );
        }

        /// <summary>
        ///     Returns null so nothing is written to the document.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value that needs written to the document.</param>
        /// <returns>A null.</returns>
        public override string Format( IColumnContext? context, object? value )
        {
            if (OnFormatting is not null)
            {
                _ = OnFormatting( context, value );
            }
            var result = NullFormatter.FormatNull( context ) ?? string.Empty;
            if (OnFormatted is not null)
            {
                result = OnFormatted( context, result ) ?? string.Empty;
            }
            return result;
        }
    }
}
