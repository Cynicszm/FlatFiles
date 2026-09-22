using System;

namespace FlatFiles
{
    internal sealed class FixedLengthRecordContext( FixedLengthExecutionContext executionContext ) : IFixedLengthRecordContext, IRecoverableRecordContext
    {
        private IRawValueSource? source;

        private int generation;

        public event EventHandler<ColumnErrorEventArgs>? ColumnError;

        public FixedLengthExecutionContext ExecutionContext { get; } = executionContext;

        public int PhysicalRecordNumber { get; set; }

        public int LogicalRecordNumber { get; set; }

        public string? Record { get; set; }

        /// <summary>
        ///     The raw values of the record. They are copied out of the characters they were read from the first
        ///     time anything asks for them, and only while the reader is still on this record: a context kept past
        ///     that reports null, because those characters are gone.
        /// </summary>
        public string[]? Values
        {
            get => field ??= source is not null && source.Generation == generation ? source.MaterialiseValues() : null;
            set => field = value;
        }

        /// <summary>
        ///     Records where the raw values can be had from, so that they are copied out only if something asks.
        /// </summary>
        /// <param name="currentSource">The reader, which answers for the record it is on.</param>
        public void SetValueSource( IRawValueSource currentSource )
        {
            source = currentSource;
            generation = currentSource.Generation;
        }

        IFixedLengthExecutionContext IFixedLengthRecordContext.ExecutionContext => ExecutionContext;

        IExecutionContext IRecordContext.ExecutionContext => ExecutionContext;

        public bool HasHandler => ColumnError is not null;

        public void ProcessError( object sender, ColumnErrorEventArgs e )
        {
            ColumnError?.Invoke( sender, e );
        }
    }
}
