namespace FlatFiles
{
    /// <summary>
    ///     Where a record context gets its raw values from, and how it knows they are still there. A reader
    ///     implements this once and answers for whichever record it is on, so that a context costs nothing per
    ///     record and nothing at all unless something asks it for the values.
    /// </summary>
    internal interface IRawValueSource
    {
        /// <summary>
        ///     How many records the reader has begun. A context remembers the number it was made for; once the
        ///     reader has moved on, the characters the values were read from are gone and the context says so.
        /// </summary>
        int Generation { get; }

        /// <summary>
        ///     Copies the current record's values out as strings.
        /// </summary>
        /// <returns>The values of the record the reader is on.</returns>
        string[] MaterialiseValues();
    }
}
