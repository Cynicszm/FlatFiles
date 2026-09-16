namespace FlatFiles
{
    /// <summary>
    ///     Lets a record writer step in around each column while a schema formats a record into a buffer, so the
    ///     writer can add separators and quote, pad or truncate the column's text in place once it is known.
    /// </summary>
    internal interface IFormattedColumnHandler
    {
        /// <summary>
        ///     Called before a column is formatted.
        /// </summary>
        /// <param name="columnIndex">The physical index of the column about to be formatted.</param>
        /// <param name="destination">The buffer the record is being formatted into.</param>
        void ColumnStarting( int columnIndex, RecordBuffer destination );

        /// <summary>
        ///     Called after a column has been formatted. Its text runs from <paramref name="start" /> to the end of
        ///     the buffer.
        /// </summary>
        /// <param name="columnIndex">The physical index of the column just formatted.</param>
        /// <param name="start">The position in the buffer where the column's text begins.</param>
        /// <param name="destination">The buffer the record is being formatted into.</param>
        void ColumnFormatted( int columnIndex, int start, RecordBuffer destination );
    }
}
