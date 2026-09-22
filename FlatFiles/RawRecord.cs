using System;

namespace FlatFiles
{
    /// <summary>
    ///     The raw values of one record, as ranges within the text they were read from, so that a schema can parse a
    ///     record without a string for every value. A value is a slice of the record itself unless the reader had to
    ///     rebuild it, in which case it is a slice of the text it was rebuilt in.
    /// </summary>
    internal readonly ref struct RawRecord
    {
        private readonly ReadOnlySpan<char> record;

        private readonly ReadOnlySpan<char> escaped;

        private readonly ReadOnlySpan<ValueRange> ranges;

        /// <summary>
        ///     Initialises a new RawRecord. A ref-like parameter cannot be captured by a primary constructor, so the
        ///     three spans are stored here.
        /// </summary>
        /// <param name="record">The record the values were read from.</param>
        /// <param name="escaped">The text holding the values the record itself could not hold, if any.</param>
        /// <param name="ranges">Where each value sits.</param>
        public RawRecord( ReadOnlySpan<char> record, ReadOnlySpan<char> escaped, ReadOnlySpan<ValueRange> ranges )
        {
            this.record = record;
            this.escaped = escaped;
            this.ranges = ranges;
        }

        /// <summary>
        ///     Gets the number of values in the record.
        /// </summary>
        public int Count => ranges.Length;

        /// <summary>
        ///     Gets the characters of the value at the given position.
        /// </summary>
        /// <param name="index">The position of the value within the record.</param>
        public ReadOnlySpan<char> this[int index]
        {
            get
            {
                var range = ranges[index];
                var source = range.IsEscaped ? escaped : record;
                return source.Slice( range.Start, range.Length );
            }
        }

        /// <summary>
        ///     Copies every value out as a string, for the callers that need them that way.
        /// </summary>
        /// <returns>The values of the record.</returns>
        public string[] Materialise()
        {
            var values = new string[ranges.Length];
            for (var index = 0; index != values.Length; ++index)
            {
                values[index] = this[index].ToString();
            }
            return values;
        }
    }
}
