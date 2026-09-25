using System;
using System.Buffers;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Takes a member's value from an entity and formats it, without the value becoming an
    ///     <see cref="object" /> on the way.
    /// </summary>
    /// <typeparam name="TEntity">The type being written from.</typeparam>
    internal interface IColumnGetter<in TEntity>
    {
        void Write( IColumnContext? context, TEntity entity, IBufferWriter<char> destination );

        /// <summary>
        ///     The member's value as an object, for the one place that needs it as one: an error handler being
        ///     told what could not be formatted. A box per failure rather than per value.
        /// </summary>
        object? Read( TEntity entity );
    }

    /// <summary>
    ///     Formats a member of the same type as the column it is written to.
    /// </summary>
    /// <typeparam name="TEntity">The type being written from.</typeparam>
    /// <typeparam name="T">The type the member holds and the column formats.</typeparam>
    /// <param name="column">The column to format with.</param>
    /// <param name="read">Reads the member.</param>
    internal sealed class ColumnGetter<TEntity, T>( ColumnDefinition<T> column, Func<TEntity, T> read ) : IColumnGetter<TEntity>
    {
        public void Write( IColumnContext? context, TEntity entity, IBufferWriter<char> destination )
        {
            column.FormatTyped( context, read( entity ), destination );
        }

        public object? Read( TEntity entity )
        {
            return read( entity );
        }
    }

    /// <summary>
    ///     Formats a member that holds the nullable form of what the column writes. Holding nothing is what the
    ///     column's null formatter is for, and is the one case the typed path has to decide for itself.
    /// </summary>
    /// <typeparam name="TEntity">The type being written from.</typeparam>
    /// <typeparam name="T">The type the column formats.</typeparam>
    /// <param name="column">The column to format with.</param>
    /// <param name="read">Reads the member.</param>
    internal sealed class NullableColumnGetter<TEntity, T>( ColumnDefinition<T> column, Func<TEntity, T?> read ) : IColumnGetter<TEntity>
        where T : struct
    {
        public void Write( IColumnContext? context, TEntity entity, IBufferWriter<char> destination )
        {
            var value = read( entity );
            if (value.HasValue)
            {
                column.FormatTyped( context, value.Value, destination );
                return;
            }
            column.FormatNull( context, destination );
        }

        public object? Read( TEntity entity )
        {
            return read( entity );
        }
    }
}
