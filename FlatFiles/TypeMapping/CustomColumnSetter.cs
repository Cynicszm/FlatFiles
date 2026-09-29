using System;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Puts a column's value on an entity through a reader the caller supplied, for a mapping declared with
    ///     <see cref="ICustomMapping{TEntity}.WithReader(Action{TEntity, object?})" /> rather than as a property.
    /// </summary>
    /// <typeparam name="TEntity">The type being read into.</typeparam>
    /// <param name="column">The column to parse with, where this setter is asked to read the value itself.</param>
    /// <param name="read">The reader the mapping was declared with.</param>
    /// <remarks>
    ///     A custom reader takes its value as an <see cref="object" />, so this column boxes. The point of having
    ///     it at all is that it no longer takes the rest of the mapping with it: before this existed, a mapping
    ///     needed a setter for every column or it used none of them, and a custom mapping produced none - so one
    ///     of them put every other column back through the array of parsed values. A column whose value comes from
    ///     the context rather than the record can only be mapped this way, so asking for the record number was
    ///     enough to do it.
    ///     <para>
    ///         The reader is given a context of its own, which is what it was given when the whole mapping went
    ///         through the array, and the value is parsed by the schema rather than here - see
    ///         <see cref="NeedsParsedValue" />. Both are so that this column is answered exactly as it was.
    ///     </para>
    /// </remarks>
    internal sealed class CustomColumnSetter<TEntity>( IColumnDefinition column, Action<IColumnContext?, object?, object?> read )
        : IColumnSetter<TEntity>
    {
        /// <summary>
        ///     Not the route the schema takes - it parses the value and calls <see cref="SetObject" /> - but this
        ///     has to mean the same thing as that if anything else ever reaches it.
        /// </summary>
        public void Set( IColumnContext? context, TEntity entity, ReadOnlySpan<char> value )
        {
            read( context, entity, column.Parse( context, value ) );
        }

        public void SetObject( IColumnContext? context, TEntity entity, object? value )
        {
            read( context, entity, value );
        }

        public bool NeedsParsedValue => true;
    }
}
