namespace FlatFiles
{
    internal sealed class GenericExecutionContext( ISchema? schema, IOptions options ) : ExecutionContextBase, IExecutionContext
    {
        public ISchema? Schema { get; } = schema;

        public IOptions Options { get; } = options;
    }
}
