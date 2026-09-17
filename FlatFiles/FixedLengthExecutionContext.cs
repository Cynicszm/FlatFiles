namespace FlatFiles
{
    internal sealed class FixedLengthExecutionContext( FixedLengthSchema schema, FixedLengthOptions options ) : IFixedLengthExecutionContext
    {
        public FixedLengthSchema Schema { get; } = schema; // We guarantee Schema returns non-null before exposing interface

        public FixedLengthOptions Options { get; } = options;

        ISchema IExecutionContext.Schema => Schema;

        IOptions IExecutionContext.Options => Options;
    }
}
