using System;

namespace FlatFiles
{
    internal sealed class DelimitedRecordContext( DelimitedExecutionContext executionContext ) : IDelimitedRecordContext, IRecoverableRecordContext
    {
        private string? recordText;

        private string escapedText = string.Empty;

        private ValueRange[]? ranges;

        public event EventHandler<ColumnErrorEventArgs>? ColumnError;

        public DelimitedExecutionContext ExecutionContext { get; set; } = executionContext;

        public int PhysicalRecordNumber { get; set; }

        public int LogicalRecordNumber { get; set; }

        public string? Record { get; set; }

        /// <summary>
        ///     The raw values of the record. Where the reader parsed the record without copying its values out of the
        ///     text they were read from, they are copied out here, the first time anything asks for them.
        /// </summary>
        public string[]? Values
        {
            get => field ??= recordText is null || ranges is null ? null : ValueRange.Materialise( recordText, escapedText, ranges );
            set => field = value;
        }

        /// <summary>
        ///     Records the text the raw values were read from and where each sits within it, so that
        ///     <see cref="Values" /> can be built from them if something asks for it and left unbuilt if nothing does.
        /// </summary>
        /// <param name="currentText">The text of the record the values were read from.</param>
        /// <param name="currentEscaped">The text holding the values the record itself could not hold.</param>
        /// <param name="currentRanges">Where each value sits.</param>
        public void SetPartitions( string currentText, string currentEscaped, ValueRange[] currentRanges )
        {
            recordText = currentText;
            escapedText = currentEscaped;
            ranges = currentRanges;
        }

        IDelimitedExecutionContext IDelimitedRecordContext.ExecutionContext => ExecutionContext;

        IExecutionContext IRecordContext.ExecutionContext => ExecutionContext;

        public bool HasHandler => ColumnError is not null;

        public void ProcessError( object sender, ColumnErrorEventArgs e )
        {
            ColumnError?.Invoke( sender, e );
        }
    }
}
