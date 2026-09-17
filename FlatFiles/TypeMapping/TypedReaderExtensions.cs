using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Provides extension methods for working with typed readers.
    /// </summary>
    public static class TypedReaderExtensions
    {
        extension<TEntity>( ITypedReader<TEntity> reader )
        {
            /// <summary>
            ///     Reads all of the entities from the typed reader.
            /// </summary>
            /// <returns>The entities read by the reader.</returns>
            /// <remarks>This method only consumes records from the reader on-demand.</remarks>
            public IEnumerable<TEntity> ReadAll()
            {
                while (reader.Read())
                {
                    var entity = reader.Current;
                    yield return entity;
                }
            }

            /// <summary>
            ///     Reads each record from the given reader, such that each record is retrieved asynchronously.
            /// </summary>
            /// <returns>Each record from the given reader.</returns>
            public IAsyncEnumerable<TEntity> ReadAllAsync()
            {
                return reader.ReadAllAsync( CancellationToken.None );
            }

            /// <summary>
            ///     Reads each record from the given reader, such that each record is retrieved asynchronously.
            /// </summary>
            /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
            /// <returns>Each record from the given reader.</returns>
            public async IAsyncEnumerable<TEntity> ReadAllAsync( [EnumeratorCancellation] CancellationToken cancellationToken )
            {
                while (await reader.ReadAsync( cancellationToken ).ConfigureAwait( false ))
                {
                    var entity = reader.Current;
                    yield return entity;
                }
            }
        }
    }
}
