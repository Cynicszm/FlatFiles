using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FlatFiles.Properties;
using FlatFiles.TypeMapping;

namespace FlatFiles
{
    /// <summary>
    ///     Builds textual representations of data by separating fields with a delimiter.
    /// </summary>
    public sealed class DelimitedWriter : IWriterWithMetadata
    {
        private readonly DelimitedRecordWriter recordWriter;
        private bool isSchemaWritten;

        /// <summary>
        ///     Initialises a new DelimitedWriter without a schema.
        /// </summary>
        /// <param name="writer">A writer over the delimited document.</param>
        /// <param name="options">The options used to format the output.</param>
        /// <exception cref="ArgumentNullException">The writer is null.</exception>
        public DelimitedWriter( TextWriter writer, DelimitedOptions? options = null )
            : this( writer, null, options, false )
        {
        }

        /// <summary>
        ///     Initialises a new DelimitedWriter with the given schema.
        /// </summary>
        /// <param name="writer">A writer over the delimited document.</param>
        /// <param name="schema">The schema of the delimited document.</param>
        /// <param name="options">The options used to format the output.</param>
        /// <exception cref="ArgumentNullException">The writer is null.</exception>
        /// <exception cref="ArgumentNullException">The schema is null.</exception>
        public DelimitedWriter( TextWriter writer, DelimitedSchema schema, DelimitedOptions? options = null )
            : this( writer, schema, options, true )
        {
        }

        private DelimitedWriter( TextWriter writer, DelimitedSchema? schema, DelimitedOptions? options, bool hasSchema )
        {
            ArgumentNullException.ThrowIfNull( writer );
            if (hasSchema)
            {
                ArgumentNullException.ThrowIfNull( schema );
            }
            recordWriter = new DelimitedRecordWriter( writer, schema, options );
        }

        /// <summary>
        ///     Initialises a new DelimitedWriter with the given schema.
        /// </summary>
        /// <param name="writer">A writer over the delimited document.</param>
        /// <param name="injector">The schema injector to use to determine the schema.</param>
        /// <param name="options">The options used to format the output.</param>
        /// <exception cref="ArgumentNullException">The writer is null.</exception>
        /// <exception cref="ArgumentNullException">The schema injector is null.</exception>
        public DelimitedWriter( TextWriter writer, DelimitedSchemaInjector injector, DelimitedOptions? options = null )
        {
            ArgumentNullException.ThrowIfNull( writer );
            ArgumentNullException.ThrowIfNull( injector );
            recordWriter = new DelimitedRecordWriter( writer, injector, options );
        }

        /// <summary>
        ///     Raised when an error occurs while processing a column.
        /// </summary>
        public event EventHandler<ColumnErrorEventArgs>? ColumnError
        {
            add => recordWriter.ColumnError += value;
            remove => recordWriter.ColumnError -= value;
        }

        /// <summary>
        ///     Raised when an error occurs while processing a record.
        /// </summary>
        public event EventHandler<RecordErrorEventArgs>? RecordError;

        IOptions IWriter.Options => recordWriter.Options;

        /// <summary>
        ///     Gets the schema used to build the output.
        /// </summary>
        /// <returns>The schema used to build the output.</returns>
        public DelimitedSchema? GetSchema()
        {
            return recordWriter.ActualSchema;
        }

        ISchema? IWriter.GetSchema()
        {
            return GetSchema();
        }

        /// <summary>
        ///     Write the textual representation of the record schema.
        /// </summary>
        /// <remarks>If the header or records have already been written, this call is ignored.</remarks>
        public void WriteSchema()
        {
            if (isSchemaWritten)
            {
                return;
            }
            if (recordWriter.ActualSchema is not null)
            {
                recordWriter.WriteSchema();
                recordWriter.WriteRecordSeparator();
                ++recordWriter.PhysicalRecordNumber;
            }
            isSchemaWritten = true;
        }

        /// <summary>
        ///     Write the textual representation of the record schema to the writer.
        /// </summary>
        /// <remarks>If the header or records have already been written, this call is ignored.</remarks>
        public Task WriteSchemaAsync()
        {
            return WriteSchemaAsync( CancellationToken.None );
        }

