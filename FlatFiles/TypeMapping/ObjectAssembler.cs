using System;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Reads a record onto an entity and holds it until it is taken, for a reader that knows the entity only
    ///     as an <see cref="object" />. A mapper selector needs one of these per mapper it selects between,
    ///     because which one a record belongs to is not known until the record is read.
    /// </summary>
    internal interface IObjectAssembler : IEntityAssembler
    {
        /// <summary>
        ///     The entity the last record was read onto, which is taken once.
        /// </summary>
        /// <returns>The entity.</returns>
        object? Take();
    }

    /// <summary>
    ///     What <see cref="IObjectAssembler" /> describes, for one mapper. The setters are built once, as they are
    ///     for a typed reader; what changes per record is only the entity.
    /// </summary>
    internal sealed class ObjectAssembler<TEntity>( IMapper<TEntity> mapper, IColumnSetter<TEntity>[] setters ) : IObjectAssembler
    {
        private TEntity? assembled;

        public void Assemble( IRecoverableRecordContext context, Schema schema, RawRecord values )
        {
            var entity = mapper.CreateEntity();
            schema.ParseValues( context, values, entity, setters );
            assembled = entity;
        }

        public object? Take()
        {
            var entity = assembled;
            assembled = default;
            return entity;
        }
    }
}
