using System;
using System.IO;
using System.Threading.Tasks;

namespace FlatFiles
{
    /// <summary>
    ///     Buffers the characters of a <see cref="TextReader" /> so that a parser can scan them as one contiguous span.
    ///     Everything the parser has not yet consumed is kept across a refill, so a record that straddles two reads is
    ///     still contiguous afterwards, and the buffer grows if a single record outgrows it.
    /// </summary>
    /// <param name="reader">The reader to buffer.</param>
    /// <param name="bufferSize">The initial number of characters the buffer holds.</param>
    internal sealed class CharBufferReader( TextReader reader, int bufferSize = CharBufferReader.DefaultBufferSize )
    {
        /// <summary>
        ///     The initial buffer size when none is given. Larger buffers mean fewer refills and fewer records that
        ///     have to be re-scanned after one.
        /// </summary>
        public const int DefaultBufferSize = 16384;

        private char[] buffer = new char[bufferSize];

        private int start;

        private int end;

        /// <summary>
        ///     Gets whether the underlying reader has run out of characters. Buffered characters may still remain.
        /// </summary>
        public bool IsEndOfStream { get; private set; }

        /// <summary>
        ///     Gets the number of buffered characters not yet consumed.
        /// </summary>
        public int Available => end - start;

        /// <summary>
        ///     Gets the buffered characters not yet consumed.
        /// </summary>
        public ReadOnlySpan<char> Span => buffer.AsSpan( start, end - start );

        /// <summary>
        ///     Releases the given number of characters from the front of the buffer.
        /// </summary>
        /// <param name="count">The number of characters the parser has finished with.</param>
        public void Consume( int count )
        {
            ArgumentOutOfRangeException.ThrowIfNegative( count );
            ArgumentOutOfRangeException.ThrowIfGreaterThan( count, Available );
            start += count;
            if (start == end)
            {
                start = 0;
                end = 0;
            }
        }

        /// <summary>
        ///     Reads more characters from the reader, keeping everything not yet consumed.
        /// </summary>
        public void Fill()
        {
            if (IsEndOfStream)
            {
                return;
            }
            var free = MakeRoom();
            var read = reader.ReadBlock( free.Span );
            RecordRead( read, free.Length );
        }

        /// <summary>
        ///     Reads more characters from the reader, keeping everything not yet consumed.
        /// </summary>
        public async ValueTask FillAsync()
        {
            if (IsEndOfStream)
            {
                return;
            }
            var free = MakeRoom();
            var read = await reader.ReadBlockAsync( free ).ConfigureAwait( false );
            RecordRead( read, free.Length );
        }

        private Memory<char> MakeRoom()
        {
            if (start > 0)
            {
                buffer.AsSpan( start, end - start ).CopyTo( buffer );
                end -= start;
                start = 0;
            }
            if (end == buffer.Length)
            {
                var larger = new char[buffer.Length * 2];
                buffer.AsSpan( 0, end ).CopyTo( larger );
                buffer = larger;
            }
            return buffer.AsMemory( end );
        }

        private void RecordRead( int read, int requested )
        {
            end += read;
            // ReadBlock only returns fewer characters than asked for when the reader has nothing more to give.
            if (read < requested)
            {
                IsEndOfStream = true;
            }
        }
    }
}
