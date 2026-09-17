using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FlatFiles.Properties;

namespace FlatFiles
{
    /// <summary>
    ///     Extracts records from a file containing delimited values.
    /// </summary>
    public sealed class DelimitedReader : IReaderWithMetadata
    {
        private readonly DelimitedRecordParser parser;
        private readonly DelimitedSchemaSelector? schemaSelector;
        private DelimitedSchema? schema;
        private IRecordContext? recordContext;
        private int physicalRecordNumber;
        private int logicalRecordNumber;
        private object?[]? values;
        private bool endOfFile;
        private bool hasError;

        /// <summary>
        ///     Initializes a new DelimitedReader with no schema.
        /// </summary>
        /// <param name="reader">A reader over the delimited document.</param>
        /// <param name="options">The options controlling how the delimited document is read.</param>
        /// <exception cref="ArgumentNullException">The reader is null.</exception>
        public DelimitedReader( TextReader reader, DelimitedOptions? options = null )
            : this( reader, null, options, false )
        {
        }

        /// <summary>
        ///     Initializes a new DelimitedReader with the given schema.
        /// </summary>
        /// <param name="reader">A reader over the delimited document.</param>
        /// <param name="schema">The schema of the delimited document.</param>
        /// <param name="options">The options controlling how the delimited document is read.</param>
        /// <exception cref="ArgumentNullException">The reader is null.</exception>
        /// <exception cref="ArgumentNullException">The schema is null.</exception>
        public DelimitedReader( TextReader reader, DelimitedSchema schema, DelimitedOptions? options = null )
            : this( reader, schema, options, true )
        {
        }

        /// <summary>
        ///     Initializes a new DelimitedReader with the given schema.
        /// </summary>
        /// <param name="reader">A reader over the delimited document.</param>
        /// <param name="schemaSelector">The schema selector configured to determine the schema dynamically.</param>
        /// <param name="options">The options controlling how the delimited document is read.</param>
        /// <exception cref="ArgumentNullException">The reader is null.</exception>
        /// <exception cref="ArgumentNullException">The schema selector is null.</exception>
        public DelimitedReader( TextReader reader, DelimitedSchemaSelector schemaSelector, DelimitedOptions? options = null )
            : this( reader, null, options, false )
        {
            this.schemaSelector = schemaSelector ?? throw new ArgumentNullException( nameof( schemaSelector ) );
        }

        private DelimitedReader( TextReader reader, DelimitedSchema? schema, DelimitedOptions? options, bool hasSchema )
        {
            ArgumentNullException.ThrowIfNull( reader );
            if (hasSchema && schema is null)
            {
                throw new ArgumentNullException( nameof( schema ) );
            }
            options = options is null ? new DelimitedOptions() : options.Clone();
            if (options.RecordSeparator == options.Separator)
            {
                throw new ArgumentException( Resources.SameSeparator, nameof( options ) );
            }
            parser = new DelimitedRecordParser( reader, options );
            this.schema = schema;
        }

        /// <summary>
        ///     Raised when a record is read but before its columns are parsed.
        /// </summary>
        public event EventHandler<DelimitedRecordReadEventArgs>? RecordRead;

        /// <summary>
        ///     Raised when a record is parsed.
        /// </summary>
        public event EventHandler<DelimitedRecordParsedEventArgs>? RecordParsed;

        event EventHandler<IRecordParsedEventArgs>? IReader.RecordParsed
        {
            // EventHandler<T> is contravariant, so the interface handler subscribes to the typed event as it is and can
            // be removed again. Wrapping it in a lambda made every removal a silent no-op.
            add => RecordParsed += value;
            remove => RecordParsed -= value;
        }

        /// <summary>
        ///     Raised when an error occurs while processing a record.
        /// </summary>
        public event EventHandler<RecordErrorEventArgs>? RecordError;

        /// <summary>
        ///     Raised when an error occurs while processing a column.
        /// </summary>
        public event EventHandler<ColumnErrorEventArgs>? ColumnError;

        IOptions IReader.Options => parser.Options;

        /// <summary>
        ///     Gets the schema being used by the parser. If a
        ///     SchemaSelector was provided, null will be returned.
        ///     If no schema was specified and no schema exists in
        ///     the file, null will be returned.
        /// </summary>
        /// <returns>The names.</returns>
        public DelimitedSchema? GetSchema()
        {
            return schemaSelector is not null ? null : HandleSchema();
        }

