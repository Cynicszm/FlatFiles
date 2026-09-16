using System;
using System.Buffers;

namespace FlatFiles
{
    /// <summary>
    ///     Recognises a record separator in a span of text. With no separator configured, a line ends at a carriage
    ///     return, a line feed, or a carriage return followed by a line feed, and the pair counts as one separator.
    /// </summary>
    internal sealed class RecordSeparatorMatcher
    {
        private static readonly SearchValues<char> LineBreaks = SearchValues.Create( "\r\n" );

        private readonly string? separator;

        /// <summary>
        ///     Initialises a matcher for the given separator.
        /// </summary>
        /// <param name="separator">The record separator, or null to accept any line break.</param>
        public RecordSeparatorMatcher( string? separator )
        {
            this.separator = separator;
            if (separator is null)
            {
                StartCharacters = LineBreaks;
                MaximumLength = 2;
            }
            else
            {
                StartCharacters = SearchValues.Create( separator.AsSpan( 0, 1 ) );
                MaximumLength = separator.Length;
            }
        }

        /// <summary>
        ///     Gets the characters a separator can begin with, for scanning ahead to the next candidate.
        /// </summary>
        public SearchValues<char> StartCharacters { get; }

        /// <summary>
        ///     Gets the most characters a separator can occupy, which is how far a parser must be able to see to
        ///     decide whether a candidate is one.
        /// </summary>
        public int MaximumLength { get; }

        /// <summary>
        ///     Determines whether a separator begins at the given position.
        /// </summary>
        /// <param name="text">The text being scanned.</param>
        /// <param name="position">The position to test.</param>
        /// <param name="length">The length of the separator found, or zero.</param>
        /// <returns>True if a separator begins at the position; otherwise, false.</returns>
        public bool IsMatch( ReadOnlySpan<char> text, int position, out int length )
        {
            if (separator is not null)
            {
                if (text[position..].StartsWith( separator ))
                {
                    length = separator.Length;
                    return true;
                }
                length = 0;
                return false;
            }
            if (position < text.Length)
            {
                if (text[position] == '\r')
                {
                    length = position + 1 < text.Length && text[position + 1] == '\n' ? 2 : 1;
                    return true;
                }
                if (text[position] == '\n')
                {
                    length = 1;
                    return true;
                }
            }
            length = 0;
            return false;
        }
    }
}
