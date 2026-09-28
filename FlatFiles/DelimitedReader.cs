using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FlatFiles.Properties;

namespace FlatFiles
{
    /// <summary>
    ///     Extracts records from a file containing delimited values.
    /// </summary>
    public sealed class DelimitedReader : IReaderWithMetadata, IRawValueSource
    {
        private readonly DelimitedRecordParser parser;
        private readonly DelimitedSchemaSelector? schemaSelector;
        private DelimitedSchema? schema;

        /// <summary>
        ///     How many values the header carried, once the columns have been matched to it, or -1 where they have
        ///     not been. It is what a record's length is judged against from then on.
        /// </summary>
        private int matchedValueCount = -1;

        private IRecordContext? recordContext;
        private int physicalRecordNumber;
        private int logicalRecordNumber;
        private object?[]? values;
        private bool endOfFile;
        private bool hasError;
        private int generation;

        int IRawValueSource.Generation => generation;

        string[] IRawValueSource.MaterialiseValues()
        {
            return parser.Values.Materialise();
        }

        /// <summary>
        ///     Initialises a new DelimitedReader with no schema.
        /// </summary>
        /// <param name="reader">A reader over the delimited document.</param>
        /// <param name="options">The options controlling how the delimited document is read.</param>
        /// <exception cref="ArgumentNullException">The reader is null.</exception>
        public DelimitedReader( TextReader reader, DelimitedOptions? options = null )
            : this( reader, null, options, false )
        {
        }

        /// <summary>
        ///     Initialises a new DelimitedReader with the given schema.
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
        ///     Initialises a new DelimitedReader with the given schema.
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

        /// <summary>
        ///     Initialises a new DelimitedReader over a stream, with no schema.
        /// </summary>
        /// <param name="stream">A stream over the delimited document.</param>
        /// <param name="encoding">The encoding to read the stream as, or null for UTF-8.</param>
        /// <remarks>
        ///     A byte order mark is honoured whatever encoding is asked for, and taken off the text rather than
        ///     left to become part of the first value. The stream is left open: a reader does not own what it was
        ///     handed.
        /// </remarks>
        /// <param name="options">The options controlling how the delimited document is read.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        public DelimitedReader( Stream stream, DelimitedOptions? options = null, Encoding? encoding = null )
            : this( StreamText.Over( stream, encoding ), null, options, false )
        {
        }

        /// <summary>
        ///     Initialises a new DelimitedReader over a stream, with the given schema.
        /// </summary>
        /// <param name="stream">A stream over the delimited document.</param>
        /// <param name="encoding">The encoding to read the stream as, or null for UTF-8.</param>
        /// <remarks>
        ///     A byte order mark is honoured whatever encoding is asked for, and taken off the text rather than
        ///     left to become part of the first value. The stream is left open: a reader does not own what it was
        ///     handed.
        /// </remarks>
        /// <param name="schema">The schema of the delimited document.</param>
        /// <param name="options">The options controlling how the delimited document is read.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="ArgumentNullException">The schema is null.</exception>
        public DelimitedReader( Stream stream, DelimitedSchema schema, DelimitedOptions? options = null, Encoding? encoding = null )
            : this( StreamText.Over( stream, encoding ), schema, options, true )
        {
        }

