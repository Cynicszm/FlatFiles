using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FlatFiles.Properties;

namespace FlatFiles
{
    internal sealed class FixedLengthRecordWriter( TextWriter writer, FixedLengthSchema? schema, FixedLengthOptions? options ) : IFormattedColumnHandler
    {
        private readonly FixedLengthSchemaInjector? injector;
        private readonly RecordBuffer buffer = new();
        private ExecutionContextCache<FixedLengthSchema, FixedLengthExecutionContext>? executionContexts;

        public FixedLengthRecordWriter( TextWriter writer, FixedLengthSchemaInjector injector, FixedLengthOptions? options )
            : this( writer, (FixedLengthSchema?) null, options )
        {
            this.injector = injector;
        }

        public FixedLengthSchema? ActualSchema { get; } = schema;

        public FixedLengthOptions Options { get; } = options is null ? new FixedLengthOptions() : options.Clone();

        public FixedLengthRecordContext? Metadata { get; private set; }

        public int PhysicalRecordNumber { get; set; }

        public int LogicalRecordNumber { get; set; }

        public event EventHandler<ColumnErrorEventArgs>? ColumnError;

        public void WriteRecord( object?[] values )
        {
            Metadata = null;
            FormatRecord( values );
            writer.Write( buffer.WrittenSpan );
        }

        public async Task WriteRecordAsync( object?[] values, CancellationToken cancellationToken = default )
        {
            Metadata = null;
            FormatRecord( values );
            await writer.WriteAsync( buffer.WrittenMemory, cancellationToken ).ConfigureAwait( false );
        }

        /// <summary>
        ///     Formats the record into the buffer: each value is formatted by its column straight into the buffer and
        ///     then padded or truncated in place to its window. The record is then written in one go.
        /// </summary>
        private void FormatRecord( object?[] values )
        {
            var currentSchema = GetSchema( values );
            var metadata = NewRecordContext( currentSchema, null, null );
            Metadata = metadata;
            if (values.Length != currentSchema.ColumnDefinitions.PhysicalCount)
            {
                throw new RecordProcessingException( metadata, Resources.WrongNumberOfValues );
            }
            metadata.ColumnError += ColumnError;
            buffer.Clear();
            currentSchema.FormatValues( metadata, values, buffer, this );
        }

        void IFormattedColumnHandler.ColumnStarting( int columnIndex, RecordBuffer destination )
        {
        }

        void IFormattedColumnHandler.ColumnFormatted( int columnIndex, int start, RecordBuffer destination )
        {
            var windows = Metadata?.ExecutionContext.Schema.Windows;
            if (windows is not null && columnIndex < windows.Count)
            {
                FitWindow( windows[columnIndex], start );
            }
        }

        public FixedLengthSchema GetSchema( object?[] values )
        {
            return injector is null ? ActualSchema! : injector.GetSchema( values );
        }

        private FixedLengthRecordContext NewRecordContext( FixedLengthSchema currentSchema, string? record, string[]? values )
        {
            var executionContext = (executionContexts ??= new ExecutionContextCache<FixedLengthSchema, FixedLengthExecutionContext>( s => new FixedLengthExecutionContext( s!, Options.Clone() ) )).Get( currentSchema );
            var currentContext = new FixedLengthRecordContext( executionContext )
            {
                PhysicalRecordNumber = PhysicalRecordNumber,
                LogicalRecordNumber = LogicalRecordNumber,
                Record = record,
                Values = values
            };
            return currentContext;
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

        private void FormatSchema( FixedLengthSchema currentSchema )
        {
            buffer.Clear();
            var definitions = currentSchema.ColumnDefinitions;
            var windows = currentSchema.Windows;
            for (int columnIndex = 0, columnCount = definitions.Count; columnIndex != columnCount; ++columnIndex)
            {
                var start = buffer.Length;
                buffer.Write( definitions[columnIndex].ColumnName.AsSpan() );
                FitWindow( windows[columnIndex], start );
            }
        }

        /// <summary>
        ///     Pads or truncates the value that begins at <paramref name="start" /> and runs to the end of the buffer so
        ///     that it exactly fills the window.
        /// </summary>
        private void FitWindow( Window window, int start )
        {
            var length = buffer.Length - start;
            if (length > window.Width)
            {
                TruncateValue( window, start, length );
            }
            else if (length < window.Width)
            {
                PadValue( window, start, length );
            }
        }

        private void TruncateValue( Window window, int start, int length )
        {
            var policy = window.TruncationPolicy ?? Options.TruncationPolicy;
            switch (policy)
            {
                case OverflowTruncationPolicy.TruncateLeading:
                {
                    var value = buffer.WrittenSpan.Slice( start, length );
                    value[^window.Width..].CopyTo( value );
                    buffer.Truncate( start + window.Width );
                    break;
                }
                case OverflowTruncationPolicy.TruncateTrailing:
                    buffer.Truncate( start + window.Width );
                    break;
                case OverflowTruncationPolicy.ThrowException:
                    throw new FlatFileException( Resources.ValueExceedsWindowWidth );
                default:
                    throw new FlatFileException( Resources.InvalidTruncationPolicy );
            }
        }

        private void PadValue( Window window, int start, int length )
        {
            var alignment = window.Alignment ?? Options.Alignment;
            var fillCharacter = window.FillCharacter ?? Options.FillCharacter;
            var padding = window.Width - length;
            var value = buffer.Extend( start, window.Width );
            if (alignment == FixedAlignment.LeftAligned)
            {
                value[length..].Fill( fillCharacter );
            }
            else
            {
                value[..length].CopyTo( value[padding..] );
                value[..padding].Fill( fillCharacter );
            }
        }

        public void WriteRecordSeparator()
        {
            if (!Options.HasRecordSeparator)
            {
                return;
            }
            var separator = Options.RecordSeparator ?? Environment.NewLine;
            writer.Write( separator );
        }

        public async Task WriteRecordSeparatorAsync( CancellationToken cancellationToken = default )
        {
            if (!Options.HasRecordSeparator)
            {
                return;
            }
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
