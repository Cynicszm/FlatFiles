namespace FlatFiles
{
    internal sealed class DelimitedExecutionContext(DelimitedSchema schema, DelimitedOptions options) : IDelimitedExecutionContext
    {
        public DelimitedSchema Schema { get; } = schema;

        public DelimitedOptions Options { get; } = options;

        ISchema IExecutionContext.Schema => Schema;

        IOptions IExecutionContext.Options => Options;
    }
}
