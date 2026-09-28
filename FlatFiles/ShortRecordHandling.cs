namespace FlatFiles
{
    /// <summary>
    ///     What a reader does with a record that carries less than the schema asks for.
    /// </summary>
    /// <remarks>
    ///     A delimited record is short when it has fewer values than the schema has columns to fill; a fixed-length
    ///     one is short when it ends before the last window does. Both readers answer the question the same way.
    /// </remarks>
    public enum ShortRecordHandling
    {
        /// <summary>
        ///     The record is reported as a <see cref="RecordProcessingException" /> and not returned. This is the
        ///     default, and is what both readers have always done.
        /// </summary>
        Refuse = 0,

        /// <summary>
        ///     The columns the record does not reach are read as empty, which their null handling turns into null,
        ///     and the record is returned.
        /// </summary>
        Pad = 1
    }
}
