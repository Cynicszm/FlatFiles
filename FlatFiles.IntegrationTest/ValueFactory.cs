using System;
using System.Globalization;

namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     What a field reads as: a date, a number, text, or nothing. A field of spaces reads as
    ///     <see cref="Empty" /> while still having a width, which is how the profiles record one.
    /// </summary>
    internal enum ValueKind
    {
        Date,
        Numeric,
        String,
        Empty
    }

    /// <summary>
    ///     Builds a field of an exact width. The content is simulated and means nothing; what matters is that it
    ///     is the width the profile asked for, that it parses as the kind the profile asked for, and that it
    ///     differs from the field before it, because a column holding the same forty characters on every record
    ///     would measure the cache rather than the parser.
    /// </summary>
    internal static class ValueFactory
    {
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        private static readonly DateTime Epoch = new( 1996, 1, 1 );

        public static void Append( Span<char> destination, ValueKind kind, int width, long sequence )
        {
            if (width == 0)
            {
                return;
            }
            switch (kind)
            {
                case ValueKind.Empty:
                    destination.Fill( ' ' );
                    return;
                case ValueKind.Date:
                    AppendDate( destination, width, sequence );
                    return;
                case ValueKind.Numeric:
                    AppendNumeric( destination, width, sequence );
                    return;
                default:
                    AppendString( destination, width, sequence );
                    return;
            }
        }

        /// <summary>
        ///     A value that depends on both the record and the position within the field, so that neither a column
        ///     nor a record repeats itself. Any cheap avalanche would do; this one is the mix from splitmix64.
        /// </summary>
        private static long Mix( long sequence, int index )
        {
            unchecked
            {
                var value = (ulong) sequence * 0x9E3779B97F4A7C15UL + (ulong) index * 0xBF58476D1CE4E5B9UL;
                value ^= value >> 30;
                value *= 0xBF58476D1CE4E5B9UL;
                value ^= value >> 27;
                value *= 0x94D049BB133111EBUL;
                value ^= value >> 31;
                return (long) ( value & long.MaxValue );
            }
        }

        /// <summary>
        ///     The widths a date can be written in. A width with no format of its own is filled with digits
        ///     instead, which is what the profiled file does where a date column holds something else.
        /// </summary>
        private static string? FormatFor( int width )
        {
            return width switch
            {
                6 => "yyMMdd",
                8 => "yyyyMMdd",
                10 => "yyyy-MM-dd",
                14 => "yyyyMMddHHmmss",
                19 => "yyyy-MM-dd HH:mm:ss",
                23 => "yyyy-MM-dd HH:mm:ss.fff",
                _ => null
            };
        }

        private static void AppendDate( Span<char> destination, int width, long sequence )
        {
            var format = FormatFor( width );
            if (format is null)
            {
                AppendNumeric( destination, width, sequence );
                return;
            }
            // Days first, so that a column written as yyyyMMdd varies record to record rather than only where
            // the clock happens to cross midnight. Thirty years of them, and a time of day underneath.
            var moment = Epoch.AddDays( Mix( sequence, 1 ) % 11000 ).AddSeconds( Mix( sequence, 2 ) % 86400 );
            moment.TryFormat( destination, out _, format, CultureInfo.InvariantCulture );
        }

        private static void AppendNumeric( Span<char> destination, int width, long sequence )
        {
            // Four characters is the shortest that leaves room for a decimal point and two places after it.
            if (width < 4)
            {
                AppendDigits( destination, sequence );
                return;
            }
            AppendDigits( destination[..( width - 3 )], sequence );
            destination[width - 3] = '.';
            AppendDigits( destination[( width - 2 )..], sequence + 1 );
        }

        private static void AppendDigits( Span<char> destination, long sequence )
        {
            for (var index = 0; index != destination.Length; ++index)
            {
                destination[index] = (char) ( '0' + (int) ( Mix( sequence, index + 3 ) % 10 ) );
            }
        }

        private static void AppendString( Span<char> destination, int width, long sequence )
        {
            for (var index = 0; index != width; ++index)
            {
                destination[index] = Alphabet[(int) ( Mix( sequence, index + 17 ) % Alphabet.Length )];
            }
        }

        /// <summary>
        ///     The ragged final column: a value pushed to the right of a wider field, so the field has one width
        ///     and the value inside it another. The shape - a short code, a run of spaces and a decimal tail - is
        ///     what makes it worth reading as its own case.
        /// </summary>
        public static void AppendRightAligned( Span<char> destination, int trimmed, long sequence )
        {
            destination.Fill( ' ' );
            if (trimmed <= 0)
            {
                return;
            }
            var value = destination[^trimmed..];
            var tail = Math.Min( 18, trimmed );
            AppendNumeric( value[^tail..], tail, sequence );
            if (trimmed == tail)
            {
                return;
            }
            var code = Math.Min( 3, trimmed - tail );
            AppendString( value, code, sequence );
            value[code..^tail].Fill( ' ' );
        }
    }
}
