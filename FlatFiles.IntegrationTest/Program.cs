using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FlatFiles.IntegrationTest
{
    internal static class Program
    {
        private static readonly string[] Scenarios = ["parse", "typed", "values"];

        private static int Main( string[] args )
        {
            var command = args.Length == 0 ? "run" : args[0].ToLowerInvariant();
            try
            {
                return Dispatch( command, args );
            }
            catch (Exception exception) when (exception is IOException or ArgumentException)
            {
                // A missing sample or an unknown profile is something to say in a sentence, not to print a
                // stack trace over: both mean somebody has to do something, and the message says what.
                Console.Error.WriteLine( exception.Message );
                return 1;
            }
        }

        private static int Dispatch( string command, string[] args )
        {
            return command switch
            {
                "generate" => Generate( args.Skip( 1 ).ToArray() ),
                "measure" => Measure( args.Skip( 1 ).ToArray() ),
                "run" => Run( args.Skip( 1 ).ToArray() ),
                "check" => Check( args.Skip( 1 ).ToArray() ),
                "profile" => WriteProfileReport( args.Skip( 1 ).ToArray() ),
                "baseline" => TakeBaseline( args.Skip( 1 ).ToArray() ),
                _ => Usage()
            };
        }

        private static int Usage()
        {
            Console.WriteLine( "FlatFiles stress test" );
            Console.WriteLine();
            Console.WriteLine( "  generate [profile...]   rebuild the samples from the profiles and pack them for committing." );
            Console.WriteLine( "                          The only thing that writes a sample; invalidates the baseline." );
            Console.WriteLine( "  run      [profile...]   measure every scenario against the committed samples." );
            Console.WriteLine( "                          Add --markdown for the table a release entry carries." );
            Console.WriteLine( "  check    [profile...]   measure, compare against the baseline, and fail if anything moved" );
            Console.WriteLine( "  profile  [profile...]   read the generated files back and write a workbook describing them" );
            Console.WriteLine( "  baseline [profile...]   measure and write the baseline, replacing what is there" );
            Console.WriteLine( "  measure  <profile> <scenario>" );
            Console.WriteLine( "                          measure one scenario in this process and print one line" );
            Console.WriteLine();
            Console.WriteLine( "Scenarios: " + string.Join( ", ", Scenarios ) );
            return 1;
        }

        private static string ProfileDirectory => Path.Combine( AppContext.BaseDirectory, "Profiles" );

        private static string FileDirectory => Path.Combine( AppContext.BaseDirectory, "Files" );

        private static List<FileProfile> Load( string[] names )
        {
            var paths = Directory.GetFiles( ProfileDirectory, "*.json" ).OrderBy( x => x );
            var profiles = paths.Select( FileProfile.Load ).ToList();
            if (names.Length == 0)
            {
                return profiles;
            }
            List<FileProfile> chosen = [.. profiles.Where( x => names.Contains( x.Name, StringComparer.OrdinalIgnoreCase ) )];
            if (chosen.Count == 0)
            {
                throw new ArgumentException( "No profile matched: " + string.Join( ", ", names ) );
            }
            return chosen;
        }

        /// <summary>
        ///     Where a sample is read from: unpacked into the build output from the committed copy. Nothing on
        ///     this path generates a sample.
        /// </summary>
        private static string PathFor( FileProfile profile )
        {
            return FileStore.Restore( PackedPathFor( profile ), WorkingPathFor( profile ) );
        }

        private static string FileNameFor( FileProfile profile )
        {
            return profile.Name + ( profile.IsFixedLength ? ".txt" : ".csv" );
        }

        private static string WorkingPathFor( FileProfile profile )
        {
            return Path.Combine( FileDirectory, FileNameFor( profile ) );
        }

        /// <summary>
        ///     The committed sample, beside the profiles rather than in the build output.
        /// </summary>
        private static string PackedPathFor( FileProfile profile )
        {
            return Path.Combine( SourceDirectory(), "Files", FileNameFor( profile ) + FileStore.Extension );
        }

        /// <summary>
        ///     Columns in the file: a fixed-length file has a set per record layout, so the widest is the one
        ///     worth reporting beside the count of layouts.
        /// </summary>
        private static int ColumnCount( FileProfile profile )
        {
            if (!profile.IsFixedLength)
            {
                return profile.Columns.Count;
            }
            var widest = 0;
            foreach (var type in profile.RecordTypes)
            {
                widest = Math.Max( widest, type.Columns.Count );
            }
            return widest;
        }

        // ------------------------------------------------------------------ generate

        /// <summary>
        ///     Rebuilds the samples from the profiles and packs them for committing. This is the only thing that
        ///     writes a sample, and it is never reached except by asking for it: the samples are the input every
        ///     figure is gated against, so replacing them invalidates the baseline and has to be deliberate.
        /// </summary>
        private static int Generate( string[] names )
        {
            var profiles = Load( names );
            foreach (var profile in profiles)
            {
                Write( profile );
            }
            Console.WriteLine();
            Console.WriteLine( "{0} sample{1} rebuilt and packed. The baseline no longer matches them:", profiles.Count, profiles.Count == 1 ? "" : "s" );
            Console.WriteLine( "  take a new one with the baseline command, and say what moved in the changelog." );
            return 0;
        }

        private static void Write( FileProfile profile )
        {
            var path = WorkingPathFor( profile );
            Console.WriteLine( "{0}: {1} columns, {2:N0} records", profile.Name, ColumnCount( profile ), profile.RecordCount );
            var result = FileGenerator.Generate( profile, path );
            var expected = profile.Expected;
            Console.WriteLine(
                "  {0:N0} bytes in {1:N1}s   record bytes min {2:N0} / avg {3:N1} / max {4:N0}",
                result.FileSize, result.Elapsed.TotalSeconds,
                result.RecordBytesMinimum, result.RecordBytesAverage, result.RecordBytesMaximum );
            if (profile.IsFixedLength)
            {
                // Every record is the same width, so the file size is the whole of the comparison.
                Console.WriteLine( "  profile said       {0:N0} bytes   {1}",
                    expected.FileSize,
                    result.FileSize == expected.FileSize
                        ? "the same to the byte"
                        : string.Format( CultureInfo.CurrentCulture, "{0:+#,##0;-#,##0} out", result.FileSize - expected.FileSize ) );
            }
            else
            {
                Console.WriteLine(
                    "  profile said       min {0:N0} / avg {1:N1} / max {2:N0}   average is {3:+0.00%;-0.00%;0.00%} out",
                    expected.RecordBytesMinimum, expected.RecordBytesAverage, expected.RecordBytesMaximum,
                    expected.RecordBytesAverage == 0 ? 0 : result.RecordBytesAverage / expected.RecordBytesAverage - 1 );
            }
            if (profile.MisalignedRecords != 0)
            {
                Console.WriteLine( "  {0:N0} records carry a separator inside a field", result.MisalignedRecords );
            }
            var packed = FileStore.Pack( path, PackedPathFor( profile ) );
            Console.WriteLine( "  packed to {0:N0} bytes ({1:P0} of the file) for committing", packed, (double) packed / result.FileSize );
        }

        // ------------------------------------------------------------------ measure

        private static int Measure( string[] args )
        {
            if (args.Length != 2)
            {
                return Usage();
            }
            var profile = Load( [args[0]] )[0];
            var result = IntegrationRun.Execute( profile, PathFor( profile ), args[1] );
            Console.WriteLine( string.Join( "|",
                result.Profile,
                result.Scenario,
                result.Records.ToString( CultureInfo.InvariantCulture ),
                result.SkippedRecords.ToString( CultureInfo.InvariantCulture ),
                result.FileSize.ToString( CultureInfo.InvariantCulture ),
                result.Elapsed.TotalMilliseconds.ToString( "F1", CultureInfo.InvariantCulture ),
                result.AllocatedBytes.ToString( CultureInfo.InvariantCulture ),
                result.PeakManagedBytes.ToString( CultureInfo.InvariantCulture ),
                result.PeakWorkingSetBytes.ToString( CultureInfo.InvariantCulture ) ) );
            return 0;
        }

        // ------------------------------------------------------------------ run

        private static string BaselinePath => Path.Combine( AppContext.BaseDirectory, Baseline.FileName );

        /// <summary>
        ///     Reads the generated files back and writes what is in them, for comparing against the profiles they
        ///     came from.
        /// </summary>
        private static int WriteProfileReport( string[] names )
        {
            var profiles = Load( names );
            var directory = Path.Combine( SourceDirectory(), "Profiles", "Generated" );
            Console.WriteLine( "Reading {0} files back...", profiles.Count );
            var path = ProfileReport.Write( profiles, PathFor, directory );
            Console.WriteLine( "Written to {0}", path );
            return 0;
        }

        /// <summary>
        ///     Measures everything and compares it against the committed baseline, answering non-zero when a
        ///     figure has moved. This is what runs before a release.
        /// </summary>
        private static int Check( string[] names )
        {
            if (!File.Exists( BaselinePath ))
            {
                Console.Error.WriteLine( "There is no baseline at {0}. Take one with the baseline command.", BaselinePath );
                return 1;
            }
            var profiles = Load( names );
            var baseline = Baseline.Load( BaselinePath );
            // What is about to be read has to be what the baseline was taken from, or the comparison is empty.
            List<(string, string)> samples = [.. profiles.Select( x => ( FileNameFor( x ), PathFor( x ) ) )];
            if (!BaselineCheck.VerifySamples( baseline, samples ))
            {
                return 1;
            }
            var results = MeasureAll( profiles );
            return BaselineCheck.Compare( baseline, results ) ? 0 : 1;
        }

        /// <summary>
        ///     Records what these files cost now, as the figures every later run is checked against.
        /// </summary>
        private static int TakeBaseline( string[] names )
        {
            var profiles = Load( names );
            var results = MeasureAll( profiles );
            var baseline = new Baseline
            {
                Files = [.. profiles.Select( x => new SampleFile
                {
                    Name = FileNameFor( x ),
                    Bytes = new FileInfo( PathFor( x ) ).Length,
                    Sha256 = FileStore.Hash( PathFor( x ) )
                } )],
                Note = "Taken by the baseline command. The record count is exact and allocation is gated on the "
                     + "tolerance below; how long a read takes is not gated. No sample is meant to have a record "
                     + "refused, so any refusal fails the check whatever these figures say.",
                Measurements = [.. results.Select( x => new Measurement
                {
                    Profile = x.Profile,
                    Scenario = x.Scenario,
                    Records = x.Records,
                    BytesPerRecord = Math.Round( x.BytesPerRecord, 1 )
                } )]
            };
            // Written beside the executable, and into the project so it can be committed.
            baseline.Save( BaselinePath );
            var source = Path.Combine( SourceDirectory(), Baseline.FileName );
            if (source != BaselinePath)
            {
                baseline.Save( source );
            }
            Console.WriteLine( "Baseline written with {0} measurements:", baseline.Measurements.Count );
            Console.WriteLine( "  {0}", source );
            return 0;
        }

        /// <summary>
        ///     The project directory, found by walking up from the executable until the project file turns up, so
        ///     that a new baseline lands where it can be committed rather than only in the build output.
        /// </summary>
        private static string SourceDirectory()
        {
            for (var directory = new DirectoryInfo( AppContext.BaseDirectory ); directory is not null; directory = directory.Parent)
            {
                if (directory.GetFiles( "*.csproj" ).Length != 0)
                {
                    return directory.FullName;
                }
            }
            return AppContext.BaseDirectory;
        }

        private static List<RunResult> MeasureAll( List<FileProfile> profiles )
        {
            List<RunResult> results = [];
            foreach (var profile in profiles)
            {
                foreach (var scenario in Scenarios)
                {
                    // Each measurement gets a process of its own, because peak working set only counts up: read
                    // two files in one process and the second inherits whatever the first reached.
                    var measured = MeasureElsewhere( profile.Name, scenario );
                    if (measured is not null)
                    {
                        results.Add( measured );
                    }
                }
            }
            return results;
        }

        private static int Run( string[] names )
        {
            // A release entry carries these figures, so the run can hand them over ready to paste rather than
            // leaving eighteen rows to be copied by eye.
            var asMarkdown = names.Contains( "--markdown", StringComparer.OrdinalIgnoreCase );
            var profiles = Load( [.. names.Where( x => !x.StartsWith( "--", StringComparison.Ordinal ) )] );
            var results = MeasureAll( profiles );
            if (asMarkdown)
            {
                ReportAsMarkdown( profiles, results );
                return 0;
            }
            Report( profiles, results );
            return 0;
        }

        /// <summary>
        ///     The same figures as Markdown, for the changelog entry a release carries: one table per format,
        ///     every row carrying its sample so no row has to be read against one above it, and the text saying
        ///     what each column is a measurement of.
        /// </summary>
        private static void ReportAsMarkdown( List<FileProfile> profiles, List<RunResult> results )
        {
            WriteMarkdownTable( "Delimited", profiles, results, fixedLength: false );
            WriteMarkdownTable( "Fixed-length", profiles, results, fixedLength: true );
            WriteMarkdownNotes( results );
        }

        private static void WriteMarkdownTable( string heading, List<FileProfile> profiles, List<RunResult> results, bool fixedLength )
        {
            Console.WriteLine();
            Console.WriteLine( "**{0}**", heading );
            Console.WriteLine();
            Console.WriteLine( "| Sample | Format | Columns | Records | Scenario | Total Time | MB/s | Bytes/record | Peak heap | Peak working set |" );
            Console.WriteLine( "| --- | --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: |" );

            var previous = string.Empty;
            foreach (var result in results)
            {
                var profile = profiles.Find( x => x.Name == result.Profile )!;
                if (profile.IsFixedLength != fixedLength)
                {
                    continue;
                }
                // A rule between one sample and the next, so three scenarios read as a group.
                if (previous.Length != 0 && result.Profile != previous)
                {
                    Console.WriteLine( "| | | | | | | | | | |" );
                }
                previous = result.Profile;
                Console.WriteLine( "| {0} | {1} | {2:N0} | {3:N0} | `{4}` | {5} | {6:N1} | {7:N0} | {8} | {9} |",
                    Shorthand( result.Profile ),
                    profile.IsFixedLength ? "fixed-length" : "delimited",
                    ColumnCount( profile ),
                    result.Records,
                    result.Scenario,
                    Duration( result.Elapsed ), result.MegabytesPerSecond, result.BytesPerRecord,
                    Megabytes( result.PeakManagedBytes ), Megabytes( result.PeakWorkingSetBytes ) );
            }
        }

        /// <summary>
        ///     What the columns are measurements of. Worth writing out beside the figures rather than leaving to
        ///     a reader to assume, because two of them mean less than they look like they do.
        /// </summary>
        private static void WriteMarkdownNotes( List<RunResult> results )
        {
            Console.WriteLine();
            Console.WriteLine( "Each row is one complete read of one sample in a process of its own. The scenarios are cumulative:" );
            Console.WriteLine( "`parse` reads every column as text and asks for no value, `typed` gives each single-typed column its" );
            Console.WriteLine( "own type, and `values` is `typed` with `GetValues` called on every record. The difference between two" );
            Console.WriteLine( "of them is the cost of the step between." );
            Console.WriteLine();
            Console.WriteLine( "| Column | What it measures |" );
            Console.WriteLine( "| --- | --- |" );
            Console.WriteLine( "| Columns | Columns in the schema; for a fixed-length sample, in its widest record layout. |" );
            Console.WriteLine( "| Records | Records the reader yielded. Gated exactly. No sample is built to have a record refused, so any refusal fails the check whatever else agrees. |" );
            Console.WriteLine( "| Total Time | Wall clock for the whole read: opening the file, building the schema, constructing the reader, reading every record, and disposing. Measured once with no warm-up, so it includes first-call JIT. **Reported, never gated.** |" );
            Console.WriteLine( "| MB/s | File size divided by Total Time, so it carries the same caveats. |" );
            Console.WriteLine( "| Bytes/record | Bytes allocated across that read, divided by records read. Deterministic for given bytes on a given runtime, and repeats to within 0.1% here. **Gated at 2%.** |" );
            Console.WriteLine( "| Peak heap | The largest the managed heap reached during the read, sampled every 5 ms. Reported. |" );
            Console.WriteLine( "| Peak working set | The process's peak working set, which is why each row gets its own process. Dominated by runtime start-up rather than by the read. Reported. |" );
            Console.WriteLine();
            Console.WriteLine( "Total Time and MB/s are single un-warmed measurements and move 10-20% between runs; `FlatFiles.Benchmark`" );
            Console.WriteLine( "is the project that measures time properly. They are here to show the shape of the work, not to be compared" );
            Console.WriteLine( "release to release." );

            List<string> refused = [.. results.Where( x => x.SkippedRecords != 0 )
                .Select( x => string.Format( CultureInfo.CurrentCulture, "{0} `{1}` refused {2:N0}", Shorthand( x.Profile ), x.Scenario, x.SkippedRecords ) )];
            if (refused.Count != 0)
            {
                Console.WriteLine();
                Console.WriteLine( "Records refused: " + string.Join( "; ", refused ) + "." );
            }
        }

        private static RunResult? MeasureElsewhere( string name, string scenario )
        {
            var start = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath!,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            if (!Environment.ProcessPath!.EndsWith( ".exe", StringComparison.OrdinalIgnoreCase ))
            {
                start.ArgumentList.Add( Environment.GetCommandLineArgs()[0] );
            }
            start.ArgumentList.Add( "measure" );
            start.ArgumentList.Add( name );
            start.ArgumentList.Add( scenario );

            using var process = Process.Start( start )!;
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            var parts = output.Split( '|' );
            if (process.ExitCode != 0 || parts.Length != 9)
            {
                Console.Error.WriteLine( "{0} {1} did not measure: {2}", name, scenario, output );
                return null;
            }
            return new RunResult
            {
                Profile = parts[0],
                Scenario = parts[1],
                Records = long.Parse( parts[2], CultureInfo.InvariantCulture ),
                SkippedRecords = long.Parse( parts[3], CultureInfo.InvariantCulture ),
                FileSize = long.Parse( parts[4], CultureInfo.InvariantCulture ),
                Elapsed = TimeSpan.FromMilliseconds( double.Parse( parts[5], CultureInfo.InvariantCulture ) ),
                AllocatedBytes = long.Parse( parts[6], CultureInfo.InvariantCulture ),
                PeakManagedBytes = long.Parse( parts[7], CultureInfo.InvariantCulture ),
                PeakWorkingSetBytes = long.Parse( parts[8], CultureInfo.InvariantCulture )
            };
        }

        private static void Report( List<FileProfile> profiles, List<RunResult> results )
        {
            Console.WriteLine();
            Console.WriteLine( new string( '=', 118 ) );
            Console.WriteLine( "FILES" );
            Console.WriteLine( new string( '=', 118 ) );
            Console.WriteLine( "{0,-14}{1,8}{2,10}{3,12}{4,9}{5,8}   {6}", "Profile", "Columns", "Typed", "Records", "Layouts", "MB", "Description" );
            foreach (var profile in profiles)
            {
                var size = File.Exists( PathFor( profile ) ) ? new FileInfo( PathFor( profile ) ).Length : 0;
                Console.WriteLine( "{0,-14}{1,8}{2,10:N0}{3,12:N0}{4,9}{5,8:N1}   {6}",
                    profile.Name, Shape( profile ), SchemaFactory.TypedColumnCount( profile ),
                    profile.RecordCount, Layouts( profile ), size / 1024.0 / 1024.0,
                    profile.Description );
            }

            Console.WriteLine();
            Console.WriteLine( new string( '=', 118 ) );
            Console.WriteLine( "MEASUREMENTS" );
            Console.WriteLine( new string( '=', 118 ) );
            Console.WriteLine( "{0,-14}{1,-8}{2,11}{3,9}{4,12}{5,11}{6,12}{7,12}{8,12}",
                "Profile", "Scenario", "Records", "Refused", "Total time", "MB/s", "Bytes/rec", "Peak heap", "Peak WS" );
            var profileName = string.Empty;
            foreach (var result in results)
            {
                if (result.Profile != profileName)
                {
                    profileName = result.Profile;
                }
                Console.WriteLine( "{0,-14}{1,-8}{2,11:N0}{3,9:N0}{4,12}{5,11:N1}{6,12:N0}{7,12}{8,12}",
                    result.Profile, result.Scenario, result.Records, result.SkippedRecords,
                    Duration( result.Elapsed ), result.MegabytesPerSecond, result.BytesPerRecord,
                    Megabytes( result.PeakManagedBytes ), Megabytes( result.PeakWorkingSetBytes ) );
            }
            Console.WriteLine();
            Console.WriteLine( "parse  - every column read as text, no value asked for" );
            Console.WriteLine( "typed  - each single-typed column given its own type, no value asked for" );
            Console.WriteLine( "values - typed, and GetValues called for every record" );
            Console.WriteLine();
            Console.WriteLine( "Total time is the whole read - opening the file, building the schema, reading every record -" );
            Console.WriteLine( "measured once with no warm-up, so it includes first-call JIT and is not comparable between runs." );
            Console.WriteLine( "Bytes/rec is what the read allocated, per record, and is the figure the release gate compares." );
            Console.WriteLine( "Peak heap is the largest the managed heap reached while reading; peak WS is the process working" );
            Console.WriteLine( "set, which is why each row gets its own process." );
        }

        /// <summary>
        ///     A sample's name in a narrower form for a table: Set1Sample1 reads as S1/S1.
        /// </summary>
        private static string Shorthand( string profile )
        {
            var match = Regex.Match( profile, @"^Set(\d+)Sample(\d+)$" );
            return match.Success
                ? string.Format( CultureInfo.CurrentCulture, "S{0}/S{1}", match.Groups[1].Value, match.Groups[2].Value )
                : profile;
        }

        /// <summary>
        ///     The widest column count, and for a fixed-length file how many layouts that is the widest of.
        /// </summary>
        private static string Shape( FileProfile profile )
        {
            return ColumnCount( profile ).ToString( "N0", CultureInfo.CurrentCulture );
        }

        private static string Layouts( FileProfile profile )
        {
            return profile.IsFixedLength
                ? profile.RecordTypes.Count.ToString( CultureInfo.CurrentCulture )
                : profile.QuoteEveryField ? "quoted" : "plain";
        }

        private static string Duration( TimeSpan elapsed )
        {
            return elapsed.TotalSeconds >= 1.0
                ? elapsed.TotalSeconds.ToString( "N2", CultureInfo.CurrentCulture ) + " s"
                : elapsed.TotalMilliseconds.ToString( "N0", CultureInfo.CurrentCulture ) + " ms";
        }

        private static string Megabytes( long bytes )
        {
            return ( bytes / 1024.0 / 1024.0 ).ToString( "N1", CultureInfo.CurrentCulture ) + " MB";
        }
    }
}
