using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlatFiles.TypeMapping
{
    internal sealed class MultiplexingTypedWriter( IWriterWithMetadata writer, ITypeMapperInjector injector ) : ITypedWriter<object>
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

        public IWriter Writer => writer;

        public ISchema? GetSchema()
        {
            return null;
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
            var values = Serialize( entity );
            writer.Write( values );
        }

        public Task WriteAsync( object entity )
        {
            return WriteAsync( entity, CancellationToken.None );
        }

        public async Task WriteAsync( object entity, CancellationToken cancellationToken )
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = Serialize( entity );
            await writer.WriteAsync( values, cancellationToken ).ConfigureAwait( false );
        }

        /// <summary>
        ///     Fills one array for the whole write rather than one per record, resized where a record belongs to
        ///     a schema of a different width - which is the one thing this writer has to allow for that the
        ///     single-schema one does not.
        /// </summary>
        private object?[] Serialize( object entity )
        {
            var context = injector.SetMatcher( entity );
            if (values.Length != context.LogicalCount)
            {
                values = new object?[context.LogicalCount];
            }
            else
            {
                // Cleared rather than assumed: a mapping need not write every slot, and a value left behind by
                // the record before would be written as though it belonged to this one.
                Array.Clear( values, 0, values.Length );
            }
            var recordContext = writer.GetMetadata();
            context.Serialize( recordContext, entity, values );
            return values;
        }

        private object?[] values = [];
    }
}
