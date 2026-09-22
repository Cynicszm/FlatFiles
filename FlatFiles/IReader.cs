using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlatFiles
{
    /// <summary>
    ///     Defines the operations that a fixed-length reader must support.
    /// </summary>
    public interface IReader
    {
        /// <summary>
        ///     Raised after a record is parsed.
        /// </summary>
        event EventHandler<IRecordParsedEventArgs>? RecordParsed;

        /// <summary>
        ///     Raised when an error occurs while processing a column.
        /// </summary>
        event EventHandler<ColumnErrorEventArgs>? ColumnError;

        /// <summary>
        ///     Raised when an error occurs while processing a record.
        /// </summary>
        event EventHandler<RecordErrorEventArgs>? RecordError;

        /// <summary>
        ///     Gets the options controlling the behaviour of the reader.
        /// </summary>
        IOptions Options { get; }

        /// <summary>
        ///     Gets the schema being used by the parser to parse record values.
        /// </summary>
        /// <returns>The schema being used by the parser.</returns>
        ISchema? GetSchema();

        /// <summary>
        ///     Gets the schema being used by the parser to parse record values.
        /// </summary>
        /// <returns>The schema being used by the parser.</returns>
        /// <remarks>
        ///     The default implementation forwards to the overload that takes a token, passing
        ///     <see cref="CancellationToken.None"/>.
        /// </remarks>
        Task<ISchema?> GetSchemaAsync()
        {
            return GetSchemaAsync( CancellationToken.None );
        }

        /// <summary>
        ///     Gets the schema being used by the parser to parse record values.
        /// </summary>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        /// <returns>The schema being used by the parser.</returns>
        Task<ISchema?> GetSchemaAsync( CancellationToken cancellationToken );

        /// <summary>
        ///     Reads the next record from the file.
        /// </summary>
        /// <returns>True if the next record was read; otherwise, false if the end of file was reached.</returns>
        bool Read();

        /// <summary>
        ///     Reads the next record from the file.
        /// </summary>
        /// <returns>True if the next record was read; otherwise, false if the end of file was reached.</returns>
        /// <remarks>
        ///     The default implementation forwards to the overload that takes a token, passing
        ///     <see cref="CancellationToken.None"/>.
        /// </remarks>
        ValueTask<bool> ReadAsync()
        {
            return ReadAsync( CancellationToken.None );
        }

        /// <summary>
        ///     Reads the next record from the file.
        /// </summary>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        /// <returns>True if the next record was read; otherwise, false if the end of file was reached.</returns>
        ValueTask<bool> ReadAsync( CancellationToken cancellationToken );

        /// <summary>
        ///     Skips the next record from the file.
        /// </summary>
        /// <returns>True if the next record was skipped; otherwise, false if the end of the file was reached.</returns>
        bool Skip();

        /// <summary>
        ///     Skips the next record from the file.
        /// </summary>
        /// <returns>True if the next record was skipped; otherwise, false if the end of the file was reached.</returns>
        /// <remarks>
        ///     The default implementation forwards to the overload that takes a token, passing
        ///     <see cref="CancellationToken.None"/>.
        /// </remarks>
        ValueTask<bool> SkipAsync()
        {
            return SkipAsync( CancellationToken.None );
        }

        /// <summary>
        ///     Skips the next record from the file.
        /// </summary>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        /// <returns>True if the next record was skipped; otherwise, false if the end of the file was reached.</returns>
        ValueTask<bool> SkipAsync( CancellationToken cancellationToken );

        /// <summary>
        ///     Gets the values of the current record.
        /// </summary>
        /// <returns>The value of the current record.</returns>
        object?[] GetValues();
    }
}
