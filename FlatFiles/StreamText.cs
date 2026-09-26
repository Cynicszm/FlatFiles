using System;
using System.IO;
using System.Text;

namespace FlatFiles
{
    /// <summary>
    ///     Reads text out of a stream the way a reader given a stream should: UTF-8 unless the bytes say otherwise,
    ///     the byte order mark taken off, and the stream left open for whoever opened it.
    /// </summary>
    /// <remarks>
    ///     This is a convenience rather than a saving. Decoding a megabyte and a third of UTF-8 costs about a fifth
    ///     of a millisecond and a byte a record, because a <see cref="StreamReader" /> reuses its buffers - the same
    ///     file read through one rather than through a <see cref="StringReader" /> allocates the same bytes a record
    ///     either way. What it is worth having for is the file whose encoding has to be sniffed rather than assumed,
    ///     and for not making every caller remember the three arguments below.
    /// </remarks>
    internal static class StreamText
    {
        /// <summary>
        ///     Wraps the stream in a reader.
        /// </summary>
        /// <param name="stream">The stream to read the document from.</param>
        /// <param name="encoding">The encoding to read it as, or null for UTF-8.</param>
        /// <remarks>
        ///     A byte order mark is always honoured, whatever encoding was asked for: a file that says what it is
        ///     should be read as what it says, and a mark left in the text becomes part of the first column's first
        ///     value, which is the bug this exists to stop.
        ///     <para>
        ///         The stream is left open. A reader does not own what it was handed, and none of this library's
        ///         readers is disposable, so closing it here would close something the caller still has.
        ///     </para>
        /// </remarks>
        public static TextReader Over( Stream stream, Encoding? encoding )
        {
            ArgumentNullException.ThrowIfNull( stream );
            return new StreamReader( stream, encoding ?? Utf8, detectEncodingFromByteOrderMarks: true, bufferSize: -1, leaveOpen: true );
        }

        /// <summary>
        ///     UTF-8 that writes no byte order mark of its own and does not throw on bytes it cannot make sense of,
        ///     which is what a reader of somebody else's file wants.
        /// </summary>
        private static readonly UTF8Encoding Utf8 = new( encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false );
    }
}