        /// <summary>
        ///     Write the textual representation of the record schema to the writer.
        /// </summary>
        /// <remarks>If the header or records have already been written, this call is ignored.</remarks>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        public async Task WriteSchemaAsync( CancellationToken cancellationToken )
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (isSchemaWritten)
            {
                return;
            }
            if (recordWriter.ActualSchema is not null)
            {
                await recordWriter.WriteSchemaAsync( cancellationToken ).ConfigureAwait( false );
                await recordWriter.WriteRecordSeparatorAsync( cancellationToken ).ConfigureAwait( false );
                ++recordWriter.PhysicalRecordNumber;
            }
            isSchemaWritten = true;
        }

        /// <summary>
        ///     Writes the textual representation of the given values to the writer.
        /// </summary>
        /// <param name="values">The values to write.</param>
        /// <exception cref="ArgumentNullException">The values array is null.</exception>
        public void Write( object?[] values )
        {
            ArgumentNullException.ThrowIfNull( values );
            WriteSchemaIfNeeded();
            try
            {
                recordWriter.WriteRecord( values );
                recordWriter.WriteRecordSeparator();
                ++recordWriter.PhysicalRecordNumber;
                ++recordWriter.LogicalRecordNumber;
            }
            catch (RecordProcessingException exception)
            {
                ProcessError( exception );
            }
            catch (FlatFileException exception)
            {
                var recordContext = GetMetadata();
                ProcessError( new RecordProcessingException( recordContext, Resources.InvalidRecordConversion, exception ) );
            }
        }

        /// <summary>
        ///     Writes the textual representation of the given values to the writer.
        /// </summary>
        /// <param name="values">The values to write.</param>
        /// <exception cref="ArgumentNullException">The values array is null.</exception>
        public Task WriteAsync( object?[] values )
        {
            return WriteAsync( values, CancellationToken.None );
        }

