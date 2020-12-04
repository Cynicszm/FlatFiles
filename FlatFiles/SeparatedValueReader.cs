﻿using System;
using System.IO;
using System.Threading.Tasks;
using FlatFiles.Properties;

namespace FlatFiles
{
    /// <inheritdoc />
    /// <summary>
    /// Extracts records from a file that has values separated by a separator token.
    /// </summary>
    public sealed class SeparatedValueReader : IReader, IReaderWithMetadata
    {
        private readonly SeparatedValueRecordParser parser;
        private readonly SeparatedValueSchemaSelector schemaSelector;
        private readonly SeparatedValueRecordContext metadata;
        private object[] values;
        private bool endOfFile;
        private bool hasError;

        /// <summary>
        /// Initializes a new SeparatedValueReader with no schema.
        /// </summary>
        /// <param name="reader">A reader over the separated value document.</param>
        /// <param name="options">The options controlling how the separated value document is read.</param>
        /// <exception cref="ArgumentNullException">The reader is null.</exception>
        public SeparatedValueReader(TextReader reader, SeparatedValueOptions options = null)
            : this(reader, null, options, false)
        {
        }

        /// <summary>
        /// Initializes a new SeparatedValueReader with the given schema.
        /// </summary>
        /// <param name="reader">A reader over the separated value document.</param>
        /// <param name="schema">The schema of the separated value document.</param>
        /// <param name="options">The options controlling how the separated value document is read.</param>
        /// <exception cref="ArgumentNullException">The reader is null.</exception>
        /// <exception cref="ArgumentNullException">The schema is null.</exception>
        public SeparatedValueReader(TextReader reader, SeparatedValueSchema schema, SeparatedValueOptions options = null)
            : this(reader, schema, options, true)
        {
        }

        /// <summary>
        /// Initializes a new SeparatedValueReader with the given schema.
        /// </summary>
        /// <param name="reader">A reader over the separated value document.</param>
        /// <param name="schemaSelector">The schema selector configured to determine the schema dynamically.</param>
        /// <param name="options">The options controlling how the separated value document is read.</param>
        /// <exception cref="ArgumentNullException">The reader is null.</exception>
        /// <exception cref="ArgumentNullException">The schema selector is null.</exception>
        public SeparatedValueReader(TextReader reader, SeparatedValueSchemaSelector schemaSelector, SeparatedValueOptions options = null)
            : this(reader, null, options, false)
        {
            this.schemaSelector = schemaSelector ?? throw new ArgumentNullException(nameof(schemaSelector));
        }

        private SeparatedValueReader(TextReader reader, SeparatedValueSchema schema, SeparatedValueOptions options, bool hasSchema)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            if (hasSchema && schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }
            if (options == null)
            {
                options = new SeparatedValueOptions();
            }
            if (options.RecordSeparator == options.Separator)
            {
                throw new ArgumentException(Resources.SameSeparator, nameof(options));
            }
            RetryReader retryReader = new RetryReader(reader);
            parser = new SeparatedValueRecordParser(retryReader, options);
            metadata = new SeparatedValueRecordContext()
            {
                ExecutionContext = new SeparatedValueExecutionContext()
                {
                    Schema = hasSchema ? schema : null,
                    Options = parser.Options
                }
            };
        }

        /// <summary>
        /// Raised when a record is read but before its columns are parsed.
        /// </summary>
        public event EventHandler<SeparatedValueRecordReadEventArgs> RecordRead;

        /// <summary>
        /// Raised when a record is parsed.
        /// </summary>
        public event EventHandler<SeparatedValueRecordParsedEventArgs> RecordParsed;

        event EventHandler<IRecordParsedEventArgs> IReader.RecordParsed
        {
            add => RecordParsed += (sender, e) => value(sender, e);
            remove => RecordParsed -= (sender, e) => value(sender, e);
        }

        /// <summary>
        /// Raised when an error occurs while processing a record.
        /// </summary>
        public event EventHandler<RecordErrorEventArgs> RecordError;

