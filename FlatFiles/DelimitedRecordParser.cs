using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FlatFiles.Properties;

namespace FlatFiles
{
    /// <summary>
    ///     Splits delimited text into records and their values. The parser scans the buffered text as a span, jumping
    ///     from one candidate separator or quote to the next with a vectorised search rather than examining every
    ///     character. Nothing is copied out of the buffer: a record is a set of ranges into the buffered text, which
    ///     a column parses straight from, so a record whose values nobody asks to see as strings costs nothing at all.
    ///     A record that runs off the end of the buffer is scanned again from its start once more text has been read,
    ///     which keeps a single state machine for both the synchronous and the asynchronous reader.
    /// </summary>
    /// <remarks>
    ///     The characters of a record stay in the buffer only until the next call on this parser, which is what makes
    ///     the reader's record context report its raw values for the record being processed and null afterwards. The
    ///     release of the previous record is deferred to the start of the next call for exactly that reason.
    /// </remarks>
    internal sealed class DelimitedRecordParser
    {
        private readonly CharBufferReader reader;
        private readonly RecordBuffer escaped = new();
        private ValueRange[] valueRanges = new ValueRange[16];
        private int valueCount;
        private int pending;
        private readonly string separator;
        private readonly RecordSeparatorMatcher recordSeparator;
        private readonly string? postfix;
        private readonly SearchValues<char> stops;
        private readonly int lookahead;
        private readonly char quote;
        private readonly bool preserveWhiteSpace;
        private readonly bool preserveRecordText;

        public DelimitedRecordParser( TextReader reader, DelimitedOptions options )
            : this( new CharBufferReader( reader ), options )
        {
        }

        internal DelimitedRecordParser( CharBufferReader reader, DelimitedOptions options )
        {
            this.reader = reader;
            Options = options.Clone();
            separator = Options.Separator;
            recordSeparator = new RecordSeparatorMatcher( Options.RecordSeparator );
            // When the record separator begins with the separator, finding the separator is not enough: the
            // characters after it decide whether the record has ended too.
            if (Options.RecordSeparator is not null && Options.RecordSeparator.StartsWith( separator, StringComparison.Ordinal ))
            {
                postfix = Options.RecordSeparator[separator.Length..];
            }
            var stopCharacters = Options.RecordSeparator is null
                ? $"{separator[0]}\r\n"
                : $"{separator[0]}{Options.RecordSeparator[0]}";
            stops = SearchValues.Create( stopCharacters );
            lookahead = Math.Max( Math.Max( separator.Length, recordSeparator.MaximumLength ), 2 );
            quote = Options.Quote;
            preserveWhiteSpace = Options.PreserveWhiteSpace;
            preserveRecordText = Options.PreserveRecordText;
        }

        internal DelimitedOptions Options { get; }

        /// <summary>
        ///     Releases the record read last, which is kept in the buffer until the caller has finished with it.
        /// </summary>
        private void Release()
        {
            if (pending == 0)
            {
                return;
            }
            reader.Consume( pending );
            pending = 0;
        }

        public bool IsEndOfStream()
        {
            Release();
            if (reader is { Available: 0, IsEndOfStream: false })
            {
                reader.Fill();
            }
            return reader is { IsEndOfStream: true, Available: 0 };
        }

        public async ValueTask<bool> IsEndOfStreamAsync( CancellationToken cancellationToken = default )
        {
            Release();
            if (reader is { Available: 0, IsEndOfStream: false })
            {
                await reader.FillAsync( cancellationToken ).ConfigureAwait( false );
            }
            return reader is { IsEndOfStream: true, Available: 0 };
        }

        /// <summary>
        ///     The raw values of the record last read, as ranges into the buffered text and, for the few values the
        ///     record's own characters cannot give, into the buffer they were rebuilt in. Valid until the next call
        ///     on this parser.
        /// </summary>
        public RawRecord Values => new( reader.Span, escaped.WrittenSpan, valueRanges.AsSpan( 0, valueCount ) );

