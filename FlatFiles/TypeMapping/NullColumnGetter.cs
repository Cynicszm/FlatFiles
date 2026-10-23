using System;
using System.Buffers;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Writes nothing but the column's own idea of nothing, for a mapping that reads a column and never
    ///     writes it.
    /// </summary>
    /// <typeparam name="TEntity">The type being written from.</typeparam>
    /// <param name="column">The column, which decides what no value looks like.</param>
    /// <remarks>
    ///     A custom mapping can be given a reader and no writer, which is a reasonable thing to declare for a
    ///     column a file carries and an entity does not keep. Nothing fills its slot in the array of values, so
    ///     the column has always been formatted from the null left sitting there, and it is formatted from a
    ///     null here.
    ///     <para>
    ///         It exists so that one such column does not take the rest of the mapping with it. Writing needs a
    ///         getter for every column or it uses none of them, and before this there was none for a column that
    ///         was never going to write anything - so every other column went back through the array on account
    ///         of one that had nothing to say.
    ///     </para>
    /// </remarks>
    internal sealed class NullColumnGetter<TEntity>( IColumnDefinition column ) : IColumnGetter<TEntity>
    {
        public void Write( IColumnContext? context, TEntity entity, IBufferWriter<char> destination )
        {
            column.Format( context, null, destination );
        }

        public object? Read( TEntity entity )
        {
            return null;
        }
    }
}
