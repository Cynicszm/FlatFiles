using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     Writes a fixed-length file of the shape a profile describes: every record the same width, the header
    ///     first and the trailer last, and the rest drawn so that each layout appears as often as the profile says
    ///     it does. Which layout a record is has to be readable from the record itself, so the character the
    ///     profile selects on is written before anything else fills the record.
    /// </summary>
    internal static class FixedLengthGenerator
    {
        public static GenerationResult Generate( FileProfile profile, string path )
        {
            var layouts = new RecordLayout[profile.RecordTypes.Count];
            for (var index = 0; index != layouts.Length; ++index)
            {
                layouts[index] = new RecordLayout( profile.RecordTypes[index], index + 1 );
            }

            var body = Array.FindAll( layouts, x => !x.IsPrefixed );
            var bodyRecords = 0L;
            foreach (var layout in body)
            {
                bodyRecords += layout.RecordCount;
            }

            var record = new char[profile.RecordLength];
            var watch = Stopwatch.StartNew();
            Directory.CreateDirectory( Path.GetDirectoryName( path )! );
            using var stream = new FileStream( path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20 );
            using var writer = new StreamWriter( stream, new UTF8Encoding( false ), 1 << 20 );

            var written = 0L;
            foreach (var layout in layouts)
            {
                if (layout.IsPrefixed && layout.Key == "Header")
                {
                    WriteRecord( writer, record, layout, written++ );
                }
            }

            // The body layouts are interleaved rather than written in blocks, so the selector has to choose
            // afresh on almost every record, as it does on the file this was profiled from.
            var remaining = new long[body.Length];
            for (var index = 0; index != body.Length; ++index)
            {
                remaining[index] = body[index].RecordCount;
            }
            var sequence = new Sequence( 0.12345 );
            for (var number = 0L; number != bodyRecords; ++number)
            {
                var choice = Choose( body, remaining, sequence.Next(), bodyRecords - number );
                --remaining[choice];
                WriteRecord( writer, record, body[choice], written++ );
            }

            foreach (var layout in layouts)
            {
                if (layout.IsPrefixed && layout.Key == "Trailer")
                {
                    WriteRecord( writer, record, layout, written++ );
                }
            }
            writer.Flush();
            watch.Stop();

            var size = profile.RecordLength + profile.RecordSeparator.Length;
            return new GenerationResult
            {
                Path = path,
                Records = (int) written,
                FileSize = new FileInfo( path ).Length,
                RecordBytesMinimum = size,
                RecordBytesMaximum = size,
                RecordBytesAverage = size,
                Elapsed = watch.Elapsed
            };
        }

        /// <summary>
        ///     The layout for the next record, taken in proportion to how many of each are still owed, so the file
        ///     ends with exactly the counts the profile asked for however the draws fall.
        /// </summary>
        private static int Choose( RecordLayout[] body, long[] remaining, double position, long left )
        {
            var running = 0.0;
            for (var index = 0; index != body.Length; ++index)
            {
                running += (double) remaining[index] / left;
                if (position < running)
                {
                    return index;
                }
            }
            for (var index = body.Length - 1; index >= 0; --index)
            {
                if (remaining[index] > 0)
                {
                    return index;
                }
            }
            return 0;
        }

        private static void WriteRecord( StreamWriter writer, char[] record, RecordLayout layout, long number )
        {
            layout.Write( record, number );
            writer.Write( record );
            writer.Write( "\r\n" );
        }
    }

    /// <summary>
    ///     One record layout: a column writer per window, and whatever has to be true of the record for a reader
    ///     to recognise it.
    /// </summary>
    internal sealed class RecordLayout
    {
        private readonly ColumnWriter[] columns;
        private readonly int[] windows;
        private readonly char[] scratch;
        private readonly string prefix;
        private readonly int position;
        private readonly char key;

        public RecordLayout( RecordTypeProfile profile, int seed )
        {
            Key = profile.Key;
            RecordCount = profile.RecordCount;
            IsPrefixed = profile.IsPrefixed;
            prefix = profile.Prefix;
            position = profile.Position;
            key = profile.Key.Length == 1 ? profile.Key[0] : ' ';
            windows = new int[profile.Columns.Count];
            columns = new ColumnWriter[profile.Columns.Count];
            var widest = 1;
            foreach (var column in profile.Columns)
            {
                widest = Math.Max( widest, column.Window );
            }
            scratch = new char[widest];
            for (var index = 0; index != columns.Length; ++index)
            {
                windows[index] = profile.Columns[index].Window;
                columns[index] = new ColumnWriter( profile.Columns[index], seed * 97 + index + 1 );
            }
        }

        public string Key { get; }

        public int RecordCount { get; }

        public bool IsPrefixed { get; }

        public void Write( Span<char> record, long number )
        {
            record.Fill( ' ' );
            var offset = 0;
            for (var index = 0; index != columns.Length; ++index)
            {
                if (offset >= record.Length)
                {
                    break;
                }
                var field = record.Slice( offset, Math.Min( windows[index], record.Length - offset ) );
                offset += windows[index];
                var width = Math.Min( columns[index].Write( scratch, number ), field.Length );
                scratch.AsSpan( 0, width ).CopyTo( field );
                field[width..].Fill( ' ' );
            }
            Mark( record );
        }

        /// <summary>
        ///     Writes whatever a reader selects on, over the top of whatever the columns put there. A record has
        ///     to say which layout it is before anything else about it can be read.
        /// </summary>
        private void Mark( Span<char> record )
        {
            if (IsPrefixed)
            {
                prefix.AsSpan( 0, Math.Min( prefix.Length, record.Length ) ).CopyTo( record );
                return;
            }
            if (position < record.Length)
            {
                record[position] = key;
            }
        }
    }
}
