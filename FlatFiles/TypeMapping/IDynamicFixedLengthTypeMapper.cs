using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Supports reading to and writing from flat files for a runtime type. 
    /// </summary>
    public interface IDynamicFixedLengthTypeMapper : IDynamicFixedLengthTypeConfiguration
    {
        /// <summary>
        ///     Reads the entities from the given reader.
        /// </summary>
        /// <param name="reader">A reader over the fixed-length document.</param>
        /// <param name="options">The options controlling how the fixed-length document is read.</param>
        /// <returns>The entities that are extracted from the file.</returns>
        IEnumerable<object> Read( TextReader reader, FixedLengthOptions? options = null );

        /// <summary>
        ///     Reads the entities from the given reader.
        /// </summary>
        /// <param name="reader">A reader over the fixed-length document.</param>
        /// <param name="options">The options controlling how the fixed-length document is read.</param>
        /// <returns>An asynchronous enumerable over the entities.</returns>
        IAsyncEnumerable<object> ReadAsync( TextReader reader, FixedLengthOptions? options = null );

        /// <summary>
        ///     Reads the entities from the given reader.
        /// </summary>
        /// <param name="reader">A reader over the fixed-length document.</param>
        /// <param name="options">The options controlling how the fixed-length document is read.</param>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        /// <returns>An asynchronous enumerable over the entities.</returns>
        /// <remarks>
        ///     The default implementation forwards to the overload without a token and so cannot observe
        ///     cancellation; an implementation that can should override it.
        /// </remarks>
        IAsyncEnumerable<object> ReadAsync( TextReader reader, FixedLengthOptions? options, CancellationToken cancellationToken )
        {
            return ReadAsync( reader, options );
        }

        /// <summary>
        ///     Gets a typed reader to read entities from the underlying document.
        /// </summary>
        /// <param name="reader">A reader over the fixed-length document.</param>
        /// <param name="options">The options controlling how the fixed-length document is read.</param>
        /// <returns>A typed reader.</returns>
        IFixedLengthTypedReader<object> GetReader( TextReader reader, FixedLengthOptions? options = null );

        /// <summary>
        ///     Writes the given entities to the given stream.
        /// </summary>
        /// <param name="writer">A writer over the fixed-length document.</param>
        /// <param name="entities">The entities to write to the stream.</param>
        /// <param name="options">The options used to format the output.</param>
        void Write( TextWriter writer, IEnumerable<object> entities, FixedLengthOptions? options = null );

        /// <summary>
        ///     Writes the given entities to the given stream.
        /// </summary>
        /// <param name="writer">A writer over the fixed-length document.</param>
        /// <param name="entities">The entities to write to the stream.</param>
        /// <param name="options">The options used to format the output.</param>
        Task WriteAsync( TextWriter writer, IEnumerable<object> entities, FixedLengthOptions? options = null );

        /// <summary>
        ///     Writes the given entities to the given stream.
        /// </summary>
        /// <param name="writer">A writer over the fixed-length document.</param>
        /// <param name="entities">The entities to write to the stream.</param>
        /// <param name="options">The options used to format the output.</param>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        /// <remarks>
        ///     The default implementation forwards to the overload without a token and so cannot observe
        ///     cancellation; an implementation that can should override it.
        /// </remarks>
        Task WriteAsync( TextWriter writer, IEnumerable<object> entities, FixedLengthOptions? options, CancellationToken cancellationToken )
        {
            return WriteAsync( writer, entities, options );
        }

        /// <summary>
        ///     Writes the given entities to the given stream.
        /// </summary>
        /// <param name="writer">A writer over the fixed-length document.</param>
        /// <param name="entities">The entities to write to the stream.</param>
        /// <param name="options">The options used to format the output.</param>
        Task WriteAsync( TextWriter writer, IAsyncEnumerable<object> entities, FixedLengthOptions? options = null );

        /// <summary>
        ///     Writes the given entities to the given stream.
        /// </summary>
        /// <param name="writer">A writer over the fixed-length document.</param>
        /// <param name="entities">The entities to write to the stream.</param>
        /// <param name="options">The options used to format the output.</param>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        /// <remarks>
        ///     The default implementation forwards to the overload without a token and so cannot observe
        ///     cancellation; an implementation that can should override it.
        /// </remarks>
        Task WriteAsync( TextWriter writer, IAsyncEnumerable<object> entities, FixedLengthOptions? options, CancellationToken cancellationToken )
        {
            return WriteAsync( writer, entities, options );
        }

        /// <summary>
        ///     Gets a typed writer to write entities to the underlying document.
        /// </summary>
        /// <param name="writer">The writer over the fixed-length document.</param>
        /// <param name="options">The options controlling how the fixed-length document is written.</param>
        /// <returns>A typed writer.</returns>
        ITypedWriter<object> GetWriter( TextWriter writer, FixedLengthOptions? options = null );
    }
}