        /// <summary>
        ///     Gets the schema being used by the parser.If a
        ///     SchemaSelector was provided, null will be returned.
        ///     If no schema was specified and no schema exists in
        ///     the file, null will be returned.
        /// </summary>
        /// <returns>The schema being used by the parser.</returns>
        public Task<DelimitedSchema?> GetSchemaAsync()
        {
            return GetSchemaAsync( CancellationToken.None );
        }

        /// <summary>
        ///     Gets the schema being used by the parser.If a
        ///     SchemaSelector was provided, null will be returned.
        ///     If no schema was specified and no schema exists in
        ///     the file, null will be returned.
        /// </summary>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        /// <returns>The schema being used by the parser.</returns>
        public async Task<DelimitedSchema?> GetSchemaAsync( CancellationToken cancellationToken )
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (schemaSelector is not null)
            {
                return null;
            }
            return await HandleSchemaAsync( cancellationToken );
        }

        ISchema? IReader.GetSchema()
        {
            return GetSchema();
        }

        async Task<ISchema?> IReader.GetSchemaAsync()
        {
            var currentSchema = await GetSchemaAsync().ConfigureAwait( false );
            return currentSchema;
        }

        async Task<ISchema?> IReader.GetSchemaAsync( CancellationToken cancellationToken )
        {
            var currentSchema = await GetSchemaAsync( cancellationToken ).ConfigureAwait( false );
            return currentSchema;
        }

        /// <summary>
        ///     Attempts to read the next record from the stream.
        /// </summary>
        /// <returns>True if the next record was read or false if all records have been read.</returns>
        public bool Read()
        {
            if (hasError)
            {
                throw new InvalidOperationException( Resources.ReadingWithErrors );
            }
            HandleSchema();
            try
            {
                values = ParsePartitions();
                if (values is null)
                {
                    return false;
                }

                ++logicalRecordNumber;
                return true;
            }
            catch (FlatFileException)
            {
                hasError = true;
                throw;
            }
        }

        /// <inheritdoc />
        /// <summary>
        ///     Attempts to read the next record from the stream.
        /// </summary>
        /// <returns>True if the next record was read or false if all records have been read.</returns>
        public ValueTask<bool> ReadAsync()
        {
            return ReadAsync( CancellationToken.None );
        }

