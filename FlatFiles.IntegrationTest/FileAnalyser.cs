using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     Reads a generated file back and measures it, so the file can be compared against the profile it was
    ///     generated from. The definitions match the ones the profiles were taken with: a value's kind is decided
    ///     after trimming, and its width is the raw field for a delimited file or the trimmed value within its
    ///     window for a fixed-length one.
    /// </summary>
    internal static class FileAnalyser
    {
        private static readonly string[] DateFormats =
        [
            "yyMMdd", "yyyyMMdd", "yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy", "dd/MM/yyyy",
            "yyyyMMddHHmmss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.fff"
        ];

        public static FileAnalysis Analyse( FileProfile profile, string path )
        {
            var analysis = new FileAnalysis { Profile = profile.Name, Path = path };
            var groups = new Dictionary<string, ColumnGroup>( StringComparer.Ordinal );
            if (profile.IsFixedLength)
            {
                foreach (var type in profile.RecordTypes)
                {
                    groups[type.Key] = new ColumnGroup( type.Key, type.Columns.Count );
                }
            }
            else
            {
                groups[string.Empty] = new ColumnGroup( string.Empty, profile.Columns.Count );
            }

            using var stream = new FileStream( path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20 );
            using var reader = new StreamReader( stream );
            if (!profile.IsFixedLength)
            {
                analysis.HeaderBytes = ( reader.ReadLine()?.Length ?? 0 ) + profile.RecordSeparator.Length;
            }

            string? line;
            while (( line = reader.ReadLine() ) is not null)
            {
                if (line.Length == 0)
                {
                    ++analysis.BlankLines;
                    continue;
                }
                ++analysis.Records;
                var bytes = line.Length + profile.RecordSeparator.Length;
                analysis.RecordBytesTotal += bytes;
                analysis.RecordBytesMinimum = Math.Min( analysis.RecordBytesMinimum, bytes );
                analysis.RecordBytesMaximum = Math.Max( analysis.RecordBytesMaximum, bytes );

                if (profile.IsFixedLength)
                {
                    AnalyseFixedLength( profile, groups, line );
                    continue;
                }
                AnalyseDelimited( profile, groups[string.Empty], line, analysis );
            }

            analysis.Groups = [.. groups.Values];
            analysis.FileSize = new FileInfo( path ).Length;
            return analysis;
        }

        private static void AnalyseFixedLength( FileProfile profile, Dictionary<string, ColumnGroup> groups, string line )
        {
            var type = Match( profile, line );
            if (type is null)
            {
                return;
            }
            var group = groups[type.Key];
            ++group.Records;
            var offset = 0;
            for (var index = 0; index != type.Columns.Count; ++index)
            {
                var window = type.Columns[index].Window;
                var length = Math.Max( 0, Math.Min( window, line.Length - offset ) );
                var raw = offset >= line.Length ? string.Empty : line.Substring( offset, length );
                offset += window;
                group.Columns[index].Add( raw, raw.Trim().Length );
            }
        }

        private static RecordTypeProfile? Match( FileProfile profile, string line )
        {
            foreach (var type in profile.RecordTypes)
            {
                if (type.IsPrefixed)
                {
                    if (line.StartsWith( type.Prefix, StringComparison.Ordinal ))
                    {
                        return type;
                    }
                    continue;
                }
                if (line.Length > type.Position && line[type.Position] == type.Key[0])
                {
                    return type;
                }
            }
            return null;
        }

        private static void AnalyseDelimited( FileProfile profile, ColumnGroup group, string line, FileAnalysis analysis )
        {
            ++group.Records;
            var fields = Split( line, profile.Separator[0], profile.QuoteEveryField );
            analysis.FieldCounts[fields.Count] = analysis.FieldCounts.GetValueOrDefault( fields.Count ) + 1;
            for (var index = 0; index != group.Columns.Length; ++index)
            {
                if (index >= fields.Count)
                {
                    ++group.Columns[index].Missing;
                    continue;
                }
                group.Columns[index].Add( fields[index], fields[index].Length );
            }
        }

        /// <summary>
        ///     Splits a record, honouring quotes and the doubled quote inside them. A record with no quote at all
        ///     takes the quick path, which is every record in two of the three delimited files.
        /// </summary>
        private static List<string> Split( string line, char separator, bool quoted )
        {
            if (!quoted && line.IndexOf( '"' ) < 0)
            {
                return [.. line.Split( separator )];
            }
            List<string> fields = [];
            var builder = new System.Text.StringBuilder();
            var inQuotes = false;
            for (var index = 0; index != line.Length; ++index)
            {
                var character = line[index];
                if (inQuotes)
                {
                    if (character != '"')
                    {
                        builder.Append( character );
                        continue;
                    }
                    if (index + 1 < line.Length && line[index + 1] == '"')
                    {
                        builder.Append( '"' );
                        ++index;
                        continue;
                    }
                    inQuotes = false;
                    continue;
                }
                if (character == '"')
                {
                    inQuotes = true;
                    continue;
                }
                if (character == separator)
                {
                    fields.Add( builder.ToString() );
                    builder.Clear();
                    continue;
                }
                builder.Append( character );
            }
            fields.Add( builder.ToString() );
            return fields;
        }

        public static string KindOf( string value )
        {
            var text = value.Trim();
            if (text.Length == 0)
            {
                return "Empty";
            }
            if (text.Length is >= 6 and <= 23
                && DateTime.TryParseExact( text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment )
                && moment.Year is >= 1900 and <= 2100)
            {
                return "Date";
            }
            return decimal.TryParse( text, NumberStyles.Number, CultureInfo.InvariantCulture, out _ ) ? "Numeric" : "String";
        }
    }

    internal sealed class FileAnalysis
    {
        public string Profile { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public long Records { get; set; }

        public long BlankLines { get; set; }

        public int HeaderBytes { get; set; }

        public long RecordBytesTotal { get; set; }

        public int RecordBytesMinimum { get; set; } = int.MaxValue;

        public int RecordBytesMaximum { get; set; }

        public double RecordBytesAverage => Records == 0 ? 0 : (double) RecordBytesTotal / Records;

        public Dictionary<int, long> FieldCounts { get; } = [];

        public List<ColumnGroup> Groups { get; set; } = [];
    }

    /// <summary>
    ///     The columns of one record layout, or of the whole file where it has only one.
    /// </summary>
    internal sealed class ColumnGroup
    {
        public ColumnGroup( string key, int columns )
        {
            Key = key;
            Columns = new ColumnStatistics[columns];
            for (var index = 0; index != columns; ++index)
            {
                Columns[index] = new ColumnStatistics { Index = index + 1 };
            }
        }

        public string Key { get; }

        public long Records { get; set; }

        public ColumnStatistics[] Columns { get; }
    }

    internal sealed class ColumnStatistics
    {
        public int Index { get; set; }

        public long Date { get; set; }

        public long Numeric { get; set; }

        public long String { get; set; }

        public long Empty { get; set; }

        public long Missing { get; set; }

        public int MinimumWidth { get; set; } = int.MaxValue;

        public int MaximumWidth { get; set; }

        public long TotalWidth { get; set; }

        public Dictionary<int, long> Widths { get; } = [];

        public void Add( string value, int width )
        {
            switch (FileAnalyser.KindOf( value ))
            {
                case "Date": ++Date; break;
                case "Numeric": ++Numeric; break;
                case "Empty": ++Empty; break;
                default: ++String; break;
            }
            MinimumWidth = Math.Min( MinimumWidth, width );
            MaximumWidth = Math.Max( MaximumWidth, width );
            TotalWidth += width;
            Widths[width] = Widths.GetValueOrDefault( width ) + 1;
        }

        public long Present => Date + Numeric + String + Empty;

        public double AverageWidth => Present == 0 ? 0 : (double) TotalWidth / Present;

        public int ModeWidth
        {
            get
            {
                var mode = 0;
                var best = -1L;
                foreach (var pair in Widths)
                {
                    if (pair.Value > best)
                    {
                        mode = pair.Key;
                        best = pair.Value;
                    }
                }
                return mode;
            }
        }

        public long ModeCount
        {
            get
            {
                var best = 0L;
                foreach (var pair in Widths)
                {
                    best = Math.Max( best, pair.Value );
                }
                return best;
            }
        }
    }
}
