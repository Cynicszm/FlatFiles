using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     Reads every generated file back, measures it, and writes one workbook describing what is there. Its
    ///     purpose is to be compared with the profiles the files were generated from: a summary that sets the two
    ///     side by side, and a sheet per file giving every column's type mix and widths.
    /// </summary>
    internal static class ProfileReport
    {
        public static string Write( List<FileProfile> profiles, Func<FileProfile, string> pathFor, string directory )
        {
            var workbook = new Workbook();
            var summary = workbook.AddSheet( "Summary" );
            summary.Add( "Generated file profiles" );
            summary.Add( "Measured from the generated files, beside what the profile they came from asked for." );
            summary.Blank();

            List<(FileProfile Profile, FileAnalysis Analysis)> measured = [];
            foreach (var profile in profiles)
            {
                var path = pathFor( profile );
                if (!File.Exists( path ))
                {
                    continue;
                }
                measured.Add( ( profile, FileAnalyser.Analyse( profile, path ) ) );
            }

            WriteSummary( summary, measured );
            foreach (var (profile, analysis) in measured)
            {
                WriteColumns( workbook.AddSheet( profile.Name ), profile, analysis );
            }

            var target = Path.Combine( directory, "GeneratedFileProfiles.xlsx" );
            workbook.Save( target );
            return target;
        }

        private static void WriteSummary( Sheet sheet, List<(FileProfile Profile, FileAnalysis Analysis)> measured )
        {
            var names = measured.Select( x => (object?) x.Profile.Name ).ToArray();
            sheet.Add( [.. new object?[] { "Metric" }.Concat( names )] );

            Row( sheet, "Format", measured, x => x.Profile.IsFixedLength ? "FixedLength" : "Delimited" );
            Row( sheet, "Record separator", measured, _ => "CRLF" );
            Row( sheet, "Every field quoted", measured, x => x.Profile.QuoteEveryField ? "yes" : "no" );
            Row( sheet, "Record layouts", measured, x => x.Profile.IsFixedLength ? x.Profile.RecordTypes.Count : 1 );
            Row( sheet, "Columns (widest layout)", measured, x => x.Analysis.Groups.Max( g => g.Columns.Length ) );
            sheet.Blank();
            Row( sheet, "Records measured", measured, x => x.Analysis.Records );
            Row( sheet, "Records the profile asked for", measured, x => x.Profile.RecordCount );
            Row( sheet, "Blank lines", measured, x => x.Analysis.BlankLines );
            Row( sheet, "Header row bytes", measured, x => x.Analysis.HeaderBytes );
            sheet.Blank();
            Row( sheet, "File size measured", measured, x => x.Analysis.FileSize );
            Row( sheet, "File size the profile asked for", measured, x => x.Profile.Expected.FileSize );
            Row( sheet, "File size difference", measured, x => x.Analysis.FileSize - x.Profile.Expected.FileSize );
            sheet.Blank();
            Row( sheet, "Record bytes minimum", measured, x => x.Analysis.RecordBytesMinimum );
            Row( sheet, "Record bytes maximum", measured, x => x.Analysis.RecordBytesMaximum );
            Row( sheet, "Record bytes average", measured, x => Math.Round( x.Analysis.RecordBytesAverage, 4 ) );
            Row( sheet, "Record bytes average asked for", measured, x => x.Profile.IsFixedLength
                ? x.Profile.RecordLength + 2
                : x.Profile.Expected.RecordBytesAverage );
            Row( sheet, "Average out by", measured, x => AverageError( x.Profile, x.Analysis ) );
            sheet.Blank();
            Row( sheet, "Reconciliation: records + header vs file", measured,
                x => x.Analysis.RecordBytesTotal + x.Analysis.HeaderBytes - x.Analysis.FileSize );

            sheet.Blank();
            sheet.Add( "Fields per record", "Records" );
            foreach (var (profile, analysis) in measured )
            {
                if (analysis.FieldCounts.Count == 0)
                {
                    continue;
                }
                foreach (var pair in analysis.FieldCounts.OrderBy( x => x.Key ))
                {
                    sheet.Add( profile.Name, pair.Key, pair.Value );
                }
            }

            sheet.Blank();
            sheet.Add( "Notes" );
            sheet.Add( "A value's kind is decided after trimming, so a field of spaces counts as empty while still having a width." );
            sheet.Add( "Widths are the raw field for a delimited file, and the trimmed value within its window for a fixed-length one." );
            sheet.Add( "A fixed-length file reports one block of columns per record layout; the layout is named in the first column." );
        }

        private static object? AverageError( FileProfile profile, FileAnalysis analysis )
        {
            var expected = profile.IsFixedLength ? profile.RecordLength + 2 : profile.Expected.RecordBytesAverage;
            return expected == 0 ? 0 : Math.Round( analysis.RecordBytesAverage / expected - 1, 6 );
        }

        private static void Row( Sheet sheet, string label, List<(FileProfile Profile, FileAnalysis Analysis)> measured,
            Func<(FileProfile Profile, FileAnalysis Analysis), object?> value )
        {
            sheet.Add( [.. new object?[] { label }.Concat( measured.Select( value ) )] );
        }

        private static void WriteColumns( Sheet sheet, FileProfile profile, FileAnalysis analysis )
        {
            sheet.Add( profile.Name );
            sheet.Add( profile.Description );
            sheet.Blank();
            sheet.Add( "Layout", "Col", "Date", "Numeric", "String", "Empty", "Missing",
                "Date %", "Numeric %", "String %", "Empty %",
                "Min width", "Max width", "Avg width", "Mode width", "Mode width %", "Distinct widths",
                "Profile avg width", "Avg width out by" );

            foreach (var group in analysis.Groups)
            {
                var columns = profile.IsFixedLength
                    ? profile.RecordTypes.First( x => x.Key == group.Key ).Columns
                    : profile.Columns;
                for (var index = 0; index != group.Columns.Length; ++index)
                {
                    var measured = group.Columns[index];
                    var asked = index < columns.Count ? columns[index] : null;
                    var records = group.Records == 0 ? 1 : group.Records;
                    sheet.Add(
                        group.Key.Length == 0 ? "-" : group.Key,
                        measured.Index,
                        measured.Date, measured.Numeric, measured.String, measured.Empty, measured.Missing,
                        Share( measured.Date, records ), Share( measured.Numeric, records ),
                        Share( measured.String, records ), Share( measured.Empty, records ),
                        measured.Present == 0 ? 0 : measured.MinimumWidth, measured.MaximumWidth,
                        Math.Round( measured.AverageWidth, 4 ),
                        measured.ModeWidth, Share( measured.ModeCount, records ), measured.Widths.Count,
                        asked?.AverageWidth,
                        asked is null || asked.AverageWidth == 0
                            ? null
                            : Math.Round( measured.AverageWidth - asked.AverageWidth, 4 ) );
                }
            }
        }

        private static double Share( long part, long whole )
        {
            return whole == 0 ? 0 : Math.Round( 100.0 * part / whole, 4 );
        }
    }
}
