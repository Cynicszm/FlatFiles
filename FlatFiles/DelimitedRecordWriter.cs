using System;
using System.IO;
using System.Threading.Tasks;
using FlatFiles.Properties;

namespace FlatFiles
{
    internal sealed class DelimitedRecordWriter : IFormattedColumnHandler
    {
        private readonly TextWriter writer;
        private readonly DelimitedSchema? schema;
        private readonly DelimitedSchemaInjector? injector;
        private readonly RecordBuffer buffer = new();
        private DelimitedRecordContext? recordContext;
        private ExecutionContextCache<DelimitedSchema, DelimitedExecutionContext>? executionContexts;

        public DelimitedRecordWriter( TextWriter writer, DelimitedSchema? schema, DelimitedOptions? options )
        {
            this.writer = writer;
            this.schema = schema;
            Options = options is null ? new DelimitedOptions() : options.Clone();
        }

        public DelimitedRecordWriter( TextWriter writer, DelimitedSchemaInjector injector, DelimitedOptions? options )
            : this( writer, (DelimitedSchema?) null, options )
        {
            this.injector = injector;
        }

        public DelimitedSchema? ActualSchema => schema;

        public DelimitedSchema? Schema => GetSchema( [] );

        public DelimitedOptions Options { get; }

        public DelimitedRecordContext? Metadata => recordContext;

        public event EventHandler<ColumnErrorEventArgs>? ColumnError;

        public int PhysicalRecordNumber { get; set; }

        public int LogicalRecordNumber { get; set; }

        public void WriteRecord( object?[] values )
        {
            FormatRecord( values );
            writer.Write( buffer.WrittenSpan );
        }

        public async Task WriteRecordAsync( object?[] values )
        {
            FormatRecord( values );
            await writer.WriteAsync( buffer.WrittenMemory ).ConfigureAwait( false );
        }

        /// <summary>
        ///     Formats the record into the buffer: each value is formatted by its column straight into the buffer,
        ///     quoted in place if it needs to be, and separated from the next. The record is then written in one go.
        /// </summary>
        private void FormatRecord( object?[] values )
        {
            var schema = GetSchema( values ) ?? DelimitedSchema.BuildDynamicSchema( Options, values.Length );
            var recordContext = NewRecordContext( schema );
            this.recordContext = recordContext;
            if (values.Length != schema.ColumnDefinitions.PhysicalCount)
            {
                throw new RecordProcessingException( recordContext, Resources.WrongNumberOfValues );
            }
            recordContext.ColumnError += ColumnError;
            buffer.Clear();
            schema.FormatValues( recordContext, values, buffer, this );
        }

        void IFormattedColumnHandler.ColumnStarting( int columnIndex, RecordBuffer destination )
        {
            if (columnIndex != 0)
            {
                destination.Write( Options.Separator.AsSpan() );
            }
        }

        void IFormattedColumnHandler.ColumnFormatted( int columnIndex, int start, RecordBuffer destination )
        {
            Escape( start );
        }

        private DelimitedRecordContext NewRecordContext( DelimitedSchema schema )
        {
            var executionContext = (executionContexts ??= new( s => new DelimitedExecutionContext( s!, Options.Clone() ) )).Get( schema );
            return new DelimitedRecordContext( executionContext )
            {
                PhysicalRecordNumber = PhysicalRecordNumber,
                LogicalRecordNumber = LogicalRecordNumber
            };
        }

        internal DelimitedSchema? GetSchema( object?[] values )
        {
            return injector is null ? schema : injector.GetSchema( values );
        }

        /// <summary>
        ///     Quotes the value that begins at <paramref name="start" /> and runs to the end of the buffer, if the
        ///     options call for it, doubling any quote characters it contains.
        /// </summary>
        private void Escape( int start )
        {
            var value = buffer.WrittenSpan[start..];
            if (!NeedsEscaping( value ))
            {
                return;
            }
            var quote = Options.Quote;
            var length = value.Length;
            var quoteCount = value.Count( quote );
            // Work backwards from the end so every character is moved before its old position is overwritten:
            // the write index starts two ahead of the read index and only gets further ahead at each doubled quote.
            var escaped = buffer.Extend( start, length + quoteCount + 2 );
            var write = escaped.Length - 1;
            escaped[write--] = quote;
            for (var read = length - 1; read >= 0; --read)
            {
                var character = escaped[read];
                escaped[write--] = character;
                if (character == quote)
                {
                    escaped[write--] = quote;
                }
            }
            escaped[0] = quote;
        }

        private bool NeedsEscaping( ReadOnlySpan<char> value )
        {
            if (Options.QuoteBehavior == QuoteBehavior.AlwaysQuote)
            {
                return true;
            }
            if (Options.QuoteBehavior == QuoteBehavior.Never)
            {
                return false;
            }
            // Don't escape empty strings.
            if (value.IsEmpty)
            {
                return false;
            }
            // Escape strings beginning or ending in whitespace.
            if (Char.IsWhiteSpace( value[0] ) || Char.IsWhiteSpace( value[^1] ))
            {
                return true;
            }
            // Escape strings containing the separator.
            if (value.Contains( Options.Separator.AsSpan(), StringComparison.Ordinal ))
            {
                return true;
            }
            // Escape strings containing the record separator.
            if (Options.RecordSeparator is not null && value.Contains( Options.RecordSeparator.AsSpan(), StringComparison.Ordinal ))
            {
                return true;
            }
            // Escape strings containing quotes.
            return value.Contains( Options.Quote );
        }

        public void WriteSchema()
        {
            if (schema is null)
            {
                return;
            }
            FormatSchema( schema );
            writer.Write( buffer.WrittenSpan );
        }

        public async Task WriteSchemaAsync()
        {
            if (schema is null)
            {
                return;
            }
            FormatSchema( schema );
            await writer.WriteAsync( buffer.WrittenMemory ).ConfigureAwait( false );
        }

        private void FormatSchema( DelimitedSchema schema )
        {
            buffer.Clear();
            var definitions = schema.ColumnDefinitions;
            for (int columnIndex = 0, columnCount = definitions.Count; columnIndex != columnCount; ++columnIndex)
            {
                if (columnIndex != 0)
                {
                    buffer.Write( Options.Separator.AsSpan() );
                }
                var start = buffer.Length;
                buffer.Write( definitions[columnIndex].ColumnName.AsSpan() );
                Escape( start );
            }
        }

        public void WriteRecordSeparator()
        {
            var separator = Options.RecordSeparator ?? Environment.NewLine;
            writer.Write( separator );
        }

        public async Task WriteRecordSeparatorAsync()
        {
            var separator = Options.RecordSeparator ?? Environment.NewLine;
            await writer.WriteAsync( separator ).ConfigureAwait( false );
        }

        public void WriteRaw( string data )
        {
            writer.Write( data );
        }

        public Task WriteRawAsync( string data )
        {
            return writer.WriteAsync( data );
        }
    }
}
