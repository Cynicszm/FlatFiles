using System;

namespace FlatFiles.TypeMapping
{
    internal interface IMapper
    {
        IMemberAccessor? Member { get; }

        int LogicalCount { get; }

        Func<IRecordContext, object?[], object?> GetReader();

        Action<IRecordContext, object?, object?[]> GetWriter();

        /// <summary>
        ///     Something to read a record onto an entity with, without any value becoming an <see cref="object" />,
        ///     or null where this mapping cannot be read that way. For a reader that knows the entity only as an
        ///     object, which is what a mapper selector leaves it as.
        /// </summary>
        /// <returns>The assembler, or null to read through the array of parsed values as before.</returns>
        IObjectAssembler? GetAssembler();
    }

    internal interface IMapper<TEntity> : IMapper
    {
        new Func<IRecordContext, object?[], TEntity> GetReader();

        new Action<IRecordContext, TEntity, object?[]> GetWriter();

        /// <summary>
        ///     One setter per column, for reading a record onto an entity without any value becoming an
        ///     <see cref="object" />, or null where this mapping cannot be read that way.
        /// </summary>
        /// <returns>The setters, or null to read through the array of parsed values as before.</returns>
        IColumnSetter<TEntity>[]? GetColumnSetters();

        /// <summary>
        ///     One getter per column, for writing a record from an entity without any value becoming an
        ///     <see cref="object" />, or null where this mapping cannot be written that way.
        /// </summary>
        /// <returns>The getters, or null to write through the array of values as before.</returns>
        IColumnGetter<TEntity>[]? GetColumnGetters();

        /// <summary>
        ///     Makes an entity for a record to be read onto.
        /// </summary>
        /// <returns>A new entity.</returns>
        TEntity CreateEntity();
    }
}
