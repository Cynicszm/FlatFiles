using System;
using System.Buffers;

namespace FlatFiles
{
    /// <summary>
    ///     Represents a column whose data is not sourced by the input file.
    /// </summary>
    /// <typeparam name="T">The type of the metadata.</typeparam>
    public abstract class MetadataColumn<T> : ColumnDefinition, IMetadataColumn
    {
        /// <summary>
        ///     Initializes a new instance of a MetadataColumn.
        /// </summary>
        /// <param name="columnName">The name of the metadata column.</param>
        protected MetadataColumn( string columnName )
            : base( columnName )
        {
        }

        /// <summary>
        ///     Gets the type of the values in the column.
        /// </summary>
        public override Type ColumnType => typeof( T );

        /// <summary>
        ///     Formats the given object.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The object to format.</param>
        /// <returns>The formatted value.</returns>
        public sealed override string Format( IColumnContext? context, object? value )
        {
            return OnFormat( context ) ?? string.Empty;
        }

        /// <summary>
        ///     Formats the given object.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <returns>The formatted value.</returns>
        protected abstract string? OnFormat( IColumnContext? context );

        /// <summary>
        ///     Formats the metadata and appends it to a buffer.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">Ignored; the value comes from the context.</param>
        /// <param name="destination">The buffer to append the formatted value to.</param>
        public sealed override void Format( IColumnContext? context, object? value, IBufferWriter<char> destination )
        {
            ArgumentNullException.ThrowIfNull( destination );
            OnFormat( context, destination );
        }

        /// <summary>
        ///     Formats the metadata and appends it to a buffer. The default writes the string that
        ///     <see cref="OnFormat(IColumnContext?)" /> returns.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="destination">The buffer to append the formatted value to.</param>
        protected virtual void OnFormat( IColumnContext? context, IBufferWriter<char> destination )
        {
            destination.Write( (OnFormat( context ) ?? string.Empty).AsSpan() );
        }

        /// <summary>
        ///     Parses the given value and returns the parsed object.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <param name="value">The value to parse.</param>
        /// <returns>The parsed value.</returns>
        public sealed override object? Parse( IColumnContext? context, string value )
        {
            return OnParse( context );
        }

        /// <summary>
        ///     Parses the given value and returns the parsed object.
        /// </summary>
        /// <param name="context">Holds information about the column current being processed.</param>
        /// <returns>The parsed value.</returns>
        protected abstract T OnParse( IColumnContext? context );
    }
}
