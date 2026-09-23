using System;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Puts a column's value on a member that holds the nullable form of it. The column reads
    ///     <typeparamref name="T" /> and the member takes <typeparamref name="T" />?, which costs nothing: the
    ///     conversion is a wrap, not a box.
    /// </summary>
    /// <typeparam name="TEntity">The type being read into.</typeparam>
    /// <typeparam name="T">The type the column reads.</typeparam>
    /// <param name="column">The column to parse with.</param>
    /// <param name="setter">The member's setter.</param>
    internal sealed class NullableColumnSetter<TEntity, T>( ColumnDefinition<T> column, Action<TEntity, T?> setter )
        : IColumnSetter<TEntity>
        where T : struct
    {
        public void Set( IColumnContext? context, TEntity entity, ReadOnlySpan<char> value )
        {
            setter( entity, column.ParseTyped( context, value, out var parsed ) ? parsed : null );
        }

        public void SetObject( TEntity entity, object? value )
        {
            setter( entity, (T?) value );
        }
    }
}
