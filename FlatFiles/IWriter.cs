using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlatFiles
{
    /// <summary>
    ///     Defines the operations that a writer must support.
    /// </summary>
    public interface IWriter
    {
        /// <summary>
        ///     Raised when an error occurs while processing a column.
        /// </summary>
        event EventHandler<ColumnErrorEventArgs>? ColumnError;

        /// <summary>
        ///     Raised when an error occurs while processing a record.
        /// </summary>
        event EventHandler<RecordErrorEventArgs>? RecordError;

        /// <summary>
        ///     Gets the options controlling the behaviour of the writer.
        /// </summary>
        IOptions Options { get; }

        /// <summary>
        ///     Gets the schema being used by the builder to create the textual representation.
        /// </summary>
        /// <returns>The schema being used by the builder to create the textual representation.</returns>
        ISchema? GetSchema();

        /// <summary>
        ///     Write the textual representation of the record schema.
        /// </summary>
        /// <remarks>If the header or records have already been written, this call is ignored.</remarks>
        void WriteSchema();

        /// <summary>
        ///     Write the textual representation of the record schema.
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
        ///     Write the textual representation of the record schema.
        /// </summary>
        /// <remarks>If the header or records have already been written, this call is ignored.</remarks>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        Task WriteSchemaAsync( CancellationToken cancellationToken );

        /// <summary>
        ///     Writes the textual representation of the given values to the writer.
        /// </summary>
        /// <param name="values">The values to write.</param>
        /// <returns>The textual representation of the given values.</returns>
        void Write( object?[] values );

        /// <summary>
        ///     Writes the textual representation of the given values to the writer.
        /// </summary>
        /// <param name="values">The values to write.</param>
        /// <returns>The textual representation of the given values.</returns>
        /// <remarks>
        ///     The default implementation forwards to the overload that takes a token, passing
        ///     <see cref="CancellationToken.None"/>.
        /// </remarks>
        Task WriteAsync( object?[] values )
        {
            return WriteAsync( values, CancellationToken.None );
        }

        /// <summary>
        ///     Writes the textual representation of the given values to the writer.
        /// </summary>
        /// <param name="values">The values to write.</param>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        /// <returns>The textual representation of the given values.</returns>
        Task WriteAsync( object?[] values, CancellationToken cancellationToken );

        /// <summary>
        ///     Write the given data directly to the output. By default, this will
        ///     not include a newline.
        /// </summary>
        /// <param name="data">The data to write to the output.</param>
        /// <param name="writeRecordSeparator">Indicates whether a record separator should be written after the data.</param>
        void WriteRaw( string data, bool writeRecordSeparator = false );

        /// <summary>
        ///     Write the given data directly to the output. By default, this will
        ///     not include a newline.
        /// </summary>
        /// <param name="data">The data to write to the output.</param>
        /// <param name="writeRecordSeparator">Indicates whether a record separator should be written after the data.</param>
        /// <remarks>
        ///     The default implementation forwards to the overload that takes a token, passing
        ///     <see cref="CancellationToken.None"/>.
        /// </remarks>
        Task WriteRawAsync( string data, bool writeRecordSeparator = false )
        {
            return WriteRawAsync( data, writeRecordSeparator, CancellationToken.None );
        }

        /// <summary>
        ///     Write the given data directly to the output. By default, this will
        ///     not include a newline.
        /// </summary>
        /// <param name="data">The data to write to the output.</param>
        /// <param name="writeRecordSeparator">Indicates whether a record separator should be written after the data.</param>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        Task WriteRawAsync( string data, bool writeRecordSeparator, CancellationToken cancellationToken );
    }
}
