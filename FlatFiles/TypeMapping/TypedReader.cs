using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlatFiles.TypeMapping
{
    internal abstract class TypedReader<TEntity> : ITypedReader<TEntity>
    {
        private readonly Func<IRecordContext, object?[], TEntity> deserializer;
        private TEntity? current;

        protected TypedReader( IMapper<TEntity> mapper )
        {
            deserializer = mapper.GetReader();
        }

        event EventHandler<IRecordParsedEventArgs>? ITypedReader<TEntity>.RecordParsed
        {
            add => Reader.RecordParsed += value;
            remove => Reader.RecordParsed -= value;
        }

        public event EventHandler<RecordErrorEventArgs>? RecordError
        {
            add => Reader.RecordError += value;
            remove => Reader.RecordError -= value;
        }

        public event EventHandler<ColumnErrorEventArgs>? ColumnError
        {
            add => Reader.ColumnError += value;
            remove => Reader.ColumnError -= value;
        }

        public abstract IReader Reader { get; }

        public ISchema? GetSchema()
        {
            return Reader.GetSchema();
        }

        public bool Read()
        {
            if (!Reader.Read())
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
            if (!await Reader.ReadAsync( cancellationToken ).ConfigureAwait( false ))
            {
                return false;
            }
            SetCurrent();
            return true;
        }

        private void SetCurrent()
        {
            var values = Reader.GetValues();
            var metadataReader = (IReaderWithMetadata) Reader;
            var recordContext = metadataReader.GetMetadata();
            current = deserializer( recordContext, values ); // Won't be null is Read returns true
        }

        public bool Skip()
        {
            return Reader.Skip();
        }

        public ValueTask<bool> SkipAsync()
        {
            return SkipAsync( CancellationToken.None );
        }

        public async ValueTask<bool> SkipAsync( CancellationToken cancellationToken )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Reader.SkipAsync( cancellationToken ).ConfigureAwait( false );
        }

        // FIXME: We should throw an exception if no or all records read
        public TEntity Current => current!;
    }
}
