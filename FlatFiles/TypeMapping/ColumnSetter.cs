using System;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Puts a column's value on a member of the same type.
    /// </summary>
    /// <typeparam name="TEntity">The type being read into.</typeparam>
    /// <typeparam name="T">The type the column reads and the member holds.</typeparam>
    /// <param name="column">The column to parse with.</param>
    /// <param name="setter">The member's setter.</param>
    /// <param name="member">The member, for the rare value that has to travel as an object.</param>
    internal sealed class ColumnSetter<TEntity, T>( ColumnDefinition<T> column, Action<TEntity, T> setter, IMemberAccessor member )
        : IColumnSetter<TEntity>
    {
        public void Set( IColumnContext? context, TEntity entity, ReadOnlySpan<char> value )
        {
            if (column.ParseTyped( context, value, out var parsed ))
            {
                setter( entity, parsed );
                return;
            }
            // A null reaching a member that cannot hold one fails here exactly as it does through the ordinary path.
            member.SetValue( entity!, null );
        }

        public void SetObject( TEntity entity, object? value )
        {
            member.SetValue( entity!, value );
        }
    }
}
