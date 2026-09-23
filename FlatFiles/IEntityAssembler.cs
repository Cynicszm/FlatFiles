namespace FlatFiles
{
    /// <summary>
    ///     Reads a record onto an entity of its own making, in place of the reader parsing it into an array of
    ///     objects. A typed reader installs one of these so that a value never has to be boxed on its way from the
    ///     file to a property.
    /// </summary>
    /// <remarks>
    ///     This is not generic, because the reader that calls it is not; the implementation knows the entity type
    ///     and keeps the entity it made until the typed reader takes it.
    /// </remarks>
    internal interface IEntityAssembler
    {
        /// <summary>
        ///     Reads the record onto a new entity.
        /// </summary>
        /// <param name="context">The metadata for the record being processed.</param>
        /// <param name="schema">The schema the record was read with.</param>
        /// <param name="values">The raw values of the record.</param>
        void Assemble( IRecoverableRecordContext context, Schema schema, RawRecord values );
    }
}
