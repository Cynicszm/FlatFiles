using System;

namespace FlatFiles
{
    internal sealed class FixedLengthRecordContext( FixedLengthExecutionContext executionContext ) : IFixedLengthRecordContext, IRecoverableRecordContext
    {
        private ValueRange[]? ranges;

        public event EventHandler<ColumnErrorEventArgs>? ColumnError;

        public FixedLengthExecutionContext ExecutionContext { get; } = executionContext;

        public int PhysicalRecordNumber { get; set; }

        public int LogicalRecordNumber { get; set; }

        public string? Record { get; set; }

        /// <summary>
        ///     The raw values of the record. Where the reader parsed the record without copying its values out of the
        ///     text they were read from, they are copied out here, the first time anything asks for them.
        /// </summary>
        public string[]? Values
        {
            get => field ??= Record is null || ranges is null ? null : ValueRange.Materialise( Record, string.Empty, ranges );
            set => field = value;
        }

        /// <summary>
        ///     Records where each raw value sits within the record, so that <see cref="Values" /> can be built from
        ///     the record if something asks for it and left unbuilt if nothing does.
        /// </summary>
        /// <param name="currentRanges">Where each value sits within the record.</param>
        public void SetPartitions( ValueRange[] currentRanges )
        {
            ranges = currentRanges;
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
