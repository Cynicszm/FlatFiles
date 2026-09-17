using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;
using FlatFiles.Properties;

namespace FlatFiles
{
    /// <summary>
    ///     Extracts records from a file that has value in fixed-length columns.
    /// </summary>
    public sealed class FixedLengthReader : IReaderWithMetadata
    {
        private readonly FixedLengthRecordParser parser;
        private readonly FixedLengthSchemaSelector? schemaSelector;
        private readonly FixedLengthSchema? schema;
        private readonly FixedLengthOptions options;
        private int physicalRecordNumber;
        private int logicalRecordNumber;
        private IRecordContext? recordContext;
        private object?[]? values;
        private bool endOfFile;
        private bool hasError;

        /// <summary>
        ///     Initializes a new FixedLengthReader with the given schema.
        /// </summary>
        /// <param name="reader">A reader over the fixed-length document.</param>
        /// <param name="schema">The schema of the fixed-length document.</param>
        /// <param name="options">The options controlling how the fixed-length document is read.</param>
        /// <exception cref="ArgumentNullException">The reader is null.</exception>
        /// <exception cref="ArgumentNullException">The schema is null.</exception>
        public FixedLengthReader( TextReader reader, FixedLengthSchema schema, FixedLengthOptions? options = null )
            : this( reader, schema, options, true )
        {
        }

        /// <summary>
        ///     Initializes a new FixedLengthReader with the given schema.
        /// </summary>
        /// <param name="reader">A reader over the fixed-length document.</param>
        /// <param name="schemaSelector">The schema selector configured to determine the schema dynamically.</param>
        /// <param name="options">The options controlling how the fixed-length document is read.</param>
        /// <exception cref="ArgumentNullException">The reader is null.</exception>
        /// <exception cref="ArgumentNullException">The schema selector is null.</exception>
        public FixedLengthReader( TextReader reader, FixedLengthSchemaSelector schemaSelector, FixedLengthOptions? options = null )
            : this( reader, null, options, false )
        {
            this.schemaSelector = schemaSelector ?? throw new ArgumentNullException( nameof( schemaSelector ) );
        }

        private FixedLengthReader( TextReader reader, FixedLengthSchema? schema, FixedLengthOptions? options = null, bool hasSchema = true )
        {
            ArgumentNullException.ThrowIfNull( reader );
            if (hasSchema && schema is null)
            {
                throw new ArgumentNullException( nameof( schema ) );
            }
            this.options = options is null ? new FixedLengthOptions() : options.Clone();
            this.schema = schema;
            parser = new FixedLengthRecordParser( reader, this.schema, this.options );
        }

        /// <summary>
        ///     Raised when a record is read from the source file, before it is partitioned.
        /// </summary>
        public event EventHandler<FixedLengthRecordReadEventArgs>? RecordRead;

        /// <summary>
        ///     Raised after a record is partitioned, before it is parsed.
        /// </summary>
        public event EventHandler<FixedLengthRecordPartitionedEventArgs>? RecordPartitioned;

        /// <summary>
        ///     Raised after a record is parsed.
        /// </summary>
        public event EventHandler<FixedLengthRecordParsedEventArgs>? RecordParsed;

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

        IOptions IReader.Options => options;

        /// <summary>
        ///     Gets the schema being used by the parser.
        /// </summary>
        /// <returns>The schema being used by the parser.</returns>
        public FixedLengthSchema? GetSchema()
        {
            return schema;
        } 

        ISchema? IReader.GetSchema()
        {
            return GetSchema();
        }

        /// <summary>
        ///     Gets the schema being used by the parser.
        /// </summary>
        /// <returns>The schema being used by the parser.</returns>
        public Task<FixedLengthSchema?> GetSchemaAsync()
        {
            return GetSchemaAsync( CancellationToken.None );
        }

        /// <summary>
        ///     Gets the schema being used by the parser.
        /// </summary>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        /// <returns>The schema being used by the parser.</returns>
        public Task<FixedLengthSchema?> GetSchemaAsync( CancellationToken cancellationToken )
{
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult( schema );
        }

        Task<ISchema?> IReader.GetSchemaAsync()
{
            return Task.FromResult<ISchema?>( schema );
        }

        Task<ISchema?> IReader.GetSchemaAsync( CancellationToken cancellationToken )
{
            return Task.FromResult<ISchema?>( schema );
        }

