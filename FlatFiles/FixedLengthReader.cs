using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        ///     Initialises a new FixedLengthReader with the given schema.
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
        ///     Initialises a new FixedLengthReader with the given schema.
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

        private EventHandler<IRecordParsedEventArgs>? recordParsedUntyped;

        event EventHandler<IRecordParsedEventArgs>? IReader.RecordParsed
        {
            // Kept apart from the typed event. EventHandler<T> is contravariant, so an interface handler converts to
            // the typed delegate type, but Delegate.Combine refuses to join delegates of two runtime types once both
            // kinds are subscribed. A list of its own lets each be added and removed as itself.
            add => recordParsedUntyped += value;
            remove => recordParsedUntyped -= value;
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
            cancellationToken.ThrowIfCancellationRequested();
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
                var currentValues = ProcessRecord( record );
                if (currentValues is not null)
                {
                    return currentValues;
                }
            }
            return null;
        }

        private ExecutionContextCache<FixedLengthSchema, FixedLengthExecutionContext>? executionContexts;

        private FixedLengthRecordContext NewRecordContext( FixedLengthSchema currentSchema, string record, ValueRange[]? ranges, string[]? currentValues )
        {
            var executionContext = (executionContexts ??= new ExecutionContextCache<FixedLengthSchema, FixedLengthExecutionContext>( s => new FixedLengthExecutionContext( s!, options.Clone() ) )).Get( currentSchema );
            var currentContext = new FixedLengthRecordContext( executionContext )
            {
                PhysicalRecordNumber = physicalRecordNumber,
                LogicalRecordNumber = logicalRecordNumber,
                Record = record,
                Values = currentValues
            };
            if (currentValues is null && ranges is not null)
            {
                // Nothing has asked for the values as strings, so the context is told where they sit and copies them
                // out of the record only if a handler or an error reaches for them.
                currentContext.SetPartitions( ranges );
            }
            return currentContext;
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
                var currentValues = ProcessRecord( record );
                if (currentValues is not null)
                {
                    return currentValues;
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
            var currentSchema = GetSchema( record );
            if (currentSchema is null)
            {
                return null;
            }
            var ranges = PartitionRecord( currentSchema, record );
            if (ranges is null)
            {
                return null;
            }
            // A handler for the partitioned record is given the raw values as an array it can write into, and what it
            // leaves there is what gets parsed, so the values are copied out of the record whenever one is listening.
            string[]? rawValues = null;
            if (RecordPartitioned is not null)
            {
                rawValues = ValueRange.Materialise( record, string.Empty, ranges );
                if (IsSkipped( currentSchema, record, rawValues ))
                {
                    return null;
                }
            }
            var currentValues = ParseValues( currentSchema, record, ranges, rawValues );
            if (currentValues is null)
            {
                return null;
            }
            var metadata = NewRecordContext( currentSchema, record, ranges, rawValues );
            recordContext = metadata;
            if (RecordParsed is not null || recordParsedUntyped is not null)
            {
                var parsedArgs = new FixedLengthRecordParsedEventArgs( metadata, currentValues );
                RecordParsed?.Invoke( this, parsedArgs );
                recordParsedUntyped?.Invoke( this, parsedArgs );
            }
            return currentValues;
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

        private bool IsSkipped( FixedLengthSchema currentSchema, string record, string[] currentValues )
        {
            if (RecordPartitioned is null)
            {
                return false;
            }
            var metadata = NewRecordContext( currentSchema, record, null, currentValues );
            var e = new FixedLengthRecordPartitionedEventArgs( metadata, currentValues );
            RecordPartitioned( this, e );
            return e.IsSkipped;
        }

        private object?[]? ParseValues( FixedLengthSchema currentSchema, string record, ValueRange[] ranges, string[]? rawValues )
        {
            var metadata = NewRecordContext( currentSchema, record, ranges, rawValues );
            metadata.ColumnError += ColumnError;
            try
            {
                return rawValues is null
                    ? currentSchema.ParseValues( metadata, new RawRecord( record, default, ranges ) )
                    : currentSchema.ParseValues( metadata, rawValues );
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

        /// <summary>
        ///     Finds where each raw value sits within the record, one per window, without copying any of them out of
        ///     it. With a ragged right the last column runs from its offset to the end of the record, however long or
        ///     short that is, a window the record ends inside takes the characters that are there, and a window past
        ///     the end of the record is empty. Otherwise a record shorter than the schema is refused, and a longer one
        ///     when the options say so.
        /// </summary>
        private ValueRange[]? PartitionRecord( FixedLengthSchema currentSchema, string record )
        {
            var lengthError = options.IsRaggedRight ? null
                : record.Length < currentSchema.TotalWidth ? Resources.FixedLengthRecordTooShort
                : options.IsLongRecordRejected && record.Length > currentSchema.TotalWidth ? Resources.FixedLengthRecordTooLong
                : null;
            if (lengthError is not null)
            {
                var metadata = NewRecordContext( currentSchema, record, null, null );
                ProcessError( new RecordProcessingException( metadata, lengthError ) );
                return null;
            }
            var windows = currentSchema.Windows;
            var ranges = new ValueRange[currentSchema.ColumnDefinitions.Count - currentSchema.ColumnDefinitions.MetadataCount];
            // The ragged column is the last window unless a trailing column follows it and already runs to the end.
            var raggedIndex = options.IsRaggedRight && windows.Count == currentSchema.ColumnDefinitions.Count ? windows.Count - 1 : -1;
            var offset = 0;
            for (int valueIndex = 0, columnIndex = 0; valueIndex != ranges.Length; ++columnIndex)
            {
                var definition = currentSchema.ColumnDefinitions[columnIndex];
                if (definition is IMetadataColumn)
                {
                    continue;
                }
                var window = columnIndex < windows.Count ? windows[columnIndex] : null;
                // A window the record never reaches starts at its end, so that its value is empty rather than out of
                // the record altogether.
                var start = offset < record.Length ? offset : record.Length;
                var available = record.Length - start;
                var runsToTheEnd = window is null || columnIndex == raggedIndex;
                var length = runsToTheEnd || available < window!.Width ? available : window.Width;
                if (window is not null)
                {
                    if (!definition.IsComplex)
                    {
                        var fillCharacter = window.FillCharacter ?? options.FillCharacter;
                        var value = record.AsSpan( start, length );
                        if ((window.Alignment ?? options.Alignment) == FixedAlignment.LeftAligned)
                        {
                            length = value.TrimEnd( fillCharacter ).Length;
                        }
                        else
                        {
                            var trimmed = value.TrimStart( fillCharacter );
                            start += length - trimmed.Length;
                            length = trimmed.Length;
                        }
                    }
                    offset += window.Width;
                }
                ranges[valueIndex] = new ValueRange( start, length );
                ++valueIndex;
            }
            return ranges;
        }

        private FixedLengthSchema? GetSchema( string record )
        {
            if (schemaSelector is null)
            {
                return schema;
            }
            var currentSchema = schemaSelector.GetSchema( record );
            if (currentSchema is not null)
            {
                return currentSchema;
            }
            var currentContext = GetMetadata( null, record );
            ProcessError( new RecordProcessingException( currentContext, Resources.MissingMatcher ) );
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

        private IRecordContext GetMetadata( FixedLengthSchema? currentSchema, string? record )
        {
            if (recordContext is not null)
            {
                return recordContext;
            }
            var executionContext = (metadataExecutionContexts ??= new ExecutionContextCache<FixedLengthSchema, GenericExecutionContext>( s => new GenericExecutionContext( s, options.Clone() ) )).Get( currentSchema );
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
    }
}
