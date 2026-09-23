using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     Writes a file of the shape a <see cref="FileProfile" /> describes. The content is simulated and means
    ///     nothing; the widths, the type mix and the punctuation are what a reader spends its time on, and those
    ///     are what the profile pins.
    /// </summary>
    internal static class FileGenerator
    {
        public static GenerationResult Generate( FileProfile profile, string path )
        {
            if (profile.IsFixedLength)
            {
                return FixedLengthGenerator.Generate( profile, path );
            }
            var columns = new ColumnWriter[profile.Columns.Count];
            for (var index = 0; index != columns.Length; ++index)
            {
                columns[index] = new ColumnWriter( profile.Columns[index], index + 1 );
            }
            var ragged = profile.RaggedFinalColumn is null || profile.RaggedFinalColumn.Alignment == "None"
                ? null
                : new RaggedColumnWriter( profile.RaggedFinalColumn, columns.Length );

            var field = new char[Math.Max( 256, LargestField( profile ) )];
            var record = new StringBuilder( LongestRecord( profile ) );
            var separator = profile.Separator;
            var quote = profile.QuoteEveryField;

            // Every nth record carries a separator inside a field, as the profiled file does.
            var misalignEvery = profile.MisalignedRecords == 0 ? 0 : profile.RecordCount / profile.MisalignedRecords;

            var watch = Stopwatch.StartNew();
            Directory.CreateDirectory( Path.GetDirectoryName( path )! );
            using var stream = new FileStream( path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20 );
            using var writer = new StreamWriter( stream, new UTF8Encoding( false ), 1 << 20 );

            for (var index = 0; index != profile.Columns.Count; ++index)
            {
                if (index != 0)
                {
                    writer.Write( separator );
                }
                WriteField( writer, profile.Columns[index].Name.AsSpan(), quote );
            }
            writer.Write( profile.RecordSeparator );

            long bytes = 0;
            var smallest = int.MaxValue;
            var largest = 0;
            var misaligned = 0;
            for (var number = 0L; number != profile.RecordCount; ++number)
            {
                record.Clear();
                var isMisaligned = misalignEvery != 0 && misaligned < profile.MisalignedRecords && number % misalignEvery == 0;
                // Vary where the stray separator falls, so the shift is not always detectable in the same place.
                var splitAfter = isMisaligned ? (int) ( number / Math.Max( 1, misalignEvery ) * 7 % columns.Length ) : 0;
                for (var index = 0; index != columns.Length; ++index)
                {
                    if (index != 0)
                    {
                        record.Append( separator );
                    }
                    var isLast = index == columns.Length - 1;
                    var width = isLast && ragged is not null
                        ? ragged.Write( field, number )
                        : columns[index].Write( field, number );
                    var value = field.AsSpan( 0, width );
                    if (quote)
                    {
                        record.Append( '"' ).Append( value ).Append( '"' );
                        continue;
                    }
                    record.Append( value );
                    // An unquoted separator inside a field is what makes a record misaligned; it goes in the first
                    // field at or after the chosen one that is wide enough to hold it.
                    if (isMisaligned && index >= splitAfter && width > 2)
                    {
                        record.Insert( record.Length - width / 2, separator );
                        isMisaligned = false;
                        ++misaligned;
                    }
                }
                record.Append( profile.RecordSeparator );
                writer.Write( record );

                var size = record.Length;
                bytes += size;
                if (size < smallest) { smallest = size; }
                if (size > largest) { largest = size; }
            }
            writer.Flush();
            watch.Stop();

            return new GenerationResult
            {
                Path = path,
                Records = profile.RecordCount,
                MisalignedRecords = misaligned,
                FileSize = new FileInfo( path ).Length,
                RecordBytesMinimum = smallest,
                RecordBytesMaximum = largest,
                RecordBytesAverage = profile.RecordCount == 0 ? 0 : (double) bytes / profile.RecordCount,
                Elapsed = watch.Elapsed
            };
        }

        private static void WriteField( StreamWriter writer, ReadOnlySpan<char> value, bool quote )
        {
            if (!quote)
            {
                writer.Write( value );
                return;
            }
            writer.Write( '"' );
            writer.Write( value );
            writer.Write( '"' );
        }

        private static int LargestField( FileProfile profile )
        {
            var largest = 0;
            foreach (var column in profile.Columns)
            {
                largest = Math.Max( largest, column.MaxWidth );
            }
            if (profile.RaggedFinalColumn is not null)
            {
                foreach (var pair in profile.RaggedFinalColumn.RawWidths)
                {
                    largest = Math.Max( largest, (int) pair[0] );
                }
            }
            return largest;
        }

        private static int LongestRecord( FileProfile profile )
        {
            var total = profile.RecordSeparator.Length + profile.Columns.Count * ( profile.Separator.Length + 2 );
            foreach (var column in profile.Columns)
            {
                total += column.MaxWidth;
            }
            return total;
        }
    }

    internal sealed class GenerationResult
    {
        public string Path { get; set; } = string.Empty;

        public int Records { get; set; }

        public int MisalignedRecords { get; set; }

        public long FileSize { get; set; }

        public int RecordBytesMinimum { get; set; }

        public int RecordBytesMaximum { get; set; }

        public double RecordBytesAverage { get; set; }

        public TimeSpan Elapsed { get; set; }
    }
}
