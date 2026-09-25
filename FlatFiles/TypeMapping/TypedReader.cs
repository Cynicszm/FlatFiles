using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlatFiles.TypeMapping
{
    internal abstract class TypedReader<TEntity>( IMapper<TEntity> mapper ) : ITypedReader<TEntity>, IEntityAssembler
    {
        private readonly Func<IRecordContext, object?[], TEntity> deserialiser = mapper.GetReader();
        // Non-null where every column and member this mapper covers can be read without boxing a value.
        private readonly IColumnSetter<TEntity>[]? setters = mapper.GetColumnSetters();
        private TEntity? current;
        private TEntity? assembled;
        private bool hasAssembled;
        private bool isAssemblerInstalled;

        void IEntityAssembler.Assemble( IRecoverableRecordContext context, Schema schema, RawRecord values )
        {
            var entity = mapper.CreateEntity();
            schema.ParseValues( context, values, entity, setters! );
            assembled = entity;
            hasAssembled = true;
        }

        /// <summary>
        ///     Tells the reader, once, that this one will take each record onto an entity itself. The reader still
        ///     decides per record whether it can: a handler that is given the parsed values takes them back.
        /// </summary>
        private void InstallAssembler()
        {
            if (isAssemblerInstalled)
            {
                return;
            }
            isAssemblerInstalled = true;
            if (setters is not null && Reader is IReaderWithMetadata metadataReader)
            {
                metadataReader.Assembler = this;
            }
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
            InstallAssembler();
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
            InstallAssembler();
            if (!await Reader.ReadAsync( cancellationToken ).ConfigureAwait( false ))
            {
                return false;
            }
            SetCurrent();
            return true;
        }

        private void SetCurrent()
        {
            if (hasAssembled)
            {
                // The record went straight onto an entity; there is nothing to deserialise.
                current = assembled;
                assembled = default;
                hasAssembled = false;
                return;
            }
            // The reader's own array, not a copy: the deserialiser reads each value once and never keeps it.
            var metadataReader = (IReaderWithMetadata) Reader;
            var values = metadataReader.GetCurrentValues();
            var recordContext = metadataReader.GetMetadata();
            current = deserialiser( recordContext, values ); // Won't be null is Read returns true
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
