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
                var currentValues = ProcessRecord( record );
                if (currentValues is not null)
                {
                    return currentValues;
                }
            }
            return null;
        }

        private ExecutionContextCache<FixedLengthSchema, FixedLengthExecutionContext>? executionContexts;

        private FixedLengthRecordContext NewRecordContext( FixedLengthSchema currentSchema, string record, string[]? currentValues )
        {
            var executionContext = (executionContexts ??= new ExecutionContextCache<FixedLengthSchema, FixedLengthExecutionContext>( s => new FixedLengthExecutionContext( s!, options.Clone() ) )).Get( currentSchema );
            var currentContext = new FixedLengthRecordContext( executionContext )
            {
                PhysicalRecordNumber = physicalRecordNumber,
                LogicalRecordNumber = logicalRecordNumber,
                Record = record,
                Values = currentValues
            };
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
            var rawValues = PartitionRecord( currentSchema, record );
            if (rawValues is null || IsSkipped( currentSchema, record, rawValues ))
            {
                return null;
            }
            var currentValues = ParseValues( currentSchema, record, rawValues );
            if (currentValues is null)
            {
                return null;
            }
            var metadata = NewRecordContext( currentSchema, record, rawValues );
            recordContext = metadata;
            RecordParsed?.Invoke( this, new FixedLengthRecordParsedEventArgs( metadata, currentValues ) );
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
            var metadata = NewRecordContext( currentSchema, record, currentValues );
            var e = new FixedLengthRecordPartitionedEventArgs( metadata, currentValues );
            RecordPartitioned( this, e );
            return e.IsSkipped;
        }

        private object?[]? ParseValues( FixedLengthSchema currentSchema, string record, string[] rawValues )
        {
            var metadata = NewRecordContext( currentSchema, record, rawValues );
            metadata.ColumnError += ColumnError;
            try
            {
                return currentSchema.ParseValues( metadata, rawValues );
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
        ///     Cuts the record into one raw value per window. With a ragged right the last column runs from its offset
        ///     to the end of the record, however long or short that is, a window the record ends inside takes the
        ///     characters that are there, and a window past the end of the record is empty. Otherwise a record shorter
        ///     than the schema is refused, and a longer one when the options say so.
        /// </summary>
        private string[]? PartitionRecord( FixedLengthSchema currentSchema, string record )
        {
            var lengthError = options.IsRaggedRight ? null
                : record.Length < currentSchema.TotalWidth ? Resources.FixedLengthRecordTooShort
                : options.IsLongRecordRejected && record.Length > currentSchema.TotalWidth ? Resources.FixedLengthRecordTooLong
                : null;
            if (lengthError is not null)
            {
                var metadata = NewRecordContext( currentSchema, record, null );
                ProcessError( new RecordProcessingException( metadata, lengthError ) );
                return null;
            }
            var windows = currentSchema.Windows;
            var currentValues = new string[currentSchema.ColumnDefinitions.Count - currentSchema.ColumnDefinitions.MetadataCount];
            // The ragged column is the last window unless a trailing column follows it and already runs to the end.
            var raggedIndex = options.IsRaggedRight && windows.Count == currentSchema.ColumnDefinitions.Count ? windows.Count - 1 : -1;
            var offset = 0;
            for (int valueIndex = 0, columnIndex = 0; valueIndex != currentValues.Length; ++columnIndex)
            {
                var definition = currentSchema.ColumnDefinitions[columnIndex];
                if (definition is IMetadataColumn)
                {
                    continue;
                }
                var window = columnIndex < windows.Count ? windows[columnIndex] : null;
                var available = record.Length - offset;
                var runsToTheEnd = window is null || columnIndex == raggedIndex;
                string value;
                if (runsToTheEnd)
                {
                    value = available > 0 ? record[offset..] : string.Empty;
                }
                else
                {
                    value = available >= window!.Width ? record.Substring( offset, window.Width )
                        : available > 0 ? record[offset..]
                        : string.Empty;
                }
                if (window is not null)
                {
                    if (!definition.IsComplex)
                    {
                        var alignment = window.Alignment ?? options.Alignment;
                        value = alignment == FixedAlignment.LeftAligned
                            ? value.TrimEnd( window.FillCharacter ?? options.FillCharacter )
                            : value.TrimStart( window.FillCharacter ?? options.FillCharacter );
                    }
                    offset += window.Width;
                }
                currentValues[valueIndex] = value;
                ++valueIndex;
            }
            return currentValues;
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
