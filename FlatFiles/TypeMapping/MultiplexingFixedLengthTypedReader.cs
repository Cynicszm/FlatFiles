using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlatFiles.TypeMapping
{
    internal sealed class MultiplexingFixedLengthTypedReader( FixedLengthReader reader ) : IFixedLengthTypedReader<object>, IEntityAssembler
    {
        private object? current;
        private bool hasAssembled;

        public IReader Reader => reader;

        FixedLengthReader IFixedLengthTypedReader<object>.Reader => reader;

        // FIXME: We should throw an exception if no or all records read
        public object Current => current!;

        public Func<IRecordContext, object?[], object?>? Deserialiser { get; set; }

        /// <summary>
        ///     The assembler for the layout the current record matched, set as the schema is selected. Null until
        ///     a record matches, and null throughout where the mappings cannot all be read this way.
        /// </summary>
        public IObjectAssembler? Assembler { get; set; }

        /// <summary>
        ///     Takes each record onto an entity directly, in place of the reader parsing it into an array of
        ///     objects for a deserialiser to unbox. Installed only where every mapping behind this reader can be
        ///     read that way, since the reader takes this path for every record once it has one.
        /// </summary>
        public void Install()
        {
            reader.Assembler = this;
        }

        void IEntityAssembler.Assemble( IRecoverableRecordContext context, Schema schema, RawRecord values )
        {
            // No matcher accepted the record; the values path raises this, and so must this one.
            var assembler = Assembler ?? throw new FlatFileException( Properties.Resources.MissingMatcher );
            assembler.Assemble( context, schema, values );
            hasAssembled = true;
        }

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
            if (hasAssembled)
            {
                // The record went straight onto an entity; there is nothing to deserialise.
                current = Assembler!.Take();
                hasAssembled = false;
                return;
            }
            var values = reader.GetValues();
            IReaderWithMetadata metadataReader = reader;
            var recordContext = metadataReader.GetMetadata();
            // No matcher accepted the record and no default took it, so the underlying reader parsed it with a schema
            // built from its own values and there is nothing to make an entity with.
            var deserialiser = Deserialiser ?? throw new FlatFileException( Properties.Resources.MissingMatcher );
            current = deserialiser( recordContext, values );
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
