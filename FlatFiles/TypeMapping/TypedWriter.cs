using System.Threading.Tasks;
using System.Threading;
using System;

namespace FlatFiles.TypeMapping
{
    internal sealed class TypedWriter<TEntity> : ITypedWriter<TEntity>
    {
        private readonly IWriterWithMetadata writer;
        private readonly Action<IRecordContext, TEntity, object[]> serializer;
        private readonly int logicalCount;

        public TypedWriter( IWriterWithMetadata writer, IMapper<TEntity> mapper )
        {
            this.writer = writer;
            serializer = mapper.GetWriter();
            logicalCount = mapper.LogicalCount;
        }

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
            var values = Serialize( entity );
            await writer.WriteAsync( values, cancellationToken ).ConfigureAwait( false );
        }

        private object[] Serialize( TEntity entity )
        {
            var values = new object[logicalCount];
            var recordContext = writer.GetMetadata();
            serializer( recordContext, entity, values );
            return values;
        }
    }
}
