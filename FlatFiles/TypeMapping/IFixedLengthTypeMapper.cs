using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Supports configuring reading to and writing from flat files for a type.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity read and written.</typeparam>
    public interface IFixedLengthTypeMapper<TEntity> : IFixedLengthTypeConfiguration<TEntity>
    {
        /// <summary>
        ///     Reads the entities from the given reader.
        /// </summary>
        /// <param name="reader">A reader over the fixed-length document.</param>
        /// <param name="options">The options controlling how the fixed-length document is read.</param>
        /// <returns>The entities that are extracted from the file.</returns>
        IEnumerable<TEntity> Read( TextReader reader, FixedLengthOptions? options = null );

        /// <summary>
        ///     Reads the entities from the given reader.
        /// </summary>
        /// <param name="reader">A reader over the fixed-length document.</param>
        /// <param name="options">The options controlling how the fixed-length document is read.</param>
        /// <returns>An asynchronous enumerable over the entities.</returns>
        /// <remarks>
        ///     The default implementation forwards to the overload that takes a token, passing
        ///     <see cref="CancellationToken.None"/>.
        /// </remarks>
        IAsyncEnumerable<TEntity> ReadAsync( TextReader reader, FixedLengthOptions? options = null )
        {
            return ReadAsync( reader, options, CancellationToken.None );
        }

        /// <summary>
        ///     Reads the entities from the given reader.
        /// </summary>
        /// <param name="reader">A reader over the fixed-length document.</param>
        /// <param name="options">The options controlling how the fixed-length document is read.</param>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        /// <returns>An asynchronous enumerable over the entities.</returns>
        IAsyncEnumerable<TEntity> ReadAsync( TextReader reader, FixedLengthOptions? options, CancellationToken cancellationToken );

        /// <summary>
        ///     Gets a typed reader to read entities from the underlying document.
        /// </summary>
        /// <param name="reader">A reader over the fixed-length document.</param>
        /// <param name="options">The options controlling how the fixed-length document is read.</param>
        /// <returns>A typed reader.</returns>
        IFixedLengthTypedReader<TEntity> GetReader( TextReader reader, FixedLengthOptions? options = null );

        /// <summary>
        ///     Reads the entities from the given stream.
        /// </summary>
        /// <param name="stream">A stream over the fixed-length document.</param>
        /// <param name="options">The options controlling how the fixed-length document is read.</param>
        /// <param name="encoding">The encoding to read the stream as, or null for UTF-8.</param>
        /// <returns>The entities that are extracted from the file.</returns>
        /// <remarks>
        ///     A byte order mark is honoured whatever encoding is asked for, and taken off the text rather than
        ///     left to become part of the first value. The stream is left open: a mapper does not own what it was
        ///     handed. Defaulted so that an implementation written before this existed keeps compiling.
        /// </remarks>
        IEnumerable<TEntity> Read( Stream stream, FixedLengthOptions? options = null, Encoding? encoding = null )
        {
            return Read( StreamText.Over( stream, encoding ), options );
        }

        /// <summary>
        ///     Gets a typed reader over the given stream.
        /// </summary>
        /// <param name="stream">A stream over the fixed-length document.</param>
        /// <param name="options">The options controlling how the fixed-length document is read.</param>
        /// <param name="encoding">The encoding to read the stream as, or null for UTF-8.</param>
        /// <returns>A typed reader over the entities in the file.</returns>
        /// <remarks>
        ///     A byte order mark is honoured whatever encoding is asked for, and taken off the text rather than
        ///     left to become part of the first value. The stream is left open: a mapper does not own what it was
        ///     handed. Defaulted so that an implementation written before this existed keeps compiling.
        /// </remarks>
        IFixedLengthTypedReader<TEntity> GetReader( Stream stream, FixedLengthOptions? options = null, Encoding? encoding = null )
        {
            return GetReader( StreamText.Over( stream, encoding ), options );
        }


        /// <summary>
        ///     Writes the given entities to the given writer.
        /// </summary>
        /// <param name="writer">A writer over the fixed-length document.</param>
        /// <param name="entities">The entities to write to the document.</param>
        /// <param name="options">The options controlling how the fixed-length document is written.</param>
        void Write( TextWriter writer, IEnumerable<TEntity> entities, FixedLengthOptions? options = null );

        /// <summary>
        ///     Writes the given entities to the given writer.
        /// </summary>
        /// <param name="writer">A writer over the fixed-length document.</param>
        /// <param name="entities">The entities to write to the document.</param>
        /// <param name="options">The options controlling how the fixed-length document is written.</param>
        /// <remarks>
        ///     The default implementation forwards to the overload that takes a token, passing
        ///     <see cref="CancellationToken.None"/>.
        /// </remarks>
        Task WriteAsync( TextWriter writer, IEnumerable<TEntity> entities, FixedLengthOptions? options = null )
        {
            return WriteAsync( writer, entities, options, CancellationToken.None );
        }

        /// <summary>
        ///     Writes the given entities to the given writer.
        /// </summary>
        /// <param name="writer">A writer over the fixed-length document.</param>
        /// <param name="entities">The entities to write to the document.</param>
        /// <param name="options">The options controlling how the fixed-length document is written.</param>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        Task WriteAsync( TextWriter writer, IEnumerable<TEntity> entities, FixedLengthOptions? options, CancellationToken cancellationToken );

        /// <summary>
        ///     Writes the given entities to the given writer.
        /// </summary>
        /// <param name="writer">A writer over the fixed-length document.</param>
        /// <param name="entities">The entities to write to the document.</param>
        /// <param name="options">The options controlling how the fixed-length document is written.</param>
        /// <remarks>
        ///     The default implementation forwards to the overload that takes a token, passing
        ///     <see cref="CancellationToken.None"/>.
        /// </remarks>
        Task WriteAsync( TextWriter writer, IAsyncEnumerable<TEntity> entities, FixedLengthOptions? options = null )
        {
            return WriteAsync( writer, entities, options, CancellationToken.None );
        }

        /// <summary>
        ///     Writes the given entities to the given writer.
        /// </summary>
        /// <param name="writer">A writer over the fixed-length document.</param>
        /// <param name="entities">The entities to write to the document.</param>
        /// <param name="options">The options controlling how the fixed-length document is written.</param>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        Task WriteAsync( TextWriter writer, IAsyncEnumerable<TEntity> entities, FixedLengthOptions? options, CancellationToken cancellationToken );

        /// <summary>
        ///     Gets a typed writer to write entities to the underlying document.
        /// </summary>
        /// <param name="writer">The writer over the fixed-length document.</param>
        /// <param name="options">The options controlling how the fixed-length document is written.</param>
        /// <returns>A typed writer.</returns>
        ITypedWriter<TEntity> GetWriter( TextWriter writer, FixedLengthOptions? options = null );
    }
}
