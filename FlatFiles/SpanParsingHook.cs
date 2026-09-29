using System;

namespace FlatFiles
{
    /// <summary>
    ///     Transforms a value before its column parses it, without either side building a string.
    /// </summary>
    /// <param name="context">Holds information about the column being processed.</param>
    /// <param name="value">The value as it lies in the record being read.</param>
    /// <param name="destination">Where to write the replacement value.</param>
    /// <returns>
    ///     The number of characters written to <paramref name="destination" />;
    ///     <see cref="SpanParsingHooks.Unchanged" /> to parse <paramref name="value" /> as it is; or the negation
    ///     of the number of characters needed, where <paramref name="destination" /> is too small.
    /// </returns>
    /// <remarks>
    ///     This is the counterpart of <see cref="ColumnDefinition.OnParsing" /> that costs nothing to use. That one
    ///     is handed the value as a string and returns another, so a reader has to copy every value out of the
    ///     record to call it and the hook has to build a string to answer - and because it deals in strings, a
    ///     column carrying one cannot be read straight onto an entity. A hook of this shape reads the value where
    ///     it lies and writes its answer into a buffer the reader owns, so neither happens.
    ///     <para>
    ///         Neither span outlives the call. Do not keep hold of either, and write only to
    ///         <paramref name="destination" />.
    ///     </para>
    ///     <para>
    ///         Where the replacement does not fit, return the negation of the length needed rather than writing a
    ///         partial answer; the reader will call again with a buffer at least that long. Returning
    ///         <see cref="SpanParsingHooks.Unchanged" /> is not the same as writing the value back out: nothing is
    ///         copied at all.
    ///     </para>
    /// </remarks>
    public delegate int SpanParsingHook( IColumnContext? context, ReadOnlySpan<char> value, Span<char> destination );

    /// <summary>
    ///     The values a <see cref="SpanParsingHook" /> can return besides a length.
    /// </summary>
    public static class SpanParsingHooks
    {
        /// <summary>
        ///     Returned by a hook that wants the value parsed exactly as it lies, having written nothing.
        /// </summary>
        /// <remarks>
        ///     Deliberately a value that no request for a length can produce. A request is the negation of the
        ///     length wanted, so the largest one is -1; were this that, a hook asking for a single character would
        ///     be read as wanting the value left alone, and its answer would be dropped without a word.
        /// </remarks>
        public const int Unchanged = int.MinValue;

        /// <summary>
        ///     Returned by a hook that needs a longer buffer than it was given.
        /// </summary>
        /// <param name="length">The number of characters the hook needs.</param>
        /// <returns>The value to return from the hook.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The length is not positive.</exception>
        /// <remarks>
        ///     Every length this accepts encodes as something distinct from <see cref="Unchanged" />, one included.
        /// </remarks>
        public static int NeedsLength( int length )
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero( length );
            return -length;
        }

        /// <summary>
        ///     The fewest characters a hook is ever offered, whatever the length of the value.
        /// </summary>
        /// <remarks>
        ///     A reader offers room for the value plus this much, so a hook writing a sign, a decimal point or a
        ///     stripped symbol usually has somewhere to put it without asking for more.
        /// </remarks>
        public const int MinimumOffered = 16;
    }
}
