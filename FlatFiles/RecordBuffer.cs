using System;
using System.Buffers;

namespace FlatFiles
{
    /// <summary>
    ///     A growable character buffer that a record writer formats one record into before handing the whole
    ///     record to its <see cref="System.IO.TextWriter" /> in a single write. The buffer is reused from record to
    ///     record, so once it has grown to fit the widest record in the file it allocates nothing further.
    /// </summary>
    /// <remarks>
    ///     Besides the <see cref="IBufferWriter{T}" /> contract that columns format into, the buffer lets the writer
    ///     edit what has already been written in place: quoting a delimited value once its text is known, or
    ///     padding and truncating a fixed-length value to its window.
    /// </remarks>
    internal sealed class RecordBuffer : IBufferWriter<char>
    {
        private const int InitialCapacity = 256;

        private char[] array = new char[InitialCapacity];

        /// <summary>
        ///     Gets the number of characters written so far.
        /// </summary>
        public int Length { get; private set; }

        /// <summary>
        ///     Gets the characters written so far. The span is writable so a record writer can edit a value in place.
        /// </summary>
        public Span<char> WrittenSpan => array.AsSpan( 0, Length );

        /// <summary>
        ///     Gets the characters written so far as memory, for asynchronous writes.
        /// </summary>
        public ReadOnlyMemory<char> WrittenMemory => array.AsMemory( 0, Length );

        /// <summary>
        ///     Discards everything written so far while keeping the capacity.
        /// </summary>
        public void Clear()
        {
            Length = 0;
        }

        /// <summary>
        ///     Discards everything written past the given length.
        /// </summary>
        /// <param name="length">The number of characters to keep.</param>
        public void Truncate( int length )
        {
            ArgumentOutOfRangeException.ThrowIfNegative( length );
            ArgumentOutOfRangeException.ThrowIfGreaterThan( length, Length );
            Length = length;
        }

        /// <summary>
        ///     Grows the written region so that the text beginning at <paramref name="start" /> is
        ///     <paramref name="length" /> characters long, and returns that region. The characters already there
        ///     are kept; the new ones at the end are unspecified until the caller fills them.
        /// </summary>
        /// <param name="start">The position the region begins at.</param>
        /// <param name="length">The length the region should have afterwards.</param>
        /// <returns>The region from <paramref name="start" /> to the new end of the buffer.</returns>
        public Span<char> Extend( int start, int length )
        {
            ArgumentOutOfRangeException.ThrowIfNegative( start );
            ArgumentOutOfRangeException.ThrowIfGreaterThan( start, Length );
            var end = start + length;
            ArgumentOutOfRangeException.ThrowIfLessThan( end, Length );
            EnsureCapacity( end );
            Length = end;
            return array.AsSpan( start, length );
        }

        /// <summary>
        ///     Appends the given characters.
        /// </summary>
        /// <param name="value">The characters to append.</param>
        public void Write( ReadOnlySpan<char> value )
        {
            EnsureCapacity( Length + value.Length );
            value.CopyTo( array.AsSpan( Length ) );
            Length += value.Length;
        }

        /// <inheritdoc />
        public void Advance( int count )
        {
            ArgumentOutOfRangeException.ThrowIfNegative( count );
            ArgumentOutOfRangeException.ThrowIfGreaterThan( count, array.Length - Length );
            Length += count;
        }

        /// <inheritdoc />
        public Memory<char> GetMemory( int sizeHint = 0 )
        {
            EnsureFree( sizeHint );
            return array.AsMemory( Length );
        }

        /// <inheritdoc />
        public Span<char> GetSpan( int sizeHint = 0 )
        {
            EnsureFree( sizeHint );
            return array.AsSpan( Length );
        }

        private void EnsureFree( int sizeHint )
        {
            ArgumentOutOfRangeException.ThrowIfNegative( sizeHint );
            EnsureCapacity( Length + Math.Max( sizeHint, 1 ) );
        }

        private void EnsureCapacity( int capacity )
        {
            if (capacity <= array.Length)
            {
                return;
            }
            var newCapacity = Math.Max( capacity, array.Length * 2 );
            var newArray = new char[newCapacity];
            array.AsSpan( 0, Length ).CopyTo( newArray );
            array = newArray;
        }
    }
}
