namespace FlatFiles
{
    internal class GenericExecutionContext(ISchema? schema, IOptions options) : IExecutionContext
    {
        public ISchema? Schema { get; } = schema;

        public IOptions Options { get; } = options;
    }
}
