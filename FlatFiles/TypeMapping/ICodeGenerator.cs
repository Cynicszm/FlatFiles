using System;

namespace FlatFiles.TypeMapping
{
    internal interface ICodeGenerator
    {
        Func<TEntity> GetFactory<TEntity>();

        /// <summary>
        ///     Builds an entity by handing a constructor the values the record parsed to, for a type that cannot
        ///     be built empty and filled afterwards.
        /// </summary>
        /// <typeparam name="TEntity">The type being built.</typeparam>
        /// <param name="mapping">The constructor, and where each of its parameters comes from.</param>
        /// <returns>A function taking the parsed values and answering the entity.</returns>
        Func<object?[], TEntity> GetConstructor<TEntity>( ConstructorMapping mapping );

        Action<IRecordContext, TEntity, object?[]> GetReader<TEntity>(IMemberMapping[] mappings);

        Action<IRecordContext, TEntity, object?[]> GetWriter<TEntity>(IMemberMapping[] mappings);
    }
}
