using System.Threading.Tasks;
using System.Threading;
using System;

namespace FlatFiles.TypeMapping
{
    internal sealed class UntypedWriter<TEntity>( ITypedWriter<TEntity> writer ) : ITypedWriter<object>
    {
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

        public IWriter Writer => writer.Writer;

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

        public void Write( object entity )
        {
            writer.Write( (TEntity) entity );
        }

        public Task WriteAsync( object entity )
        {
            return WriteAsync( entity, CancellationToken.None );
        }

        public async Task WriteAsync( object entity, CancellationToken cancellationToken )
{
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteAsync( (TEntity) entity, cancellationToken ).ConfigureAwait( false );
        }
    }
}
