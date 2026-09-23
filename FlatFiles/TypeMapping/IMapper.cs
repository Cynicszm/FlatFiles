using System;

namespace FlatFiles.TypeMapping
{
    internal interface IMapper
    {
        IMemberAccessor? Member { get; }

        int LogicalCount { get; }

        Func<IRecordContext, object?[], object?> GetReader();

        Action<IRecordContext, object?, object?[]> GetWriter();
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
        ///     Makes an entity for a record to be read onto.
        /// </summary>
        /// <returns>A new entity.</returns>
        TEntity CreateEntity();
    }
}