        public string ReadRecord()
        {
            Release();
            string record;
            while (!TryReadRecord( out record ))
            {
                reader.Fill();
            }
            return record;
        }

        public async Task<string> ReadRecordAsync( CancellationToken cancellationToken = default )
        {
            Release();
            string record;
            while (!TryReadRecord( out record ))
            {
                await reader.FillAsync( cancellationToken ).ConfigureAwait( false );
            }
            return record;
        }

        /// <summary>
        ///     Scans one record from the start of the buffered text. Returns false if the buffer ran out before the
        ///     record ended, in which case nothing is consumed and the caller reads more text and tries again.
        /// </summary>
        private bool TryReadRecord( out string record )
        {
            var text = reader.Span;
            valueCount = 0;
            escaped.Clear();
            var position = 0;
            while (true)
            {
                var end = ReadToken( text, ref position, out var separatorStart );
                switch (end)
                {
                    case TokenEnd.NeedMore:
                        record = string.Empty;
                        return false;
                    case TokenEnd.Token:
                        continue;
                }
                record = preserveRecordText ? new string( text[..separatorStart] ) : string.Empty;
                // The record stays in the buffer until the next call, because its values are ranges into it.
                pending = position;
                return true;
            }
        }

        /// <summary>
        ///     Records where one value of the record sits.
        /// </summary>
        /// <param name="start">The position the value begins at.</param>
        /// <param name="length">The number of characters in the value.</param>
        /// <param name="isEscaped">Whether the value sits in the escape buffer rather than in the record.</param>
        private void AddRange( int start, int length, bool isEscaped = false )
        {
            if (valueCount == valueRanges.Length)
            {
                Array.Resize( ref valueRanges, valueRanges.Length * 2 );
            }
            valueRanges[valueCount] = new ValueRange( start, length, isEscaped );
            ++valueCount;
        }

        private TokenEnd ReadToken( ReadOnlySpan<char> text, ref int position, out int separatorStart )
        {
            var start = position;
            if (!preserveWhiteSpace)
            {
                // Leading whitespace is dropped, but a separator wins over whitespace, so a separator made of
                // whitespace still ends the token.
                while (true)
                {
                    if (NeedsMore( text, start ))
                    {
                        separatorStart = 0;
                        return TokenEnd.NeedMore;
                    }
                    var end = MatchSeparator( text, start, out var length );
                    if (end != TokenEnd.None)
                    {
                        AddRange( start, 0 );
                        separatorStart = start;
                        position = start + length;
                        return end;
                    }
                    if (!char.IsWhiteSpace( text[start] ))
                    {
                        break;
                    }
                    ++start;
                }
            }
            else if (NeedsMore( text, start ))
            {
                separatorStart = 0;
                return TokenEnd.NeedMore;
            }
            if (start < text.Length && text[start] == quote)
            {
                return ReadQuotedToken( text, start + 1, ref position, out separatorStart );
            }
            return ReadUnquotedToken( text, start, ref position, out separatorStart );
        }

        private TokenEnd ReadUnquotedToken( ReadOnlySpan<char> text, int start, ref int position, out int separatorStart )
        {
            var scan = start;
            while (true)
            {
                var index = text[scan..].IndexOfAny( stops );
                if (index < 0)
                {
                    if (reader.IsEndOfStream)
                    {
                        AddRange( start, text.Length - start );
                        separatorStart = text.Length;
                        position = text.Length;
                        return TokenEnd.Stream;
                    }
                    separatorStart = 0;
                    return TokenEnd.NeedMore;
                }
                var candidate = scan + index;
                if (NeedsMore( text, candidate ))
                {
                    separatorStart = 0;
                    return TokenEnd.NeedMore;
                }
                var end = MatchSeparator( text, candidate, out var length );
                if (end != TokenEnd.None)
                {
                    AddRange( start, candidate - start );
                    separatorStart = candidate;
                    position = candidate + length;
                    return end;
                }
                scan = candidate + 1;
            }
        }