        /// <summary>
        /// Raised when an error occurs while processing a column.
        /// </summary>
        public event EventHandler<ColumnErrorEventArgs> ColumnError
        {
            add { metadata.ColumnError += value; }
            remove { metadata.ColumnError -= value; }
        }

        /// <summary>
        /// Gets the names of the columns found in the file.
        /// </summary>
        /// <returns>The names.</returns>
        public SeparatedValueSchema GetSchema()
        {
            if (schemaSelector != null)
            {
                return null;
            }
            HandleSchema();
            if (metadata.ExecutionContext.Schema == null)
            {
                throw new InvalidOperationException(Resources.SchemaNotDefined);
            }
            return metadata.ExecutionContext.Schema;
        }

        ISchema IReader.GetSchema()
        {
            return GetSchema();
        }

        internal void SetSchema(SeparatedValueSchema schema)
        {
            metadata.ExecutionContext.Schema = schema;
        }

        /// <summary>
        /// Gets the schema being used by the parser.
        /// </summary>
        /// <returns>The schema being used by the parser.</returns>
        public async Task<SeparatedValueSchema> GetSchemaAsync()
        {
            if (schemaSelector != null)
            {
                return null;
            }
            await HandleSchemaAsync().ConfigureAwait(false);
            if (metadata.ExecutionContext.Schema == null)
            {
                throw new InvalidOperationException(Resources.SchemaNotDefined);
            }
            return metadata.ExecutionContext.Schema;
        }

        async Task<ISchema> IReader.GetSchemaAsync()
        {
            var schema = await GetSchemaAsync().ConfigureAwait(false);
            return schema;
        }

        /// <summary>
        /// Attempts to read the next record from the stream.
        /// </summary>
        /// <returns>True if the next record was read or false if all records have been read.</returns>
        public bool Read()
        {
            if (hasError)
            {
                throw new InvalidOperationException(Resources.ReadingWithErrors);
            }
            metadata.Record = null;
            metadata.Values = null;
            HandleSchema();
            try
            {
                values = ParsePartitions();
                if (values == null)
                {
                    return false;
                }

                ++metadata.LogicalRecordNumber;
                return true;
            }
            catch (FlatFileException)
            {
                hasError = true;
                throw;
            }
        }

        private void HandleSchema()
        {
            if (metadata.PhysicalRecordNumber != 0)
            {
                return;
            }
            if (!parser.Options.IsFirstRecordSchema)
            {
                return;
            }
            if (schemaSelector != null || metadata.ExecutionContext.Schema != null)
            {
                SkipInternal();
                return;
            }
            string[] columnNames = ReadNextRecord();
            metadata.ExecutionContext.Schema = new SeparatedValueSchema();
            foreach (string columnName in columnNames)
            {
                StringColumn column = new StringColumn(columnName);
                metadata.ExecutionContext.Schema.AddColumn(column);
            }
        }

        private object[] ParsePartitions()
        {
            var rawValues = ReadWithFilter();
            while (rawValues != null)
            {
                if (metadata.ExecutionContext.Schema != null && HasWrongNumberOfColumns(rawValues))
                {
                    ProcessError(new RecordProcessingException(metadata, Resources.SeparatedValueRecordWrongNumberOfColumns));
                }
                else
                {
                    object[] values = ParseValues(rawValues);
                    if (values != null)
                    {
                        RecordParsed?.Invoke(this, new SeparatedValueRecordParsedEventArgs(metadata, values));
                        return values;
                    }
                }
                rawValues = ReadWithFilter();
            }
            return null;
        }

        private string[] ReadWithFilter()
        {
            string[] rawValues = ReadNextRecord();
            metadata.ExecutionContext.Schema = GetSchema(rawValues);
            while (rawValues != null && IsSkipped(rawValues))
            {
                rawValues = ReadNextRecord();
                metadata.ExecutionContext.Schema = GetSchema(rawValues);
            }
            return rawValues;
        }

