using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlatFiles.TypeMapping
{
    internal sealed class MultiplexingFixedLengthTypedReader( FixedLengthReader reader ) : IFixedLengthTypedReader<object>
    {
        private object? current;

        public IReader Reader => reader;

        FixedLengthReader IFixedLengthTypedReader<object>.Reader => reader;

        // FIXME: We should throw an exception if no or all records read
        public object Current => current!;

        public Func<IRecordContext, object?[], object?>? Deserializer { get; set; }

        public event EventHandler<FixedLengthRecordReadEventArgs>? RecordRead
        {
            add => reader.RecordRead += value;
            remove => reader.RecordRead -= value;
        }

        public event EventHandler<FixedLengthRecordPartitionedEventArgs>? RecordPartitioned
        {
            add => reader.RecordPartitioned += value;
            remove => reader.RecordPartitioned -= value;
        }

        public event EventHandler<FixedLengthRecordParsedEventArgs>? RecordParsed
        {
            add => reader.RecordParsed += value;
            remove => reader.RecordParsed -= value;
        }

        event EventHandler<IRecordParsedEventArgs>? ITypedReader<object>.RecordParsed
        {
            add => ((IReader) reader).RecordParsed += value;
            remove => ((IReader) reader).RecordParsed -= value;
        }

        public event EventHandler<RecordErrorEventArgs>? RecordError
        {
            add => reader.RecordError += value;
            remove => reader.RecordError -= value;
        }

        public event EventHandler<ColumnErrorEventArgs>? ColumnError
        {
            add => reader.ColumnError += value;
            remove => reader.ColumnError -= value;
        }

        public ISchema? GetSchema()
        {
            return null;
        }

        FixedLengthSchema? IFixedLengthTypedReader<object>.GetSchema()
        {
            return reader.GetSchema();
        }

        public bool Read()
        {
            if (!reader.Read())
            {
                return false;
            }
            SetCurrent();
            return true;
        }

        public ValueTask<bool> ReadAsync()
        {
            return ReadAsync( CancellationToken.None );
        }

        public async ValueTask<bool> ReadAsync( CancellationToken cancellationToken )
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await reader.ReadAsync( cancellationToken ).ConfigureAwait( false ))
            {
                return false;
            }
            SetCurrent();
            return true;
        }

        private void SetCurrent()
        {
            var values = reader.GetValues();
            IReaderWithMetadata metadataReader = reader;
            var recordContext = metadataReader.GetMetadata();
            // No matcher accepted the record and no default took it, so the underlying reader parsed it with a schema
            // built from its own values and there is nothing to make an entity with.
            var deserializer = Deserializer ?? throw new FlatFileException( Properties.Resources.MissingMatcher );
            current = deserializer( recordContext, values );
        }

        public bool Skip()
        {
            return reader.Skip();
        }

        public ValueTask<bool> SkipAsync()
        {
            return SkipAsync( CancellationToken.None );
        }

        public ValueTask<bool> SkipAsync( CancellationToken cancellationToken )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return reader.SkipAsync( cancellationToken );
        }
    }
}