        /// <summary>
        ///     Reads a quoted value. Stripping the quotes leaves a slice of the record, so nothing is copied unless a
        ///     doubled quote, or whitespace kept from after the closing quote, breaks the value into pieces that are
        ///     not next to each other in the record; only then is it rebuilt in the escape buffer.
        /// </summary>
        private TokenEnd ReadQuotedToken( ReadOnlySpan<char> text, int contentStart, ref int position, out int separatorStart )
        {
            var escapeStart = escaped.Length;
            var isEscaped = false;
            var scan = contentStart;
            while (true)
            {
                var index = text[scan..].IndexOf( quote );
                if (index < 0)
                {
                    if (reader.IsEndOfStream)
                    {
                        throw new DelimitedSyntaxException( Resources.UnmatchedQuote );
                    }
                    separatorStart = 0;
                    return TokenEnd.NeedMore;
                }
                var quoteAt = scan + index;
                var next = quoteAt + 1;
                if (next >= text.Length && !reader.IsEndOfStream)
                {
                    // Whether this quote closes the value or is the first of a doubled pair depends on the next character.
                    separatorStart = 0;
                    return TokenEnd.NeedMore;
                }
                if (next < text.Length && text[next] == quote)
                {
                    // A doubled quote stands for one, so from here the value has to be built character by character.
                    escaped.Write( isEscaped ? text[scan..quoteAt] : text[contentStart..quoteAt] );
                    isEscaped = true;
                    escaped.Write( text.Slice( quoteAt, 1 ) );
                    scan = next + 1;
                    continue;
                }
                if (isEscaped)
                {
                    escaped.Write( text[scan..quoteAt] );
                }
                // The value has closed. Only whitespace may follow it before the separator.
                var after = next;
                while (true)
                {
                    if (NeedsMore( text, after ))
                    {
                        separatorStart = 0;
                        return TokenEnd.NeedMore;
                    }
                    var end = MatchSeparator( text, after, out var length );
                    if (end != TokenEnd.None)
                    {
                        if (isEscaped)
                        {
                            AddRange( escapeStart, escaped.Length - escapeStart, true );
                        }
                        else
                        {
                            AddRange( contentStart, quoteAt - contentStart );
                        }
                        separatorStart = after;
                        position = after + length;
                        return end;
                    }
                    if (!char.IsWhiteSpace( text[after] ))
                    {
                        throw new DelimitedSyntaxException( Resources.UnmatchedQuote );
                    }
                    if (preserveWhiteSpace)
                    {
                        // The kept whitespace is not next to the value in the record, the closing quote is in between.
                        if (!isEscaped)
                        {
                            escaped.Write( text[contentStart..quoteAt] );
                            isEscaped = true;
                        }
                        escaped.Write( text.Slice( after, 1 ) );
                    }
                    ++after;
                }
            }
        }

        /// <summary>
        ///     Determines whether the buffer holds too little text past the position to decide what is there. Once the
        ///     reader is exhausted everything that remains can be decided.
        /// </summary>
        private bool NeedsMore( ReadOnlySpan<char> text, int position )
        {
            return text.Length - position < lookahead && !reader.IsEndOfStream;
        }

        private TokenEnd MatchSeparator( ReadOnlySpan<char> text, int position, out int length )
        {
            if (position >= text.Length)
            {
                length = 0;
                return TokenEnd.Stream;
            }
            if (text[position..].StartsWith( separator ))
            {
                if (postfix is not null && text[(position + separator.Length)..].StartsWith( postfix ))
                {
                    length = separator.Length + postfix.Length;
                    return TokenEnd.Record;
                }
                length = separator.Length;
                return TokenEnd.Token;
            }
            // When the record separator begins with the separator and the separator was not found, the record
            // separator cannot be here either.
            if (postfix is null && recordSeparator.IsMatch( text, position, out length ))
            {
                return TokenEnd.Record;
            }
            length = 0;
            return TokenEnd.None;
        }

        private enum TokenEnd
        {
            None,
            Token,
            Record,
            Stream,
            NeedMore
        }
    }
}
