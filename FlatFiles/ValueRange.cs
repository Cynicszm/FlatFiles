namespace FlatFiles
{
    /// <summary>
    ///     Where one raw value sits within the record it was read from, so that a reader can partition a record
    ///     without copying each value out of it.
    /// </summary>
    /// <param name="start">The index of the first character of the value within the record.</param>
    /// <param name="length">The number of characters in the value.</param>
    internal readonly struct ValueRange( int start, int length )
    {
        /// <summary>
        ///     The index of the first character of the value within the record.
        /// </summary>
        public int Start { get; } = start;

        /// <summary>
        ///     The number of characters in the value.
        /// </summary>
        public int Length { get; } = length;

        /// <summary>
        ///     Copies every value out of the record, for the callers that need them as strings.
        /// </summary>
        /// <param name="record">The record the values were partitioned from.</param>
        /// <param name="ranges">Where each value sits within the record.</param>
        /// <returns>The values as strings.</returns>
        public static string[] Materialise( string record, ValueRange[] ranges )
        {
            var values = new string[ranges.Length];
            for (var index = 0; index != ranges.Length; ++index)
            {
                var range = ranges[index];
                values[index] = record.Substring( range.Start, range.Length );
            }
            return values;
        }
    }
}