        /// <inheritdoc />
        /// <summary>
        /// Attempts to read the next record from the stream.
        /// </summary>
        /// <returns>True if the next record was read or false if all records have been read.</returns>
        public async ValueTask<bool> ReadAsync()
        {
            if (hasError)
            {
                throw new InvalidOperationException(Resources.ReadingWithErrors);
            }
            metadata.Record = null;
            metadata.Values = null;
            await HandleSchemaAsync().ConfigureAwait(false);
            try
            {
                values = await ParsePartitionsAsync().ConfigureAwait(false);
                if (values == null)
                {
                    return false;
                }

                ++metadata.LogicalRecordNumber;
                return true;
            }
            catch (FlatFileException)
            {
                hasError = true;
                throw;
            }
        }

        private async Task HandleSchemaAsync()
        {
            if (metadata.PhysicalRecordNumber != 0)
            {
                return;
            }
            if (!parser.Options.IsFirstRecordSchema)
            {
                return;
            }
            if (metadata.ExecutionContext.Schema != null)
            {
                await SkipAsyncInternal().ConfigureAwait(false);
                return;
            }
            string[] columnNames = await ReadNextRecordAsync().ConfigureAwait(false);
            metadata.ExecutionContext.Schema = new SeparatedValueSchema();
            foreach (string columnName in columnNames)
            {
                StringColumn column = new StringColumn(columnName);
                metadata.ExecutionContext.Schema.AddColumn(column);
            }
        }

        private async Task<object[]> ParsePartitionsAsync()
        {
            var rawValues = await ReadWithFilterAsync().ConfigureAwait(false);
            while (rawValues != null)
            {
                if (metadata.ExecutionContext.Schema != null && HasWrongNumberOfColumns(rawValues))
                {
                    ProcessError(new RecordProcessingException(metadata, Resources.SeparatedValueRecordWrongNumberOfColumns));
                }
                else
                {
                    object[] values = ParseValues(rawValues);
                    if (values != null)
                    {
                        return values;
                    }
                }
                rawValues = await ReadWithFilterAsync().ConfigureAwait(false);
            }
            return null;
        }

        private bool HasWrongNumberOfColumns(string[] values)
        {
            var schema = metadata.ExecutionContext.Schema;
            return values.Length + schema.ColumnDefinitions.MetadataCount < schema.ColumnDefinitions.PhysicalCount;
        }

        private async Task<string[]> ReadWithFilterAsync()
        {
            string[] rawValues = await ReadNextRecordAsync().ConfigureAwait(false);
            metadata.ExecutionContext.Schema = GetSchema(rawValues);
            while (rawValues != null && IsSkipped(rawValues))
            {
                rawValues = await ReadNextRecordAsync().ConfigureAwait(false);
                metadata.ExecutionContext.Schema = GetSchema(rawValues);
            }
            return rawValues;
        }

        private SeparatedValueSchema GetSchema(string[] rawValues)
        {
            if (rawValues == null)
            {
                return null;
            }
            if (schemaSelector == null)
            {
                return metadata.ExecutionContext.Schema;
            }
            SeparatedValueSchema schema = schemaSelector.GetSchema(rawValues);
            if (schema != null)
            {
                return schema;
            }
            ProcessError(new RecordProcessingException(metadata, Resources.MissingMatcher));
            return null;
        }

        private bool IsSkipped(string[] values)
        {
            if (metadata.ExecutionContext.Schema == null && schemaSelector != null)
            {
                // A schema was not found by the selector for the given record.
                // If we got here then we know the raised exception was handled and suppressed.
                // Therefore we skip the record and go on to the next one.
                return true;
            }
            if (RecordRead == null)
            {
                return false;
            }
            var e = new SeparatedValueRecordReadEventArgs(metadata, values);
            RecordRead(this, e);
            return e.IsSkipped;
        }

        private object[] ParseValues(string[] rawValues)
        {
            if (metadata.ExecutionContext.Schema == null)
            {
                return ParseWithoutSchema(rawValues);
            }
            try
            {
                return metadata.ExecutionContext.Schema.ParseValues(metadata, rawValues);
            }
            catch (FlatFileException exception)
            {
                ProcessError(new RecordProcessingException(metadata, Resources.InvalidRecordConversion, exception));
                return null;
            }
        }

