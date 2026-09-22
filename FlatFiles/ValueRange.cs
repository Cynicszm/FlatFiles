using System;

namespace FlatFiles
{
    /// <summary>
    ///     Where one raw value sits within the record it was read from, so that a reader can partition a record
    ///     without copying each value out of it.
    /// </summary>
    /// <param name="start">The index of the first character of the value within the text it was read from.</param>
    /// <param name="length">The number of characters in the value.</param>
    /// <param name="isEscaped">
    ///     Whether the value sits in the text a parser had to rebuild it in rather than in the record itself.
    /// </param>
    internal readonly struct ValueRange( int start, int length, bool isEscaped = false )
    {
        /// <summary>
        ///     The start, with an escaped value's held as its complement so that the pair still costs two integers.
        ///     There is one of these per column per record, and an escaped value is rare enough that a field of its
        ///     own would grow every range for the few that need it.
        /// </summary>
        private readonly int start = isEscaped ? ~start : start;

        /// <summary>
        ///     The index of the first character of the value within the text it was read from.
        /// </summary>
        public int Start => start < 0 ? ~start : start;

        /// <summary>
        ///     The number of characters in the value.
        /// </summary>
        public int Length { get; } = length;

        /// <summary>
        ///     Whether the value sits in the text a parser had to rebuild it in rather than in the record itself. A
        ///     delimited value is a slice of its record unless a doubled quote, or whitespace kept from around it,
        ///     means its characters are not next to each other there.
        /// </summary>
        public bool IsEscaped => start < 0;

        /// <summary>
        ///     Copies every value out of the text it was read from, for the callers that need them as strings.
        /// </summary>
        /// <param name="record">The record the values were partitioned from.</param>
        /// <param name="escaped">The text holding the values the record itself could not hold, if any.</param>
        /// <param name="ranges">Where each value sits.</param>
        /// <returns>The values as strings.</returns>
        public static string[] Materialise( string record, string escaped, ReadOnlySpan<ValueRange> ranges )
        {
            var values = new string[ranges.Length];
            for (var index = 0; index != ranges.Length; ++index)
            {
                var range = ranges[index];
                values[index] = (range.IsEscaped ? escaped : record).Substring( range.Start, range.Length );
            }
            return values;
        }
    }
}
