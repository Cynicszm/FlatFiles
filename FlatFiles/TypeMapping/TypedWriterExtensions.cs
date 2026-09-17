using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Provides extension methods for working with typed writers.
    /// </summary>
    public static class TypedWriterExtensions
    {
        extension<TEntity>( ITypedWriter<TEntity> writer )
        {
            /// <summary>
            ///     Writes all of the entities to the typed writer.
            /// </summary>
            /// <param name="entities">The entities to write to the file.</param>
            /// <returns>The entities written by the writer.</returns>
            public void WriteAll( IEnumerable<TEntity> entities )
            {
                if (writer.Writer.Options.IsFirstRecordSchema)
                {
                    writer.WriteSchema();
                }
                foreach (var entity in entities)
                {
                    writer.Write( entity );
                }
            }

            /// <summary>
            ///     Writes all of the entities to the typed writer.
            /// </summary>
            /// <param name="entities">The entities to write to the file.</param>
            /// <returns>The entities written by the writer.</returns>
            public Task WriteAllAsync( IEnumerable<TEntity> entities )
            {
                return writer.WriteAllAsync( entities, CancellationToken.None );
            }

            /// <summary>
            ///     Writes all of the entities to the typed writer.
            /// </summary>
            /// <param name="entities">The entities to write to the file.</param>
            /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
            /// <returns>The entities written by the writer.</returns>
            public async Task WriteAllAsync( IEnumerable<TEntity> entities, CancellationToken cancellationToken )
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (writer.Writer.Options.IsFirstRecordSchema)
                {
                    await writer.WriteSchemaAsync( cancellationToken ).ConfigureAwait( false );
                }
                foreach (var entity in entities)
                {
                    await writer.WriteAsync( entity, cancellationToken ).ConfigureAwait( false );
                }
            }

            /// <summary>
            ///     Writes all of the entities to the typed writer.
            /// </summary>
            /// <param name="entities">The entities to write to the file.</param>
            /// <returns>The entities written by the writer.</returns>
            public Task WriteAllAsync( IAsyncEnumerable<TEntity> entities )
            {
                return writer.WriteAllAsync( entities, CancellationToken.None );
            }

            /// <summary>
            ///     Writes all of the entities to the typed writer.
            /// </summary>
            /// <param name="entities">The entities to write to the file.</param>
            /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
            /// <returns>The entities written by the writer.</returns>
            public async Task WriteAllAsync( IAsyncEnumerable<TEntity> entities, CancellationToken cancellationToken )
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (writer.Writer.Options.IsFirstRecordSchema)
                {
                    await writer.WriteSchemaAsync( cancellationToken ).ConfigureAwait( false );
                }
                await foreach (var entity in entities.WithCancellation( cancellationToken ).ConfigureAwait( false ))
                {
                    await writer.WriteAsync( entity, cancellationToken ).ConfigureAwait( false );
                }
            }
        }
    }
}
