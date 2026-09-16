using System;

namespace FlatFiles
{
    internal sealed class FixedLengthRecordContext(FixedLengthExecutionContext executionContext) : IFixedLengthRecordContext, IRecoverableRecordContext
    {
        public event EventHandler<ColumnErrorEventArgs>? ColumnError;

        public FixedLengthExecutionContext ExecutionContext { get; } = executionContext;

        public int PhysicalRecordNumber { get; set; }

        public int LogicalRecordNumber { get; set; }

        public string? Record { get; set; }

        public string[]? Values { get; set; }

        IFixedLengthExecutionContext IFixedLengthRecordContext.ExecutionContext => ExecutionContext;

        IExecutionContext IRecordContext.ExecutionContext => ExecutionContext;

        public bool HasHandler => ColumnError != null;

        public void ProcessError(object sender, ColumnErrorEventArgs e)
        {
            ColumnError?.Invoke(sender, e);
        }
    }
}