        /// <summary>
        ///     Reads the next record from the file.
        /// </summary>
        /// <returns>True if the next record was parsed; otherwise, false if all files are read.</returns>
        public bool Read()
        {
            if (hasError)
            {
                throw new InvalidOperationException( Resources.ReadingWithErrors );
            }
            recordContext = null;
            HandleHeader();
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

        private void HandleHeader()
        {
            if (physicalRecordNumber == 0 && options.IsFirstRecordHeader)
            {
                SkipInternal();
            }
        }

        private object?[]? ParsePartitions()
        {
            while (!endOfFile)
            {
                var record = ReadNextRecord();
                var values = ProcessRecord( record );
                if (values is not null)
                {
                    return values;
                }
            }
            return null;
        }

        private ExecutionContextCache<FixedLengthSchema, FixedLengthExecutionContext>? executionContexts;

        private FixedLengthRecordContext NewRecordContext( FixedLengthSchema schema, string record, string[]? values )
        {
            var executionContext = (executionContexts ??= new( s => new FixedLengthExecutionContext( s!, options.Clone() ) )).Get( schema );
            var recordContext = new FixedLengthRecordContext( executionContext )
            {
                PhysicalRecordNumber = physicalRecordNumber,
                LogicalRecordNumber = logicalRecordNumber,
                Record = record,
                Values = values
            };
            return recordContext;
        }

        /// <summary>
        ///     Reads the next record from the file.
        /// </summary>
        /// <returns>True if the next record was parsed; otherwise, false if all files are read.</returns>
        public ValueTask<bool> ReadAsync()
        {
            return ReadAsync( CancellationToken.None );
        }

        /// <summary>
        ///     Reads the next record from the file.
        /// </summary>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        /// <returns>True if the next record was parsed; otherwise, false if all files are read.</returns>
        public async ValueTask<bool> ReadAsync( CancellationToken cancellationToken )
{
            cancellationToken.ThrowIfCancellationRequested();
            if (hasError)
            {
                throw new InvalidOperationException( Resources.ReadingWithErrors );
            }
            recordContext = null;
            await HandleHeaderAsync( cancellationToken ).ConfigureAwait( false );
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

        private async Task HandleHeaderAsync( CancellationToken cancellationToken = default )
{
            if (physicalRecordNumber == 0 && options.IsFirstRecordHeader)
            {
                await SkipAsyncInternal( cancellationToken ).ConfigureAwait( false );
            }
        }

        private async Task<object?[]?> ParsePartitionsAsync( CancellationToken cancellationToken = default )
{
            while (!endOfFile)
            {
                var record = await ReadNextRecordAsync( cancellationToken ).ConfigureAwait( false );
                var values = ProcessRecord( record );
                if (values is not null)
                {
                    return values;
                }
            }
            return null;
        }

        private object?[]? ProcessRecord( string? record )
        {
            if (record is null || IsSkipped( record ))
            {
                return null;
            }
            var schema = GetSchema( record );
            if (schema is null)
            {
                return null;
            }
            var rawValues = PartitionRecord( schema, record );
            if (rawValues is null || IsSkipped( schema, record, rawValues ))
            {
                return null;
            }
            var values = ParseValues( schema, record, rawValues );
            if (values is null)
            {
                return null;
            }
            var metadata = NewRecordContext( schema, record, rawValues );
            recordContext = metadata;
            RecordParsed?.Invoke( this, new FixedLengthRecordParsedEventArgs( metadata, values ) );
            return values;
        }

        private bool IsSkipped( string record )
        {
            if (RecordRead is null)
            {
                return false;
            }
            var e = new FixedLengthRecordReadEventArgs( record );
            RecordRead( this, e );
            return e.IsSkipped;
        }

        private bool IsSkipped( FixedLengthSchema schema, string record, string[] values )
        {
            if (RecordPartitioned is null)
            {
                return false;
            }
            var metadata = NewRecordContext( schema, record, values );
            var e = new FixedLengthRecordPartitionedEventArgs( metadata, values );
            RecordPartitioned( this, e );
            return e.IsSkipped;
        }

        private object?[]? ParseValues( FixedLengthSchema schema, string record, string[] rawValues )
        {
            var metadata = NewRecordContext( schema, record, rawValues );
            metadata.ColumnError += ColumnError;
            try
            {
                return schema.ParseValues( metadata, rawValues );
            }
            catch (FlatFileException exception)
            {
                ProcessError( new RecordProcessingException( metadata, Resources.InvalidRecordConversion, exception ) );
                return null;
            }
        }

        /// <summary>
        ///     Skips the next record from the file.
        /// </summary>
        /// <returns>True if the next record was skipped; otherwise, false if all records are read.</returns>
        /// <remarks>The previously parsed values remain available.</remarks>
        public bool Skip()
        {
            if (hasError)
            {
                throw new InvalidOperationException( Resources.ReadingWithErrors );
            }
            HandleHeader();
            return SkipInternal();
        }

        private bool SkipInternal()
        {
            var record = ReadNextRecord();
            return record is not null;
        }

        /// <summary>
        ///     Skips the next record from the file.
        /// </summary>
        /// <returns>True if the next record was skipped; otherwise, false if all records are read.</returns>
        /// <remarks>The previously parsed values remain available.</remarks>
        public ValueTask<bool> SkipAsync()
        {
            return SkipAsync( CancellationToken.None );
        }

        /// <summary>
        ///     Skips the next record from the file.
        /// </summary>
        /// <param name="cancellationToken">The token to observe while waiting for the operation to complete.</param>
        /// <returns>True if the next record was skipped; otherwise, false if all records are read.</returns>
        /// <remarks>The previously parsed values remain available.</remarks>
        public async ValueTask<bool> SkipAsync( CancellationToken cancellationToken )
{
            cancellationToken.ThrowIfCancellationRequested();
            if (hasError)
            {
                throw new InvalidOperationException( Resources.ReadingWithErrors );
            }
            await HandleHeaderAsync( cancellationToken ).ConfigureAwait( false );
            return await SkipAsyncInternal( cancellationToken ).ConfigureAwait( false );
        }

        private async ValueTask<bool> SkipAsyncInternal( CancellationToken cancellationToken = default )
{
            var record = await ReadNextRecordAsync( cancellationToken ).ConfigureAwait( false );
            return record is not null;
        }

        private string[]? PartitionRecord( FixedLengthSchema schema, string record )
        {
            if (record.Length < schema.TotalWidth)
            {
                var metadata = NewRecordContext( schema, record, null );
                ProcessError( new RecordProcessingException( metadata, Resources.FixedLengthRecordTooShort ) );
                return null;
            }
            if (options.IsLongRecordRejected && record.Length > schema.TotalWidth)
            {
                var metadata = NewRecordContext( schema, record, null );
                ProcessError( new RecordProcessingException( metadata, Resources.FixedLengthRecordTooLong ) );
                return null;
            }
            var windows = schema.Windows;
            var values = new string[schema.ColumnDefinitions.Count - schema.ColumnDefinitions.MetadataCount];
            int offset = 0;
            for (int valueIndex = 0, columnIndex = 0; valueIndex != values.Length; ++columnIndex)
            {
                var definition = schema.ColumnDefinitions[columnIndex];
                if (definition is not IMetadataColumn)
                {
                    Window? window = columnIndex < windows.Count ? windows[columnIndex] : null;
                    string value;
                    if (window is null)
                    {
                        value = record[offset..];
                    }
                    else
                    {
                        value = record.Substring( offset, window.Width );
                        if (!definition.IsComplex)
                        {
                            var alignment = window.Alignment ?? options.Alignment;
                            value = alignment == FixedAlignment.LeftAligned
                                ? value.TrimEnd( window.FillCharacter ?? options.FillCharacter )
                                : value.TrimStart( window.FillCharacter ?? options.FillCharacter );
                        }
                        offset += window.Width;
                    }
                    values[valueIndex] = value;
                    ++valueIndex;
                }
            }
            return values;
        }

        private FixedLengthSchema? GetSchema( string record )
        {
            if (schemaSelector is null)
            {
                return this.schema;
            }
            FixedLengthSchema? schema = schemaSelector.GetSchema( record );
            if (schema is not null)
            {
                return schema;
            }
            var recordContext = GetMetadata( null, record );
            ProcessError( new RecordProcessingException( recordContext, Resources.MissingMatcher ) );
            return null;
        }

        private string? ReadNextRecord()
        {
            if (parser.IsEndOfStream())
            {
                endOfFile = true;
                return null;
            }
            var record = parser.ReadRecord();
            ++physicalRecordNumber;
            return record;
        }

        private async Task<string?> ReadNextRecordAsync( CancellationToken cancellationToken = default )
{
            if (await parser.IsEndOfStreamAsync( cancellationToken ).ConfigureAwait( false ))
            {
                endOfFile = true;
                return null;
            }
            var record = await parser.ReadRecordAsync( cancellationToken ).ConfigureAwait( false );
            ++physicalRecordNumber;
            return record;
        }

        private void ProcessError( RecordProcessingException exception )
        {
            if (RecordError is not null)
            {
                var args = new RecordErrorEventArgs( exception );
                RecordError( this, args );
                if (args.IsHandled)
                {
                    return;
                }
            }
            throw exception;
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

        private ExecutionContextCache<FixedLengthSchema, GenericExecutionContext>? metadataExecutionContexts;

        private IRecordContext GetMetadata( FixedLengthSchema? schema, string? record )
        {
            if (this.recordContext is not null)
            {
                return this.recordContext;
            }
            var executionContext = (metadataExecutionContexts ??= new( s => new GenericExecutionContext( s, options.Clone() ) )).Get( schema );
            var recordContext = new GenericRecordContext( executionContext )
            {
                PhysicalRecordNumber = physicalRecordNumber,
                LogicalRecordNumber = logicalRecordNumber,
                Record = record
            };
            return recordContext;
        }

        IRecordContext IReaderWithMetadata.GetMetadata()
        {
            return GetMetadata( null, null );
        }
    }
}
