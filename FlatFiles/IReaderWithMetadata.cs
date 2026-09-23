namespace FlatFiles
{
    internal interface IReaderWithMetadata : IReader
    {
        IRecordContext GetMetadata();

        /// <summary>
        ///     The parsed values of the record just read, as the reader holds them rather than as a copy.
        /// </summary>
        /// <remarks>
        ///     <see cref="IReader.GetValues" /> hands a caller a copy, because a caller may keep the array and write
        ///     to it. A type mapper does neither: it reads each value once, builds an entity, and lets the array go.
        ///     Copying it for that is a second array per record that nothing ever looks at.
        /// </remarks>
        /// <returns>The reader's own values, valid until the next record is read.</returns>
        object?[] GetCurrentValues();

        /// <summary>
        ///     Set by a typed reader that will take each record onto an entity itself, so that the reader does not
        ///     parse the values into objects first. Null where the reader parses them as usual.
        /// </summary>
        IEntityAssembler? Assembler { get; set; }
    }
}
