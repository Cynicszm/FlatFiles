using System;
using System.Buffers;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Takes a column's value from an entity through a writer the caller supplied, for a mapping declared with
    ///     <see cref="ICustomMapping{TEntity}.WithWriter{TProp}(Func{TEntity, TProp})" /> rather than as a property.
    /// </summary>
    /// <typeparam name="TEntity">The type being written from.</typeparam>
    /// <param name="column">The column to format with, where this getter is asked to write the value itself.</param>
    /// <param name="write">The writer the mapping was declared with.</param>
    /// <remarks>
    ///     The reading side is <see cref="CustomColumnSetter{TEntity}" />, and this is here for the same reason: a
    ///     custom mapping produced no getter, and a mapping needs one for every column or it uses none of them, so
    ///     one of them put every other column back through the array of values.
    ///     <para>
    ///         Only a writer that returns a value can be reached this way. The other form is handed the whole
    ///         array of values and writes into it wherever it likes, so there is nothing to ask it for without an
    ///         array to give it, and a mapping declared with one still writes as it always did.
    ///     </para>
    /// </remarks>
    internal sealed class CustomColumnGetter<TEntity>( IColumnDefinition column, Func<IColumnContext?, object?, object?> write )
        : IColumnGetter<TEntity>
    {
        /// <summary>
        ///     Not the route the schema takes - it asks for the value and formats it - but this has to mean the
        ///     same thing as that if anything else ever reaches it.
        /// </summary>
        public void Write( IColumnContext? context, TEntity entity, IBufferWriter<char> destination )
        {
            column.Format( context, write( context, entity ), destination );
        }

        public object? Read( TEntity entity )
        {
            return write( null, entity );
        }

        public object? ReadWith( IColumnContext? context, TEntity entity )
        {
            return write( context, entity );
        }

        public bool NeedsColumnContext => true;
    }
}
