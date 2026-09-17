using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FlatFiles.Properties;

namespace FlatFiles
{
    internal sealed class DelimitedRecordWriter( TextWriter writer, DelimitedSchema? schema, DelimitedOptions? options ) : IFormattedColumnHandler
    {
        private readonly DelimitedSchemaInjector? injector;
        private readonly RecordBuffer buffer = new();
        private ExecutionContextCache<DelimitedSchema, DelimitedExecutionContext>? executionContexts;

        public DelimitedRecordWriter( TextWriter writer, DelimitedSchemaInjector injector, DelimitedOptions? options )
            : this( writer, (DelimitedSchema?) null, options )
        {
            this.injector = injector;
        }

        public DelimitedSchema? ActualSchema { get; } = schema;

        public DelimitedSchema? Schema => GetSchema( [] );

        public DelimitedOptions Options { get; } = options is null ? new DelimitedOptions() : options.Clone();

        public DelimitedRecordContext? Metadata { get; private set; }

        public event EventHandler<ColumnErrorEventArgs>? ColumnError;

        public int PhysicalRecordNumber { get; set; }

        public int LogicalRecordNumber { get; set; }

        public void WriteRecord( object?[] values )
        {
            FormatRecord( values );
            writer.Write( buffer.WrittenSpan );
        }

        public async Task WriteRecordAsync( object?[] values, CancellationToken cancellationToken = default )
        {
            FormatRecord( values );
            await writer.WriteAsync( buffer.WrittenMemory, cancellationToken ).ConfigureAwait( false );
        }

        /// <summary>
        ///     Formats the record into the buffer: each value is formatted by its column straight into the buffer,
        ///     quoted in place if it needs to be, and separated from the next. The record is then written in one go.
        /// </summary>
        private void FormatRecord( object?[] values )
        {
            var currentSchema = GetSchema( values ) ?? DelimitedSchema.BuildDynamicSchema( Options, values.Length );
            var currentContext = NewRecordContext( currentSchema );
            Metadata = currentContext;
            if (values.Length != currentSchema.ColumnDefinitions.PhysicalCount)
            {
                throw new RecordProcessingException( currentContext, Resources.WrongNumberOfValues );
            }
            currentContext.ColumnError += ColumnError;
            buffer.Clear();
            currentSchema.FormatValues( currentContext, values, buffer, this );
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

        private DelimitedRecordContext NewRecordContext( DelimitedSchema currentSchema )
        {
            var executionContext = (executionContexts ??= new ExecutionContextCache<DelimitedSchema, DelimitedExecutionContext>( s => new DelimitedExecutionContext( s!, Options.Clone() ) )).Get( currentSchema );
            return new DelimitedRecordContext( executionContext )
            {
                PhysicalRecordNumber = PhysicalRecordNumber,
                LogicalRecordNumber = LogicalRecordNumber
            };
        }

        internal DelimitedSchema? GetSchema( object?[] values )
        {
            return injector is null ? ActualSchema : injector.GetSchema( values );
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
            switch (Options.QuoteBehavior)
            {
                case QuoteBehavior.AlwaysQuote:
                    return true;
                case QuoteBehavior.Never:
                    return false;
            }
            // Don't escape empty strings.
            if (value.IsEmpty)
            {
                return false;
            }
            // Escape strings beginning or ending in whitespace.
            if (char.IsWhiteSpace( value[0] ) || char.IsWhiteSpace( value[^1] ))
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
            if (ActualSchema is null)
            {
                return;
            }
            FormatSchema( ActualSchema );
            writer.Write( buffer.WrittenSpan );
        }

        public async Task WriteSchemaAsync( CancellationToken cancellationToken = default )
        {
            if (ActualSchema is null)
            {
                return;
            }
            FormatSchema( ActualSchema );
            await writer.WriteAsync( buffer.WrittenMemory, cancellationToken ).ConfigureAwait( false );
        }

        private void FormatSchema( DelimitedSchema currentSchema )
        {
            buffer.Clear();
            var definitions = currentSchema.ColumnDefinitions;
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

        public async Task WriteRecordSeparatorAsync( CancellationToken cancellationToken = default )
        {
            var separator = Options.RecordSeparator ?? Environment.NewLine;
            await writer.WriteAsync( separator.AsMemory(), cancellationToken ).ConfigureAwait( false );
        }

        public void WriteRaw( string data )
        {
            writer.Write( data );
        }

        public Task WriteRawAsync( string data, CancellationToken cancellationToken = default )
        {
            return writer.WriteAsync( data.AsMemory(), cancellationToken );
        }
    }
}
