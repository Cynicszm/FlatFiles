namespace FlatFiles
{
    internal sealed class GenericRecordContext( IExecutionContext context ) : IRecordContext
    {
        public IExecutionContext ExecutionContext { get; } = context;

        public int PhysicalRecordNumber { get; set; }

        public int LogicalRecordNumber { get; set; }

        public string? Record { get; set; }

        public string[]? Values { get; set; }
    }
}
