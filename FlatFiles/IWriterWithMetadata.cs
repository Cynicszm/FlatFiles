using System.Threading;
using System.Threading.Tasks;
using FlatFiles.TypeMapping;

namespace FlatFiles
{
    internal interface IWriterWithMetadata : IWriter
    {
        IRecordContext GetMetadata();

        /// <summary>
        ///     Whether a record can be written straight from the entity it comes from. It cannot where the schema
        ///     is chosen from the values, since there are no values until they have been worked out.
        /// </summary>
        bool CanWriteFromEntity { get; }

        /// <summary>
        ///     Writes a record by asking each column's getter for its member, in place of being handed an array of
        ///     values that were boxed to fill it.
        /// </summary>
        /// <typeparam name="TEntity">The type being written from.</typeparam>
        /// <param name="entity">The entity to write.</param>
        /// <param name="getters">One per column, in the order the schema declares them.</param>
        void WriteFromEntity<TEntity>( TEntity entity, IColumnGetter<TEntity>[] getters );

        /// <summary>
        ///     Writes a record from the entity it comes from, asynchronously.
        /// </summary>
        /// <typeparam name="TEntity">The type being written from.</typeparam>
        /// <param name="entity">The entity to write.</param>
        /// <param name="getters">One per column, in the order the schema declares them.</param>
        /// <param name="cancellationToken">Stops the write.</param>
        Task WriteFromEntityAsync<TEntity>( TEntity entity, IColumnGetter<TEntity>[] getters, CancellationToken cancellationToken );
    }
}
