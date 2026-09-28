namespace FlatFiles
{
    /// <summary>
    ///     How a reader given both a schema and a header decides which column each value in a record belongs to.
    /// </summary>
    /// <remarks>
    ///     Only the delimited reader can do this. A fixed-length header cannot be partitioned without the widths in
    ///     the order the file has them, which is the thing the header would be telling you.
    /// </remarks>
    public enum HeaderMatching
    {
        /// <summary>
        ///     The header is read and discarded, and each value belongs to the column in the same position. This is
        ///     the default, and is what the reader has always done.
        /// </summary>
        ByPosition = 0,

        /// <summary>
        ///     Each column takes the value under the header of the same name, wherever in the record that sits. A
        ///     column the header does not carry is an error, and a header column the schema does not declare is not
        ///     read.
        /// </summary>
        ByName = 1,

        /// <summary>
        ///     As <see cref="ByName" />, except that a column the header does not carry is read as empty rather than
        ///     treated as an error.
        /// </summary>
        ByNameWhereFound = 2
    }
}
