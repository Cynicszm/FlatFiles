using System;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Parses one column's value and puts it on the entity, without the value ever being an
    ///     <see cref="object" />. A mapper builds one of these per column when every column and member it maps
    ///     allows it, and the schema calls them in place of filling an array of parsed values.
    /// </summary>
    /// <typeparam name="TEntity">The type being read into.</typeparam>
    internal interface IColumnSetter<in TEntity>
    {
        /// <summary>
        ///     Parses the value and puts it on the entity.
        /// </summary>
        /// <param name="context">The column context, or null where none was built.</param>
        /// <param name="entity">The entity being read into.</param>
        /// <param name="value">The characters of the value.</param>
        void Set( IColumnContext? context, TEntity entity, ReadOnlySpan<char> value );

        /// <summary>
        ///     Puts a value that something else has already produced on the entity, which is how a substituted
        ///     value from an error handler arrives.
        /// </summary>
        /// <param name="entity">The entity being read into.</param>
        /// <param name="value">The value to put on it.</param>
        void SetObject( TEntity entity, object? value );
    }
}
