using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlatFiles.TypeMapping
{
    internal sealed class TypedWriter<TEntity>( IWriterWithMetadata writer, IMapper<TEntity> mapper ) : ITypedWriter<TEntity>
    {
        private readonly Action<IRecordContext, TEntity, object[]> serializer = mapper.GetWriter();
        private readonly int logicalCount = mapper.LogicalCount;

        // Non-null where every column and member this mapping covers can be written without boxing a value.
        private readonly IColumnGetter<TEntity>[]? getters = mapper.GetColumnGetters();

        /// <summary>
        ///     Raised when an error occurs while processing a column.
        /// </summary>
        public event EventHandler<ColumnErrorEventArgs>? ColumnError
        {
            add => writer.ColumnError += value;
            remove => writer.ColumnError -= value;
        }

        /// <summary>
        ///     Raised when an error occurs while processing a record.
        /// </summary>
        public event EventHandler<RecordErrorEventArgs>? RecordError
        {
            add => writer.RecordError += value;
            remove => writer.RecordError -= value;
        }

        public IWriter Writer => writer;


        public ISchema? GetSchema()
        {
            return writer.GetSchema();
        }

        public void WriteSchema()
        {
            writer.WriteSchema();
        }

        public Task WriteSchemaAsync()
        {
            return WriteSchemaAsync( CancellationToken.None );
        }

        public async Task WriteSchemaAsync( CancellationToken cancellationToken )
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteSchemaAsync( cancellationToken ).ConfigureAwait( false );
        }

        public void Write( TEntity entity )
        {
            if (getters is not null && writer.CanWriteFromEntity)
            {
                // Straight from the entity: nothing is boxed on the way, and no array is filled to hold it.
                writer.WriteFromEntity( entity, getters );
                return;
            }
            var values = Serialize( entity );
            writer.Write( values );
        }

        public Task WriteAsync( TEntity entity )
        {
            return WriteAsync( entity, CancellationToken.None );
        }

        public async Task WriteAsync( TEntity entity, CancellationToken cancellationToken )
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (getters is not null && writer.CanWriteFromEntity)
            {
                await writer.WriteFromEntityAsync( entity, getters, cancellationToken ).ConfigureAwait( false );
                return;
            }
            var values = Serialize( entity );
            await writer.WriteAsync( values, cancellationToken ).ConfigureAwait( false );
        }

        /// <summary>
        ///     Fills one array for the whole write rather than one per record. Nothing downstream keeps it: the
        ///     record is formatted before the call returns, and no handler on the writing side is handed the
        ///     values - which is what a <c>RecordParsed</c> handler does on the reading side, and why that one
        ///     needs an array of its own.
        /// </summary>
        private object[] Serialize( TEntity entity )
        {
            // Cleared rather than assumed: a mapping need not write every slot, and a value left behind by the
            // record before would be written as though it belonged to this one.
            Array.Clear( values, 0, values.Length );
            var recordContext = writer.GetMetadata();
            serializer( recordContext, entity, values );
            return values;
        }

        private readonly object[] values = new object[mapper.LogicalCount];
    }
}
