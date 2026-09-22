using System;

namespace FlatFiles
{
    /// <summary>
    ///     Provides factory methods for generating instances of <see cref="INullFormatter"/>.
    /// </summary>
    public sealed class NullFormatter : INullFormatter
    {
        /// <summary>
        ///     Creates a new <see cref="INullFormatter"/> that treats solid whitespace as null.
        /// </summary>
        public static readonly INullFormatter Default = new NullFormatter( null, true );

        private readonly string? nullValue;
        private readonly bool matchesWhiteSpace;

        private NullFormatter( string? nullValue, bool matchesWhiteSpace )
        {
            this.nullValue = nullValue;
            this.matchesWhiteSpace = matchesWhiteSpace;
        }

        /// <summary>
        ///     Creates a new <see cref="INullFormatter"/> that uses the given value to represent null.
        /// </summary>
        /// <param name="value">The constant used to represent null in the flat file.</param>
        /// <returns>An object for configuring how nulls are handled.</returns>
        public static NullFormatter ForValue( string? value )
        {
            return new NullFormatter( value, false );
        }

        /// <inheritdoc/>
        public bool IsNullValue( IColumnContext? context, string? value )
        {
            return matchesWhiteSpace ? string.IsNullOrWhiteSpace( value ) : value is null || value == nullValue;
        }

        /// <inheritdoc/>
        /// <remarks>
        ///     Text taken from a record is never a null reference, so a formatter built from a null value matches
        ///     nothing here, exactly as the string overload does for a value read from a file.
        /// </remarks>
        public bool IsNullValue( IColumnContext? context, ReadOnlySpan<char> value )
        {
            return matchesWhiteSpace ? value.IsWhiteSpace() : nullValue is not null && value.SequenceEqual( nullValue );
        }

        /// <inheritdoc/>
        public string? FormatNull( IColumnContext? context )
        {
            return matchesWhiteSpace ? string.Empty : nullValue;
        }
    }
}