        /// <summary>
        ///     Initialises a new DelimitedReader over a stream, choosing the schema per record.
        /// </summary>
        /// <param name="stream">A stream over the delimited document.</param>
        /// <param name="encoding">The encoding to read the stream as, or null for UTF-8.</param>
        /// <remarks>
        ///     A byte order mark is honoured whatever encoding is asked for, and taken off the text rather than
        ///     left to become part of the first value. The stream is left open: a reader does not own what it was
        ///     handed.
        /// </remarks>
        /// <param name="schemaSelector">The schema selector configured to determine the schema dynamically.</param>
        /// <param name="options">The options controlling how the delimited document is read.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="ArgumentNullException">The schema selector is null.</exception>
        public DelimitedReader( Stream stream, DelimitedSchemaSelector schemaSelector, DelimitedOptions? options = null, Encoding? encoding = null )
            : this( StreamText.Over( stream, encoding ), null, options, false )
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
                CheckHeaderMatchingIsPossible();
                return schema;
            }
            if (schemaSelector is not null || schema is not null)
            {
                CheckHeaderMatchingIsPossible();
                var read = SkipInternal();
                if (read && schema is not null)
                {
                    MatchHeader( schema );
                }
                return schema;
            }
            var header = ReadNextRecord();
            if (header is null)
            {
                // Do not treat a missing schema in an empty file as an error.
                return null;
            }
            schema = CreateSchemaFromHeader( parser.Values.Materialise() );
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
                CheckHeaderMatchingIsPossible();
                return schema;
            }
            if (schemaSelector is not null || schema is not null)
            {
                CheckHeaderMatchingIsPossible();
                var read = await SkipAsyncInternal( cancellationToken ).ConfigureAwait( false );
                if (read && schema is not null)
                {
                    MatchHeader( schema );
                }
                return schema;
            }
            var header = await ReadNextRecordAsync( cancellationToken ).ConfigureAwait( false );
            if (header is null)
            {
                // Do not treat a missing schema in an empty file as an error.
                return null;
            }
            schema = CreateSchemaFromHeader( parser.Values.Materialise() );
            return schema;
        }

        /// <summary>
        ///     Refuses a reader asked to match columns to a header that has no header to match, or that chooses its
        ///     schema per record. Both are configuration mistakes rather than anything about the file, so they are
        ///     raised at the first read rather than being reported record by record.
        /// </summary>
        private void CheckHeaderMatchingIsPossible()
        {
            if (parser.Options.HeaderMatching == HeaderMatching.ByPosition)
            {
                return;
            }
            if (!parser.Options.IsFirstRecordSchema)
            {
                throw new InvalidOperationException( Resources.HeaderMatchingWithoutHeader );
            }
            if (schemaSelector is not null)
            {
                throw new InvalidOperationException( Resources.HeaderMatchingWithSelector );
            }
        }

        /// <summary>
        ///     Works out where in a record each column of the schema sits, from the header just read, and hands the
        ///     answer to the execution context the records will be read through. Done once, so that reading a record
        ///     costs one lookup per column rather than any matching.
        /// </summary>
        /// <param name="currentSchema">The schema the file is being read against.</param>
        private void MatchHeader( DelimitedSchema currentSchema )
        {
            if (parser.Options.HeaderMatching == HeaderMatching.ByPosition)
            {
                return;
            }
            var headings = HeadingPositions( parser.Values );
            var columnDefinitions = currentSchema.ColumnDefinitions;
            var sourceMap = new int[columnDefinitions.Count];
            for (var columnIndex = 0; columnIndex != sourceMap.Length; ++columnIndex)
            {
                sourceMap[columnIndex] = SourceOf( columnDefinitions[columnIndex], columnIndex, headings );
            }
            ExecutionContextFor( currentSchema ).SourceMap = sourceMap;
            // With a header matched, the header says how wide a record should be rather than the schema does: a
            // file may carry columns this schema does not declare, and every mapped position came from the header,
            // so a record as wide as the header reaches all of them.
            matchedValueCount = parser.Values.Count;
        }

        /// <summary>
        ///     Where in the record the column's value sits, or -1 where nothing in the record feeds it.
        /// </summary>
        /// <param name="definition">The column being placed.</param>
        /// <param name="columnIndex">Its position in the schema, for the message if it cannot be placed.</param>
        /// <param name="headings">Where each heading sits, with -1 against a name the header carries twice.</param>
        /// <returns>The position in the record, or -1.</returns>
        private int SourceOf( IColumnDefinition definition, int columnIndex, Dictionary<string, int> headings )
        {
            if (definition is IMetadataColumn)
            {
                // Filled from the record's context rather than from the record, so it wants no heading and it is
                // no matter that the header does not carry one.
                return -1;
            }
            var columnName = definition.ColumnName;
            if (string.IsNullOrEmpty( columnName ))
            {
                throw new FlatFileException( string.Format( CultureInfo.CurrentCulture, Resources.HeaderUnnamedColumn, columnIndex ) );
            }
            if (!headings.TryGetValue( columnName!, out var position ))
            {
                if (parser.Options.HeaderMatching == HeaderMatching.ByNameWhereFound)
                {
                    return -1;
                }
                throw new FlatFileException( string.Format( CultureInfo.CurrentCulture, Resources.HeaderMissingColumn, columnName ) );
            }
            if (position < 0)
            {
                throw new FlatFileException( string.Format( CultureInfo.CurrentCulture, Resources.HeaderAmbiguousColumn, columnName ) );
            }
            return position;
        }

        /// <summary>
        ///     Where each heading of the header sits. A name the header carries more than once is held as -1: which
        ///     of them a column means cannot be decided, but it is only an error if a column asks for that name.
        /// </summary>
        /// <param name="header">The header record.</param>
        /// <returns>The position of each heading.</returns>
        private Dictionary<string, int> HeadingPositions( RawRecord header )
        {
            var headings = new Dictionary<string, int>( header.Count, parser.Options.HeaderComparer );
            for (var position = 0; position != header.Count; ++position)
            {
                var heading = header[position].ToString();
                headings[heading] = headings.ContainsKey( heading ) ? -1 : position;
            }
            return headings;
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
                var record = ReadNextRecord();
                var currentValues = ProcessRecord( record );
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
                var record = await ReadNextRecordAsync( cancellationToken );
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
            if (record is null)
            {
                return null;
            }
            var rawRecord = parser.Values;
            // A schema selector is given the raw values, and a handler for the record read is given them as an array
            // it can write into, where what it leaves behind is what gets parsed. Either way they are copied out of
            // the buffer before the record is parsed; otherwise the columns read them where they lie and the record
            // context copies them out only if something asks it for them, and only while this record is current.
            var rawValues = (schemaSelector is not null && schemaSelector.NeedsValues) || RecordRead is not null
                ? rawRecord.Materialise()
                : null;
            var currentSchema = schemaSelector is null ? schema : GetSchema( record, parser.RecordText, rawValues );
            if (currentSchema is null)
            {
                // A selector that matched nothing has already reported the record, and a handler that let reading
                // continue means skip it, as it does for every other record error. Only a reader given neither a
                // schema nor a selector builds one from the record itself.
                if (schemaSelector is not null)
                {
                    return null;
                }
                currentSchema = DelimitedSchema.BuildDynamicSchema( parser.Options, rawRecord.Count );
            }
            var wanted = matchedValueCount < 0 ? WantedValueCount( currentSchema ) : matchedValueCount;
            if (rawRecord.Count < wanted && parser.Options.ShortRecordHandling == ShortRecordHandling.Pad)
            {
                // Padded once the schema is known, since the schema is what says how long the record should be,
                // and before the record context is built, so that what a handler is given and what the schema
                // goes on to parse are the same values. A selector has already chosen by this point, on the
                // values the record actually carried.
                parser.PadTo( wanted );
                rawRecord = parser.Values;
                rawValues = Pad( rawValues, wanted );
            }
            var currentContext = NewRecordContext( currentSchema, record, rawValues );
            recordContext = currentContext;
            if (rawValues is not null && IsSkipped( currentContext, rawValues ))
            {
                return null;
            }
            var lengthError = LengthError( parser.Options, rawRecord.Count, wanted );
            if (lengthError is not null)
            {
                ProcessError( new RecordProcessingException( currentContext, lengthError ) );
                return null;
            }
            recordSchema = currentSchema;
            valuesAreParsed = true;
            if (Assembler is not null && rawValues is null && RecordParsed is null && recordParsedUntyped is null)
            {
                // Nothing here needs the values as objects, so they go straight onto the entity instead.
                if (!Assemble( currentContext, currentSchema, rawRecord ))
                {
                    return null;
                }
                valuesAreParsed = false;
                return parsedValues;
            }
            var currentValues = ParseValues( currentContext, rawRecord, rawValues );
            if (currentValues is null)
            {
                return null;
            }
            if (RecordParsed is not null || recordParsedUntyped is not null)
            {
                var parsedArgs = new DelimitedRecordParsedEventArgs( currentContext, currentValues );
                RecordParsed?.Invoke( this, parsedArgs );
                recordParsedUntyped?.Invoke( this, parsedArgs );
            }
            return currentValues;
        }


        private DelimitedSchema? GetSchema( string record, ReadOnlySpan<char> recordText, string[]? rawValues )
        {
            if (schemaSelector is null)
            {
                return schema;
            }
            var currentSchema = schemaSelector.GetSchema( recordText, rawValues );
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


        // One array of parsed values for the whole read. Every slot is written for every record, and the only
        // caller that may keep it is a parsed record handler, which is given one of its own instead.
        private object?[] parsedValues = [];

        private object?[] GetParsedValues( ISchema currentSchema )
        {
            var count = currentSchema.ColumnDefinitions.PhysicalCount;
            if (RecordParsed is not null || recordParsedUntyped is not null)
            {
                return new object?[count];
            }
            if (parsedValues.Length != count)
            {
                parsedValues = new object?[count];
            }
            return parsedValues;
        }

        // A typed reader installs this to take the record onto an entity itself, which is the only way a value
        // reaches a property without being boxed on the way. It is used only where nothing else needs the parsed
        // values: no handler for the parsed record, and no values already made strings of for a selector or a
        // handler for the record read.
        internal IEntityAssembler? Assembler { get; set; }

        private bool valuesAreParsed;

        private DelimitedSchema? recordSchema;

        /// <summary>
        ///     Parses the values the assembler took instead, for a caller that asks the reader for them anyway.
        ///     Only a caller holding this reader can get here, because a type mapper makes its own; a column on that
        ///     path carries no parsing hooks, so reading its value a second time changes nothing.
        /// </summary>
        private void EnsureValuesParsed()
        {
            if (valuesAreParsed || recordSchema is null || recordContext is not DelimitedRecordContext context)
            {
                return;
            }
            values = recordSchema.ParseValues( context, parser.Values, GetParsedValues( recordSchema ) );
            valuesAreParsed = true;
        }

        private ExecutionContextCache<DelimitedSchema, DelimitedExecutionContext>? executionContexts;

        private DelimitedExecutionContext ExecutionContextFor( DelimitedSchema currentSchema )
        {
            executionContexts ??= new ExecutionContextCache<DelimitedSchema, DelimitedExecutionContext>( s => new DelimitedExecutionContext( s!, parser.Options.Clone() ) );
            return executionContexts.Get( currentSchema );
        }

        private DelimitedRecordContext NewRecordContext( DelimitedSchema currentSchema, string record, string[]? currentValues )
        {
            var executionContext = ExecutionContextFor( currentSchema );
            var currentContext = new DelimitedRecordContext( executionContext )
            {
                PhysicalRecordNumber = physicalRecordNumber,
                LogicalRecordNumber = logicalRecordNumber,
                Record = record,
                Values = currentValues
            };
            if (currentValues is null)
            {
                // Nothing has asked for the values as strings, so the context is pointed at the reader and copies
                // them out only if a handler or an error reaches for them while this record is still current.
                currentContext.SetValueSource( this );
            }
            return currentContext;
        }

        /// <summary>
        ///     How many values the record has to carry for the schema to read it. Every column takes one except a
        ///     metadata column, which is filled from the record's context rather than from the record; an ignored
        ///     column takes one and throws it away.
        /// </summary>
        /// <param name="currentSchema">The schema the record is being read against.</param>
        /// <returns>The number of values wanted.</returns>
        private static int WantedValueCount( DelimitedSchema currentSchema )
        {
            var columnDefinitions = currentSchema.ColumnDefinitions;
            return columnDefinitions.Count - columnDefinitions.MetadataCount;
        }

        /// <summary>
        ///     What is wrong with the length of the record, or null if nothing is. A record too short to fill the
        ///     schema is always refused; one carrying more than the schema asks for is refused only where the
        ///     options say so, and otherwise has its surplus discarded.
        /// </summary>
        /// <param name="options">The options the reader was given.</param>
        /// <param name="valueCount">How many values the record carries.</param>
        /// <param name="wanted">How many the schema wants.</param>
        /// <returns>The error to report the record with, or null to read it.</returns>
        private static string? LengthError( DelimitedOptions options, int valueCount, int wanted )
        {
            if (valueCount < wanted)
            {
                return Resources.DelimitedRecordWrongNumberOfColumns;
            }
            if (valueCount > wanted && options.LongRecordHandling == LongRecordHandling.Refuse)
            {
                return Resources.DelimitedRecordTooManyColumns;
            }
            return null;
        }

        /// <summary>
        ///     Gives an array of values that was copied out before the record was padded the same empty values the
        ///     record itself was given.
        /// </summary>
        /// <param name="rawValues">The values copied out of the record, or null if none were.</param>
        /// <param name="wanted">The number of values the record should have.</param>
        /// <returns>The padded values.</returns>
        private static string[]? Pad( string[]? rawValues, int wanted )
        {
            if (rawValues is null)
            {
                return null;
            }
            var padded = new string[wanted];
            rawValues.CopyTo( padded, 0 );
            for (var index = rawValues.Length; index != wanted; ++index)
            {
                padded[index] = string.Empty;
            }
            return padded;
        }

        private bool Assemble( DelimitedRecordContext currentContext, DelimitedSchema schema, RawRecord rawRecord )
        {
            try
            {
                currentContext.ColumnError += ColumnError;
                Assembler!.Assemble( currentContext, schema, rawRecord );
                return true;
            }
            catch (FlatFileException exception)
            {
                ProcessError( new RecordProcessingException( currentContext, Resources.InvalidRecordConversion, exception ) );
                return false;
            }
        }

        private object?[]? ParseValues( DelimitedRecordContext currentContext, RawRecord rawRecord, string[]? rawValues )
        {
            try
            {
                currentContext.ColumnError += ColumnError;
                var currentSchema = currentContext.ExecutionContext.Schema;
                var destination = GetParsedValues( currentSchema );
                return rawValues is null
                    ? currentSchema.ParseValues( currentContext, rawRecord, destination )
                    : currentSchema.ParseValues( currentContext, rawValues, destination );
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
            var record = ReadNextRecord();
            return record is not null;
        }

        private async ValueTask<bool> SkipAsyncInternal( CancellationToken cancellationToken = default )
        {
            var record = await ReadNextRecordAsync( cancellationToken ).ConfigureAwait( false );
            return record is not null;
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

        private string? ReadNextRecord()
        {
            while (true)
            {
                // Everything the last record's context could report goes with the buffer the parser is about to reuse.
                ++generation;
                if (parser.IsEndOfStream())
                {
                    endOfFile = true;
                    values = null;
                    return null;
                }
                string record;
                try
                {
                    record = parser.ReadRecord();
                    ++physicalRecordNumber;
                }
                catch (DelimitedSyntaxException exception)
                {
                    // If we cannot read the next record, we cannot process it or allow it to be ignored.
                    // We must treat it as a fatal error.
                    var currentContext = GetMetadata( null, null );
                    throw new RecordProcessingException( currentContext, Resources.InvalidRecordFormatNumber, exception );
                }
                // Asked of the values rather than the text, because this reader does not build the text unless
                // PreserveRecordText asks for it. Passed over here rather than after parsing, so the record never
                // reaches a schema selector, a handler or the header - and asking a handler to do this instead
                // makes every record in the file copy its values out of the buffer.
                if (!PassedOverRecord.Matches( parser.Options, parser.Values ))
                {
                    return record;
                }
            }
        }

        private async Task<string?> ReadNextRecordAsync( CancellationToken cancellationToken = default )
        {
            while (true)
            {
                ++generation;
                if (await parser.IsEndOfStreamAsync( cancellationToken ).ConfigureAwait( false ))
                {
                    endOfFile = true;
                    values = null;
                    return null;
                }
                string record;
                try
                {
                    record = await parser.ReadRecordAsync( cancellationToken ).ConfigureAwait( false );
                    ++physicalRecordNumber;
                }
                catch (DelimitedSyntaxException exception)
                {
                    var currentContext = GetMetadata( null, null );
                    throw new RecordProcessingException( currentContext, Resources.InvalidRecordFormatNumber, exception );
                }
                if (!PassedOverRecord.Matches( parser.Options, parser.Values ))
                {
                    return record;
                }
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
            EnsureValuesParsed();
            var copy = new object[values.Length];
            Array.Copy( values, copy, values.Length );
            return copy;
        }


        IEntityAssembler? IReaderWithMetadata.Assembler
        {
            get => Assembler;
            set => Assembler = value;
        }

        object?[] IReaderWithMetadata.GetCurrentValues()
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
            EnsureValuesParsed();
            return values;
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
