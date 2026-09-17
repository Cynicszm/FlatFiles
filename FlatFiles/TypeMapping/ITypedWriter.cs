using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Represents a writer that will write entities.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity being written.</typeparam>
    public interface ITypedWriter<TEntity>
    {
        /// <summary>
        ///     Gets the underlying writer.
        /// </summary>
        IWriter Writer { get; }

        /// <summary>
        ///     Raised when an error occurs while processing a column.
        /// </summary>
        event EventHandler<ColumnErrorEventArgs>? ColumnError;

        /// <summary>
        ///     Raised when an error occurs while processing a record.
        /// </summary>
        event EventHandler<RecordErrorEventArgs>? RecordError;

        /// <summary>
        ///     Gets the schema being used by the writer to write record values.
        /// </summary>
        /// <returns>The schema being used by the writer.</returns>
        ISchema? GetSchema();

        /// <summary>
        ///     Write the textual representation of the record schema.
        /// </summary>
        /// <remarks>If the header or records have already been written, this call is ignored.</remarks>
        void WriteSchema();

        /// <summary>
        ///     Write the textual representation of the record schema to the writer.
        /// </summary>
        /// <remarks>If the header or records have already been written, this call is ignored.</remarks>
        /// <remarks>
        ///     The default implementation forwards to the overload that takes a token, passing
        ///     <see cref="CancellationToken.None"/>.
        /// </remarks>
        Task WriteSchemaAsync()
        {
            return WriteSchemaAsync( CancellationToken.None );
        }

        /// <summary>
        ///     Write the textual representation of the record schema to the writer.
        /// </summary>
        /// <remarks>If the header or records have already been written, this call is ignored.</remarks>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        Task WriteSchemaAsync( CancellationToken cancellationToken );

        /// <summary>
        ///     Writes the given entity to the underlying document.
        /// </summary>
        /// <param name="entity">The entity to write.</param>
        void Write( TEntity entity );

        /// <summary>
        ///     Writes the given entity to the underlying document.
        /// </summary>
        /// <param name="entity">The entity to write.</param>
        /// <remarks>
        ///     The default implementation forwards to the overload that takes a token, passing
        ///     <see cref="CancellationToken.None"/>.
        /// </remarks>
        Task WriteAsync( TEntity entity )
        {
            return WriteAsync( entity, CancellationToken.None );
        }

        /// <summary>
        ///     Writes the given entity to the underlying document.
        /// </summary>
        /// <param name="entity">The entity to write.</param>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        Task WriteAsync( TEntity entity, CancellationToken cancellationToken );
    }
}
