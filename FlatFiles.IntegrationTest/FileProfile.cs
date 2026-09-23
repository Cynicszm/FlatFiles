using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     The shape of one file: how many records it holds, how they are separated, and one
    ///     <see cref="ColumnProfile" /> for every column. A profile carries no data from the file it was taken
    ///     from - only the counts and widths needed to simulate one of the same shape.
    /// </summary>
    internal sealed class FileProfile
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        /// <summary>
        ///     Delimited or FixedLength. A delimited profile describes its columns once; a fixed-length one
        ///     describes a set of record types, because one file holds records of several layouts and a character
        ///     in the record says which.
        /// </summary>
        public string Format { get; set; } = "Delimited";

        public bool IsFixedLength => Format == "FixedLength";

        /// <summary>
        ///     The width every record is written to, for a fixed-length file.
        /// </summary>
        public int RecordLength { get; set; }

        public string Separator { get; set; } = ",";

        public string RecordSeparator { get; set; } = "\r\n";

        /// <summary>
        ///     Whether every field is written inside quotes, as some systems emit regardless of content.
        /// </summary>
        public bool QuoteEveryField { get; set; }

        public int RecordCount { get; set; }

        /// <summary>
        ///     Records carrying a separator inside an unquoted field, which give the reader more fields than the
        ///     header declares. The generator spreads this many of them through the file.
        /// </summary>
        public int MisalignedRecords { get; set; }

        public ExpectedSize Expected { get; set; } = new();

        /// <summary>
        ///     The final column where it is ragged - present on every record but varying in width, and padded on
        ///     one side rather than holding a value of its own length. Null where the last column is ordinary.
        /// </summary>
        public RaggedColumnProfile? RaggedFinalColumn { get; set; }

        public List<ColumnProfile> Columns { get; set; } = [];

        /// <summary>
        ///     One per record layout the file holds, for a fixed-length file. Empty for a delimited one.
        /// </summary>
        public List<RecordTypeProfile> RecordTypes { get; set; } = [];

        /// <summary>
        ///     Records the profiler could not place against any record type. Zero on all three fixed-length
        ///     samples, and worth reporting because a file where it is not zero has a layout nobody has mapped.
        /// </summary>
        public int UnmatchedRecords { get; set; }

        /// <summary>
        ///     Every column in the file, whichever shape it takes, for the places that only need to count them.
        /// </summary>
        public IEnumerable<ColumnProfile> AllColumns => IsFixedLength
            ? RecordTypes.SelectMany( x => x.Columns )
            : Columns;

        public static FileProfile Load( string path )
        {
            using var stream = File.OpenRead( path );
            return JsonSerializer.Deserialize( stream, ProfileJson.Default.FileProfile )!;
        }
    }

    /// <summary>
    ///     What the file the profile was taken from measured, for the generated file to be checked against.
    /// </summary>
    internal sealed class ExpectedSize
    {
        public long FileSize { get; set; }

        public int RecordBytesMinimum { get; set; }

        public int RecordBytesMaximum { get; set; }

        public double RecordBytesAverage { get; set; }
    }

    /// <summary>
    ///     One record layout within a fixed-length file, and how a reader is to recognise it: either the record
    ///     starts with <see cref="Prefix" />, or the character at <see cref="Position" /> is the first of
    ///     <see cref="Key" />.
    /// </summary>
    internal sealed class RecordTypeProfile
    {
        public string Key { get; set; } = string.Empty;

        public string Match { get; set; } = "character";

        public int Position { get; set; }

        public string Prefix { get; set; } = string.Empty;

        public int RecordCount { get; set; }

        /// <summary>
        ///     What the windows come to. Less than the record length on some layouts, where the mapping stops
        ///     short and the rest of the record is not read at all.
        /// </summary>
        public int WindowTotal { get; set; }

        public List<ColumnProfile> Columns { get; set; } = [];

        public bool IsPrefixed => Match == "prefix";
    }

    /// <summary>
    ///     A column's type mix and width distribution. The four counts are of records, and the widths are of the
    ///     raw field, so a field holding only spaces counts as empty while still having a width.
    /// </summary>
    internal sealed class ColumnProfile
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>
        ///     The fixed width the column occupies, for a fixed-length file. The width statistics below are of the
        ///     value inside it once padding is taken off, so they never exceed this.
        /// </summary>
        public int Window { get; set; }

        public int Date { get; set; }

        public int Numeric { get; set; }

        public int String { get; set; }

        public int Empty { get; set; }

        public int MinWidth { get; set; }

        public int MaxWidth { get; set; }

        public double AverageWidth { get; set; }

        public int ModeWidth { get; set; }

        public double ModeShare { get; set; }

        public int DistinctWidths { get; set; }

        public int Total => Date + Numeric + String + Empty;
    }

    /// <summary>
    ///     The width and padding of a ragged final column, as two distributions of [value, share] pairs.
    /// </summary>
    internal sealed class RaggedColumnProfile
    {
        public List<double[]> RawWidths { get; set; } = [];

        public List<double[]> TrimmedLengths { get; set; } = [];

        /// <summary>
        ///     Which side the padding sits on: Right for a value pushed to the right of its width, None where the
        ///     column is empty on every record.
        /// </summary>
        public string Alignment { get; set; } = "None";
    }

    [JsonSourceGenerationOptions( PropertyNameCaseInsensitive = true )]
    [JsonSerializable( typeof( FileProfile ) )]
    internal sealed partial class ProfileJson : JsonSerializerContext
    {
    }
}