        /// <summary>
        ///     Writes the textual representation of the given values to the writer.
        /// </summary>
        /// <param name="values">The values to write.</param>
        /// <exception cref="ArgumentNullException">The values array is null.</exception>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        public async Task WriteAsync( object?[] values, CancellationToken cancellationToken )
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull( values );
            await WriteSchemaIfNeededAsync( cancellationToken ).ConfigureAwait( false );
            try
            {
                await recordWriter.WriteRecordAsync( values, cancellationToken ).ConfigureAwait( false );
                await recordWriter.WriteRecordSeparatorAsync( cancellationToken ).ConfigureAwait( false );
                ++recordWriter.PhysicalRecordNumber;
                ++recordWriter.LogicalRecordNumber;
            }
            catch (RecordProcessingException exception)
            {
                ProcessError( exception );
            }
            catch (FlatFileException exception)
            {
                var recordContext = GetMetadata();
                ProcessError( new RecordProcessingException( recordContext, Resources.InvalidRecordConversion, exception ) );
            }
        }

        /// <summary>
        ///     Write the given data directly to the output. By default, this will
        ///     not include a newline.
        /// </summary>
        /// <param name="data">The data to write to the output.</param>
        /// <param name="writeRecordSeparator">Indicates whether a newline should be written after the data.</param>
        public void WriteRaw( string data, bool writeRecordSeparator = false )
        {
            recordWriter.WriteRaw( data );
            if (writeRecordSeparator)
            {
                recordWriter.WriteRecordSeparator();
            }
        }

        /// <summary>
        ///     Write the given data directly to the output. By default, this will
        ///     not include a newline.
        /// </summary>
        /// <param name="data">The data to write to the output.</param>
        /// <param name="writeRecordSeparator">Indicates whether a record separator should be written after the data.</param>
        public Task WriteRawAsync( string data, bool writeRecordSeparator = false )
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
        public async Task WriteRawAsync( string data, bool writeRecordSeparator, CancellationToken cancellationToken )
        {
            cancellationToken.ThrowIfCancellationRequested();
            await recordWriter.WriteRawAsync( data, cancellationToken );
            if (writeRecordSeparator)
            {
                await recordWriter.WriteRecordSeparatorAsync( cancellationToken );
            }
        }

        private void ProcessError( RecordProcessingException exception )
        {
            if (RecordError is null)
            {
                throw exception;
            }
            var args = new RecordErrorEventArgs( exception );
            RecordError( this, args );
            if (args.IsHandled)
            {
                return;
            }
            throw exception;
        }

        private IRecordContext GetMetadata()
        {
            if (recordWriter.Metadata is not null)
            {
                return recordWriter.Metadata;
            }
            return GetUncachedMetadata( recordWriter.ActualSchema );
        }

        /// <summary>
        ///     Writes the header before the first record, once, whichever way that record is written.
        /// </summary>
        private void WriteSchemaIfNeeded()
        {
            if (!isSchemaWritten)
            {
                if (recordWriter.Options.IsFirstRecordSchema && recordWriter.ActualSchema is not null)
                {
                    recordWriter.WriteSchema();
                    recordWriter.WriteRecordSeparator();
                    ++recordWriter.PhysicalRecordNumber;
                }
                isSchemaWritten = true;
            }
        }

        private async Task WriteSchemaIfNeededAsync( CancellationToken cancellationToken )
        {
            if (!isSchemaWritten)
            {
                if (recordWriter.Options.IsFirstRecordSchema && recordWriter.ActualSchema is not null)
                {
                    await recordWriter.WriteSchemaAsync( cancellationToken ).ConfigureAwait( false );
                    await recordWriter.WriteRecordSeparatorAsync( cancellationToken ).ConfigureAwait( false );
                    ++recordWriter.PhysicalRecordNumber;
                }
                isSchemaWritten = true;
            }
        }

        bool IWriterWithMetadata.CanWriteFromEntity => recordWriter.CanWriteFromEntity;

        void IWriterWithMetadata.WriteFromEntity<TEntity>( TEntity entity, IColumnGetter<TEntity>[] getters )
        {
            WriteSchemaIfNeeded();
            try
            {
                recordWriter.WriteRecord( entity, getters );
                recordWriter.WriteRecordSeparator();
                ++recordWriter.PhysicalRecordNumber;
                ++recordWriter.LogicalRecordNumber;
            }
            catch (RecordProcessingException exception)
            {
                ProcessError( exception );
            }
        }

        async Task IWriterWithMetadata.WriteFromEntityAsync<TEntity>( TEntity entity, IColumnGetter<TEntity>[] getters, CancellationToken cancellationToken )
        {
            await WriteSchemaIfNeededAsync( cancellationToken ).ConfigureAwait( false );
            try
            {
                await recordWriter.WriteRecordAsync( entity, getters, cancellationToken ).ConfigureAwait( false );
                await recordWriter.WriteRecordSeparatorAsync( cancellationToken ).ConfigureAwait( false );
                ++recordWriter.PhysicalRecordNumber;
                ++recordWriter.LogicalRecordNumber;
            }
            catch (RecordProcessingException exception)
            {
                ProcessError( exception );
            }
        }

        IRecordContext IWriterWithMetadata.GetMetadata()
        {
            var schema = recordWriter.GetSchema( [] ); // Will work for TypedWriters using Schema Injector
            return GetUncachedMetadata( schema );
        }

        private ExecutionContextCache<DelimitedSchema, GenericExecutionContext>? metadataExecutionContexts;

        private IRecordContext GetUncachedMetadata( DelimitedSchema? schema )
        {
            var executionContext = (metadataExecutionContexts ??= new ExecutionContextCache<DelimitedSchema, GenericExecutionContext>( s => new GenericExecutionContext( s, recordWriter.Options.Clone() ) )).Get( schema );
            var recordContext = new GenericRecordContext( executionContext )
            {
                PhysicalRecordNumber = recordWriter.PhysicalRecordNumber,
                LogicalRecordNumber = recordWriter.LogicalRecordNumber
            };
            return recordContext;
        }
    }
}
