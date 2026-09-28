namespace FlatFiles
{
    /// <summary>
    ///     What a reader does with a record that carries more than the schema asks for.
    /// </summary>
    /// <remarks>
    ///     A delimited record is long when it has more values than the schema has columns to put them in; a
    ///     fixed-length one is long when characters follow its last window. Both readers answer the question the
    ///     same way.
    /// </remarks>
    public enum LongRecordHandling
    {
        /// <summary>
        ///     What the record carries beyond the schema is not read, and the record is returned. This is the
        ///     default, and is what both readers have always done unless asked otherwise.
        /// </summary>
        Discard = 0,

        /// <summary>
        ///     The record is reported as a <see cref="RecordProcessingException" /> and not returned, with the same
        ///     record context a short record gets, so both can be handled in the same way.
        /// </summary>
        Refuse = 1
    }
}