        private object[] ParseWithoutSchema(string[] rawValues)
        {
            var results = new object[rawValues.Length];
            bool preserveWhitespace = metadata.ExecutionContext.Options.PreserveWhiteSpace;
            for (int columnIndex = 0; columnIndex != rawValues.Length; ++columnIndex)
            {
                var rawValue = rawValues[columnIndex];
                var trimmed = preserveWhitespace ? rawValue : ColumnDefinition.TrimValue(rawValue);
                var parsedValue = String.IsNullOrEmpty(trimmed) ? null : trimmed;
                results[columnIndex] = parsedValue;
            }
            return results;
        }

        /// <summary>
        /// Attempts to skip the next record from the stream.
        /// </summary>
        /// <returns>True if the next record was skipped or false if all records have been read.</returns>
        /// <remarks>The previously parsed values remain available.</remarks>
        public bool Skip()
        {
            if (hasError)
            {
                throw new InvalidOperationException(Resources.ReadingWithErrors);
            }
            HandleSchema();
            bool result = SkipInternal();
            return result;
        }

        private bool SkipInternal()
        {
            string[] rawValues = ReadNextRecord();
            return rawValues != null;
        }

        /// <inheritdoc />
        /// <summary>
        /// Attempts to skip the next record from the stream.
        /// </summary>
        /// <returns>True if the next record was skipped or false if all records have been read.</returns>
        /// <remarks>The previously parsed values remain available.</remarks>
        public async ValueTask<bool> SkipAsync()
        {
            if (hasError)
            {
                throw new InvalidOperationException(Resources.ReadingWithErrors);
            }
            await HandleSchemaAsync().ConfigureAwait(false);
            bool result = await SkipAsyncInternal().ConfigureAwait(false);
            return result;
        }

        private async ValueTask<bool> SkipAsyncInternal()
        {
            string[] rawValues = await ReadNextRecordAsync().ConfigureAwait(false);
            return rawValues != null;
        }

        private void ProcessError(RecordProcessingException exception)
        {
            if (RecordError != null)
            {
                var args = new RecordErrorEventArgs(exception);
                RecordError(this, args);
                if (args.IsHandled)
                {
                    return;
                }
            }
            throw exception;
        }

        private string[] ReadNextRecord()
        {
            if (parser.IsEndOfStream())
            {
                endOfFile = true;
                values = null;
                return null;
            }
            try
            {
                (string record, string[] results) = parser.ReadRecord();
                metadata.Record = record;
                metadata.Values = results;
                ++metadata.PhysicalRecordNumber;
                return results;
            }
            catch (SeparatedValueSyntaxException exception)
            {
                throw new RecordProcessingException(metadata, Resources.InvalidRecordFormatNumber, exception);
            }
        }

        private async Task<string[]> ReadNextRecordAsync()
        {
            if (await parser.IsEndOfStreamAsync().ConfigureAwait(false))
            {
                endOfFile = true;
                values = null;
                return null;
            }
            try
            {
                (string record, string[] results) = await parser.ReadRecordAsync().ConfigureAwait(false);
                metadata.Record = record;
                metadata.Values = results;
                ++metadata.PhysicalRecordNumber;
                return results;
            }
            catch (SeparatedValueSyntaxException exception)
            {
                throw new RecordProcessingException(metadata, Resources.InvalidRecordFormatNumber, exception);
            }
        }

        /// <summary>
        /// Gets the values for the current record.
        /// </summary>
        /// <returns>The values of the current record.</returns>
        public object[] GetValues()
        {
            if (hasError)
            {
                throw new InvalidOperationException(Resources.ReadingWithErrors);
            }
            if (metadata.PhysicalRecordNumber == 0)
            {
                throw new InvalidOperationException(Resources.ReadNotCalled);
            }
            if (endOfFile)
            {
                throw new InvalidOperationException(Resources.NoMoreRecords);
            }
            object[] copy = new object[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }

        IRecordContext IReaderWithMetadata.GetMetadata()
        {
            return metadata;
        }
    }
}
