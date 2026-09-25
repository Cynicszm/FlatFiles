namespace FlatFiles.TypeMapping
{
    internal interface ITypeMatcherContext
    {
        int LogicalCount { get; }

        void Serialise(IRecordContext context, object? value, object?[] values);
    }
}
