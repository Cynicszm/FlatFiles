using System;

namespace FlatFiles
{
    /// <summary>
    ///     Converts the values of a column to another type.
    /// </summary>
    /// <typeparam name="TSource">The type of the source column.</typeparam>
    /// <typeparam name="TDestination">The type to convert to.</typeparam>
    internal sealed class ConversionColumn<TSource, TDestination>(
        IColumnDefinition columnDefinition,
        Func<TSource, TDestination> parser,
        Func<TDestination, TSource> formatter ) : ColumnDefinition( columnDefinition.ColumnName, false )
    {

        /// <inheritdoc />
        public override Type ColumnType => typeof( TDestination );

        /// <inheritdoc />
        /// <remarks>
        ///     Parsing and formatting are the wrapped column's, given this column's context, so whether a provider
        ///     has to reach it is the wrapped column's answer. One from outside the library cannot be asked, and is
        ///     assumed to want it.
        /// </remarks>
        internal override bool UsesFormatProvider => columnDefinition is not ColumnDefinition inner || inner.UsesFormatProvider;

        /// <inheritdoc />
        public override object? Parse( IColumnContext? context, string value )
        {
            var sourceValue = columnDefinition.Parse( context, value );
            if (sourceValue is null)
            {
                return null;
            }
            var destinationValue = parser( (TSource) sourceValue );
            return destinationValue;
        }

        /// <inheritdoc />
        public override object? Parse( IColumnContext? context, ReadOnlySpan<char> value )
        {
            var sourceValue = columnDefinition.Parse( context, value );
            if (sourceValue is null)
            {
                return null;
            }
            var destinationValue = parser( (TSource) sourceValue );
            return destinationValue;
        }

        /// <inheritdoc />
        public override string Format( IColumnContext? context, object? value )
        {
            var destinationValue = value is null ? (object?) null : formatter( (TDestination) value );
            var sourceValue = columnDefinition.Format( context, destinationValue );
            return sourceValue;
        }
    }
}
