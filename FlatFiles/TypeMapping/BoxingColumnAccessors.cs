using System;
using System.Buffers;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Reads one column onto an entity through the ordinary object path, for a column that cannot be read any
    ///     other way.
    /// </summary>
    /// <typeparam name="TEntity">The type being read into.</typeparam>
    /// <typeparam name="TMember">The type the member holds.</typeparam>
    /// <param name="column">The column to parse with.</param>
    /// <param name="assign">The member's setter.</param>
    /// <param name="member">The member, for a null reaching one that cannot hold it.</param>
    /// <remarks>
    ///     A column carrying a parsing hook, or one whose <c>Parse</c> was replaced, has to see every value as a
    ///     string or an object, so it cannot have a typed setter. Before this existed, one such column took the
    ///     whole mapping off the assembled path with it - every other column went back to being parsed into an
    ///     array of objects, because the mapping needed a setter for all of them or none. This pays for that
    ///     column and leaves the rest alone.
    ///     <para>
    ///         The value is boxed, since that is what the column produces, but the member is assigned through a
    ///         delegate rather than by reflection, so the only cost over a typed setter is the box.
    ///     </para>
    ///     <para>
    ///         Nothing about what the column does changes: the value reaches it exactly as it would have through
    ///         the array, hooks included and in the same order.
    ///     </para>
    /// </remarks>
    internal sealed class BoxingColumnSetter<TEntity, TMember>( ColumnDefinition column, Action<TEntity, TMember> assign, IMemberAccessor member )
        : IColumnSetter<TEntity>
    {
        public void Set( IColumnContext? context, TEntity entity, ReadOnlySpan<char> value )
        {
            SetObject( context, entity, column.Parse( context, value ) );
        }

        public void SetObject( IColumnContext? context, TEntity entity, object? value )
        {
            if (value is null)
            {
                // A null reaching a member that cannot hold one fails here exactly as it does through the
                // ordinary path.
                member.SetValue( entity!, null );
                return;
            }
            assign( entity, (TMember) value );
        }
    }

    /// <summary>
    ///     Writes one column from an entity through the ordinary object path, for a column that cannot be written
    ///     any other way.
    /// </summary>
    /// <typeparam name="TEntity">The type being written from.</typeparam>
    /// <typeparam name="TMember">The type the member holds.</typeparam>
    /// <param name="column">The column to format with.</param>
    /// <param name="read">Reads the member.</param>
    /// <remarks>
    ///     The writing side of <see cref="BoxingColumnSetter{TEntity, TMember}" />, and for the same reason: a
    ///     column carrying a formatting hook took every other column off the path that writes straight from an
    ///     entity.
    /// </remarks>
    internal sealed class BoxingColumnGetter<TEntity, TMember>( ColumnDefinition column, Func<TEntity, TMember> read )
        : IColumnGetter<TEntity>
    {
        public void Write( IColumnContext? context, TEntity entity, IBufferWriter<char> destination )
        {
            column.Format( context, read( entity ), destination );
        }

        public object? Read( TEntity entity )
        {
            return read( entity );
        }
    }
}