        /// <inheritdoc />
        /// <summary>
        ///     Attempts to read the next record from the stream.
        /// </summary>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        /// <returns>True if the next record was read or false if all records have been read.</returns>
        public async ValueTask<bool> ReadAsync( CancellationToken cancellationToken )
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (hasError)
            {
                throw new InvalidOperationException( Resources.ReadingWithErrors );
            }
            await HandleSchemaAsync( cancellationToken ).ConfigureAwait( false );
            try
            {
                values = await ParsePartitionsAsync( cancellationToken ).ConfigureAwait( false );
                if (values is null)
                {
                    return false;
                }

                ++logicalRecordNumber;
                return true;
            }
            catch (FlatFileException)
            {
                hasError = true;
                throw;
            }
        }

        private DelimitedSchema? HandleSchema()
        {
            if (physicalRecordNumber != 0)
            {
                return schema;
            }
            if (!parser.Options.IsFirstRecordSchema)
            {
                return schema;
            }
            if (schemaSelector is not null || schema is not null)
            {
                SkipInternal();
                return schema;
            }
            var (_, columnNames) = ReadNextRecord();
            if (columnNames is null)
            {
                // Do not treat a missing schema in an empty file as an error.
                return null;
            }
            schema = CreateSchemaFromHeader( columnNames );
            return schema;
        }

        private async Task<DelimitedSchema?> HandleSchemaAsync( CancellationToken cancellationToken = default )
        {
            if (physicalRecordNumber != 0)
            {
                return schema;
            }
            if (!parser.Options.IsFirstRecordSchema)
            {
                return schema;
            }
            if (schemaSelector is not null || schema is not null)
            {
                await SkipAsyncInternal( cancellationToken ).ConfigureAwait( false );
                return schema;
            }
            var (_, columnNames) = await ReadNextRecordAsync( cancellationToken ).ConfigureAwait( false );
            if (columnNames is null)
            {
                // Do not treat a missing schema in an empty file as an error.
                return null;
            }
            schema = CreateSchemaFromHeader( columnNames );
            return schema;
        }

        private DelimitedSchema CreateSchemaFromHeader( string[] columnNames )
        {
            var currentSchema = new DelimitedSchema();
            foreach (var columnName in columnNames)
            {
                var column = new StringColumn( columnName )
                {
                    Trim = !parser.Options.PreserveWhiteSpace
                };
                currentSchema.AddColumn( column );
            }
            return currentSchema;
        }

        private object?[]? ParsePartitions()
        {
            while (!endOfFile)
            {
                var (record, rawValues) = ReadNextRecord();
                var currentValues = ProcessRecord( record, rawValues );
                if (currentValues is not null)
                {
                    return currentValues;
                }
            }
            return null;
        }

        private async Task<object?[]?> ParsePartitionsAsync( CancellationToken cancellationToken = default )
        {
            while (!endOfFile)
            {
                var (record, rawValues) = await ReadNextRecordAsync( cancellationToken );
                var currentValues = ProcessRecord( record, rawValues );
                if (currentValues is not null)
                {
                    return currentValues;
                }
            }
            return null;
        }

        private object?[]? ProcessRecord( string? record, string[]? rawValues )
        {
            if (record is null || rawValues is null)
            {
                return null;
            }
            var currentSchema = GetSchema( record, rawValues ) ?? DelimitedSchema.BuildDynamicSchema( parser.Options, rawValues.Length );
            var currentContext = NewRecordContext( currentSchema, record, rawValues );
            recordContext = currentContext;
            if (IsSkipped( currentContext, rawValues ))
            {
                return null;
            }
            if (HasWrongNumberOfColumns( currentSchema, rawValues ))
            {
                ProcessError( new RecordProcessingException( currentContext, Resources.DelimitedRecordWrongNumberOfColumns ) );
                return null;
            }
            var currentValues = ParseValues( currentContext, rawValues );
            if (currentValues is null)
            {
                return null;
            }
            RecordParsed?.Invoke( this, new DelimitedRecordParsedEventArgs( currentContext, currentValues ) );
            return currentValues;
        }


        private DelimitedSchema? GetSchema( string? record, string[] rawValues )
        {
            if (schemaSelector is null)
            {
                return schema;
            }
            var currentSchema = schemaSelector.GetSchema( rawValues );
            if (currentSchema is not null)
            {
                return currentSchema;
            }
            var currentContext = GetMetadata( null, record );
            ProcessError( new RecordProcessingException( currentContext, Resources.MissingMatcher ) );
            return null;
        }

        private bool IsSkipped( DelimitedRecordContext currentContext, string[] currentValues )
        {
            if (RecordRead is null)
            {
                return false;
            }
            var e = new DelimitedRecordReadEventArgs( currentContext, currentValues );
            RecordRead( this, e );
            return e.IsSkipped;
        }

        private ExecutionContextCache<DelimitedSchema, DelimitedExecutionContext>? executionContexts;

        private DelimitedRecordContext NewRecordContext( DelimitedSchema currentSchema, string record, string[] currentValues )
        {
            var executionContext = (executionContexts ??= new ExecutionContextCache<DelimitedSchema, DelimitedExecutionContext>( s => new DelimitedExecutionContext( s!, parser.Options.Clone() ) )).Get( currentSchema );
            var currentContext = new DelimitedRecordContext( executionContext )
            {
                PhysicalRecordNumber = physicalRecordNumber,
                LogicalRecordNumber = logicalRecordNumber,
                Record = record,
                Values = currentValues
            };
            return currentContext;
        }

        private static bool HasWrongNumberOfColumns( DelimitedSchema currentSchema, string[] rawValues )
        {
            var columnDefinitions = currentSchema.ColumnDefinitions;
            return rawValues.Length + columnDefinitions.MetadataCount < columnDefinitions.PhysicalCount;
        }

        private object?[]? ParseValues( DelimitedRecordContext currentContext, string[] rawValues )
        {
            try
            {
                currentContext.ColumnError += ColumnError;
                var currentSchema = currentContext.ExecutionContext.Schema;
                return currentSchema.ParseValues( currentContext, rawValues );
            }
            catch (FlatFileException exception)
            {
                ProcessError( new RecordProcessingException( currentContext, Resources.InvalidRecordConversion, exception ) );
                return null;
            }
        }

        /// <summary>
        ///     Attempts to skip the next record from the stream.
        /// </summary>
        /// <returns>True if the next record was skipped or false if all records have been read.</returns>
        /// <remarks>The previously parsed values remain available.</remarks>
        public bool Skip()
        {
            if (hasError)
            {
                throw new InvalidOperationException( Resources.ReadingWithErrors );
            }
            HandleSchema();
            var result = SkipInternal();
            return result;
        }

        /// <inheritdoc />
        /// <summary>
        ///     Attempts to skip the next record from the stream.
        /// </summary>
        /// <returns>True if the next record was skipped or false if all records have been read.</returns>
        /// <remarks>The previously parsed values remain available.</remarks>
        public ValueTask<bool> SkipAsync()
        {
            return SkipAsync( CancellationToken.None );
        }

        /// <inheritdoc />
        /// <summary>
        ///     Attempts to skip the next record from the stream.
        /// </summary>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        /// <returns>True if the next record was skipped or false if all records have been read.</returns>
        /// <remarks>The previously parsed values remain available.</remarks>
        public async ValueTask<bool> SkipAsync( CancellationToken cancellationToken )
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (hasError)
            {
                throw new InvalidOperationException( Resources.ReadingWithErrors );
            }
            await HandleSchemaAsync( cancellationToken ).ConfigureAwait( false );
            var result = await SkipAsyncInternal( cancellationToken ).ConfigureAwait( false );
            return result;
        }

        private bool SkipInternal()
        {
            var (_, rawValues) = ReadNextRecord();
            return rawValues is not null;
        }

        private async ValueTask<bool> SkipAsyncInternal( CancellationToken cancellationToken = default )
        {
            var (_, rawValues) = await ReadNextRecordAsync( cancellationToken ).ConfigureAwait( false );
            return rawValues is not null;
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

        private (string?, string[]?) ReadNextRecord()
        {
            if (parser.IsEndOfStream())
            {
                endOfFile = true;
                values = null;
                return (null, null);
            }
            try
            {
                var (record, results) = parser.ReadRecord();
                ++physicalRecordNumber;
                return (record, results);
            }
            catch (DelimitedSyntaxException exception)
            {
                // If we cannot read the next record, we cannot process it or allow it to be ignored.
                // We must treat it as a fatal error.
                var currentContext = GetMetadata( null, null );
                throw new RecordProcessingException( currentContext, Resources.InvalidRecordFormatNumber, exception );
            }
        }

        private async Task<(string?, string[]?)> ReadNextRecordAsync( CancellationToken cancellationToken = default )
        {
            if (await parser.IsEndOfStreamAsync( cancellationToken ).ConfigureAwait( false ))
            {
                endOfFile = true;
                values = null;
                return (null, null);
            }
            try
            {
                var (record, results) = await parser.ReadRecordAsync( cancellationToken ).ConfigureAwait( false );
                ++physicalRecordNumber;
                return (record, results);
            }
            catch (DelimitedSyntaxException exception)
            {
                var currentContext = GetMetadata( null, null );
                throw new RecordProcessingException( currentContext, Resources.InvalidRecordFormatNumber, exception );
            }
        }

        /// <summary>
        ///     Gets the values for the current record.
        /// </summary>
        /// <returns>The values of the current record.</returns>
        public object?[] GetValues()
        {
            if (hasError)
            {
                throw new InvalidOperationException( Resources.ReadingWithErrors );
            }
            if (physicalRecordNumber == 0)
            {
                throw new InvalidOperationException( Resources.ReadNotCalled );
            }
            if (endOfFile || values is null)
            {
                throw new InvalidOperationException( Resources.NoMoreRecords );
            }
            var copy = new object[values.Length];
            Array.Copy( values, copy, values.Length );
            return copy;
        }

        private ExecutionContextCache<DelimitedSchema, GenericExecutionContext>? metadataExecutionContexts;

        private IRecordContext GetMetadata( DelimitedSchema? currentSchema, string? record )
        {
            if (recordContext is not null)
            {
                return recordContext;
            }
            var executionContext = (metadataExecutionContexts ??= new ExecutionContextCache<DelimitedSchema, GenericExecutionContext>( s => new GenericExecutionContext( s, parser.Options.Clone() ) )).Get( currentSchema );
            var currentContext = new GenericRecordContext( executionContext )
            {
                PhysicalRecordNumber = physicalRecordNumber,
                LogicalRecordNumber = logicalRecordNumber,
                Record = record
            };
            return currentContext;
        }

        IRecordContext IReaderWithMetadata.GetMetadata()
        {
            return GetMetadata( null, null );
        }

        internal void SetSchema( DelimitedSchema currentSchema )
        {
            schema = currentSchema;
        }
    }
}
