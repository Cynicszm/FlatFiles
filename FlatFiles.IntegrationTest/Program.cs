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
        private static readonly string[] Scenarios = ["parse", "typed", "values", "mapper"];

        /// <summary>
        ///     How many times each measurement is taken. Each one is a process of its own doing a single cold
        ///     read, because that is what the library is mostly asked to do - a job starts, loads a file as fast
        ///     as it can, and exits - and the only way to measure a cold start more than once is to start again.
        /// </summary>
        private const int Runs = 5;

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
            Console.WriteLine( "  baseline [profile...]   measure, write the baseline, and print the table and notes" );
            Console.WriteLine( "                          for the changelog entry, from the same runs." );
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
                result.PeakWorkingSetBytes.ToString( CultureInfo.InvariantCulture ),
                result.Reads.ToString( CultureInfo.InvariantCulture ),
                result.FastestRead.TotalMilliseconds.ToString( "F1", CultureInfo.InvariantCulture ),
                result.SlowestRead.TotalMilliseconds.ToString( "F1", CultureInfo.InvariantCulture ) ) );
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
            Console.WriteLine();
            Console.WriteLine( new string( '-', 110 ) );
            Console.WriteLine( "For the changelog entry of the release this baseline is for. These are the runs the baseline" );
            Console.WriteLine( "was taken from, so the table and the figures it gates on are the same measurements." );
            Console.WriteLine( new string( '-', 110 ) );
            // Printed rather than written to a file: the changelog is where these figures live, and a second
            // copy beside the baseline would only go stale against it.
            ReportAsMarkdown( profiles, results );
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
                    // Each run gets a process of its own: it keeps the read cold, which is the case worth
                    // measuring, and it keeps peak working set meaningful, since that figure only counts up.
                    List<RunResult> runs = [];
                    for (var run = 0; run != Runs; ++run)
                    {
                        var measured = MeasureElsewhere( profile.Name, scenario );
                        if (measured is not null)
                        {
                            runs.Add( measured );
                        }
                    }
                    if (runs.Count != 0)
                    {
                        results.Add( Average( runs ) );
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
        ///     The same figures as Markdown, for the changelog entry a release carries: a table per format for the
        ///     scenarios that read through a schema, and a second pair for the one that reads onto entities.
        ///     Separate tables because `mapper` is not a further step than `values` and its figures are not
        ///     comparable with theirs - a row of it among them invites a comparison that means nothing.
        /// </summary>
        private static void ReportAsMarkdown( List<FileProfile> profiles, List<RunResult> results )
        {
            var throughSchema = results.FindAll( x => x.Scenario != MappedScenario );
            var ontoEntities = results.FindAll( x => x.Scenario == MappedScenario );

            WriteMarkdownTable( "Delimited", profiles, throughSchema, fixedLength: false, withScenario: true );
            WriteMarkdownTable( "Fixed-length", profiles, throughSchema, fixedLength: true, withScenario: true );
            // One scenario, so a column repeating its name on every row says nothing the heading has not.
            WriteMarkdownTable( "Delimited, through a type mapper", profiles, ontoEntities, fixedLength: false, withScenario: false );
            WriteMarkdownTable( "Fixed-length, through a type mapper", profiles, ontoEntities, fixedLength: true, withScenario: false );
            WriteMarkdownNotes( results );
        }

        private const string MappedScenario = "mapper";

        private static void WriteMarkdownTable( string heading, List<FileProfile> profiles, List<RunResult> results, bool fixedLength, bool withScenario )
        {
            // A run of one profile has nothing for three of the four tables, and an empty table with a heading
            // over it says less than no table at all.
            if (!results.Exists( x => profiles.Find( p => p.Name == x.Profile )!.IsFixedLength == fixedLength ))
            {
                return;
            }
            Console.WriteLine();
            Console.WriteLine( "**{0}**", heading );
            Console.WriteLine();
            Console.WriteLine( withScenario
                ? "| Sample | Columns | Records | Scenario | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |"
                : "| Sample | Columns | Records | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |" );
            Console.WriteLine( withScenario
                ? "| --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |"
                : "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |" );

            var previous = string.Empty;
            foreach (var result in results)
            {
                var profile = profiles.Find( x => x.Name == result.Profile )!;
                if (profile.IsFixedLength != fixedLength)
                {
                    continue;
                }
                // A rule between one sample and the next, so a sample's scenarios read as a group. A table of one
                // row per sample has nothing to group, so it gets none.
                if (previous.Length != 0 && result.Profile != previous && results.Exists( x => x.Scenario != results[0].Scenario ))
                {
                    Console.WriteLine( "| | | | | | | | | | |" );
                }
                previous = result.Profile;
                // The heading says the format, so a column repeating it on every row says nothing.
                Console.WriteLine( withScenario ? "| {0} | {1:N0} | {2:N0} | `{3}` | {4} | {5} | {6:N1} | {7:N0} | {8} | {9} |"
                                                : "| {0} | {1:N0} | {2:N0} | {4} | {5} | {6:N1} | {7:N0} | {8} | {9} |",
                    Shorthand( result.Profile ),
                    ColumnCount( profile ),
                    result.Records,
                    result.Scenario,
                    Duration( result.Elapsed ), Spread( result ), result.MegabytesPerSecond, result.BytesPerRecord,
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
            Console.WriteLine( "Each row is one sample loaded {0} times, each in a process of its own that starts, reads the file once and", Runs );
            Console.WriteLine( "exits - which is how the library is mostly used - and the figures are the mean of those {0}. Everything a", Runs );
            Console.WriteLine( "job pays for is inside them: the runtime compiling the parse path on first use, the schema being built," );
            Console.WriteLine( "the file being opened. The first three scenarios are cumulative:" );
            Console.WriteLine( "`parse` reads every column as text and asks for no value, `typed` gives each single-typed column its" );
            Console.WriteLine( "own type, and `values` is `typed` with `GetValues` called on every record. The difference between two" );
            Console.WriteLine( "of them is the cost of the step between." );
            Console.WriteLine();
            Console.WriteLine( "`mapper` has tables of its own because it is not a fourth step but a different path: the file read onto" );
            Console.WriteLine( "entities through a type mapper, which is the only scenario that builds an entity or uses the setters a" );
            Console.WriteLine( "mapper makes per column. It maps the first column of each kind the profile knows about and ignores the" );
            Console.WriteLine( "rest, which costs the reader the column but not the parse, so its figures are far below the others and" );
            Console.WriteLine( "mean something different. Read each set against itself and against the same set in an earlier release." );
            Console.WriteLine();
            Console.WriteLine( "| Column | What it measures |" );
            Console.WriteLine( "| --- | --- |" );
            Console.WriteLine( "| Columns | Columns in the schema; for a fixed-length sample, in its widest record layout. |" );
            Console.WriteLine( "| Records | Records the reader yielded. Gated exactly. No sample is built to have a record refused, so any refusal fails the check whatever else agrees. |" );
            Console.WriteLine( "| Mean Total Time | Mean wall clock of {0} cold loads: opening the file, building the schema, constructing the reader, reading every record, and disposing, in a process that has done nothing else. Nothing is amortised over reads a real caller never performs. **Reported, never gated.** |", Runs );
            Console.WriteLine( "| Range | The quickest and slowest of those {0} loads, so the spread behind the mean is visible rather than implied. |", Runs );
            Console.WriteLine( "| MB/s | File size divided by Mean Total Time, so it carries the same caveats. |" );
            Console.WriteLine( "| Bytes/record | Mean bytes allocated across a load, divided by records read. Deterministic for given bytes on a given runtime, and repeats to within 0.1% here. **Gated at 2%.** |" );
            Console.WriteLine( "| Peak heap | The largest the managed heap reached in any of the {0} loads, sampled every 5 ms. Reported. |", Runs );
            Console.WriteLine( "| Peak working set | The largest peak working set any of those processes reached. Each does one load and exits, so the figure is a whole job's footprint, most of it runtime start-up rather than the read. Reported. |" );
            Console.WriteLine();
            Console.WriteLine( "Averaging {0} whole processes takes most of the machine noise out, but a cold start is noisy by nature and", Runs );
            Console.WriteLine( "the range shows what is left. `FlatFiles.Benchmark` is the project that measures a warm steady state, with" );
            Console.WriteLine( "statistics rather than a mean; these figures are the other question - what one job costs end to end - and" );
            Console.WriteLine( "show the shape of the work rather than a number to compare release to release, which is why neither Mean" );
            Console.WriteLine( "Total Time nor MB/s is gated." );

            List<string> refused = [.. results.Where( x => x.SkippedRecords != 0 )
                .Select( x => string.Format( CultureInfo.CurrentCulture, "{0} `{1}` refused {2:N0}", Shorthand( x.Profile ), x.Scenario, x.SkippedRecords ) )];
            if (refused.Count != 0)
            {
                Console.WriteLine();
                Console.WriteLine( "Records refused: " + string.Join( "; ", refused ) + "." );
            }
        }

        /// <summary>
        ///     One result from several runs of the same measurement: the mean of what they took and allocated,
        ///     the quickest and slowest of them, and the largest figure either memory reading reached.
        /// </summary>
        private static RunResult Average( List<RunResult> runs )
        {
            var first = runs[0];
            var ticks = 0L;
            var allocated = 0L;
            var quickest = long.MaxValue;
            var slowest = 0L;
            var peakManaged = 0L;
            var peakWorkingSet = 0L;
            foreach (var run in runs)
            {
                ticks += run.Elapsed.Ticks;
                allocated += run.AllocatedBytes;
                quickest = Math.Min( quickest, run.Elapsed.Ticks );
                slowest = Math.Max( slowest, run.Elapsed.Ticks );
                peakManaged = Math.Max( peakManaged, run.PeakManagedBytes );
                peakWorkingSet = Math.Max( peakWorkingSet, run.PeakWorkingSetBytes );
            }
            return new RunResult
            {
                Profile = first.Profile,
                Scenario = first.Scenario,
                Reads = runs.Count,
                Records = first.Records,
                SkippedRecords = first.SkippedRecords,
                FileSize = first.FileSize,
                Elapsed = TimeSpan.FromTicks( ticks / runs.Count ),
                FastestRead = TimeSpan.FromTicks( quickest ),
                SlowestRead = TimeSpan.FromTicks( slowest ),
                AllocatedBytes = allocated / runs.Count,
                PeakManagedBytes = peakManaged,
                PeakWorkingSetBytes = peakWorkingSet
            };
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
            if (process.ExitCode != 0 || parts.Length != 12)
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
                PeakWorkingSetBytes = long.Parse( parts[8], CultureInfo.InvariantCulture ),
                Reads = int.Parse( parts[9], CultureInfo.InvariantCulture ),
                FastestRead = TimeSpan.FromMilliseconds( double.Parse( parts[10], CultureInfo.InvariantCulture ) ),
                SlowestRead = TimeSpan.FromMilliseconds( double.Parse( parts[11], CultureInfo.InvariantCulture ) )
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
            Console.WriteLine( "{0,-14}{1,-8}{2,11}{3,9}{4,16}{5,20}{6,11}{7,12}{8,12}{9,12}",
                "Profile", "Scenario", "Records", "Refused", "Mean total time", "Range", "MB/s", "Bytes/rec", "Peak heap", "Peak WS" );
            var profileName = string.Empty;
            foreach (var result in results)
            {
                if (result.Profile != profileName)
                {
                    profileName = result.Profile;
                }
                Console.WriteLine( "{0,-14}{1,-8}{2,11:N0}{3,9:N0}{4,16}{5,20}{6,11:N1}{7,12:N0}{8,12}{9,12}",
                    result.Profile, result.Scenario, result.Records, result.SkippedRecords,
                    Duration( result.Elapsed ), Spread( result ), result.MegabytesPerSecond, result.BytesPerRecord,
                    Megabytes( result.PeakManagedBytes ), Megabytes( result.PeakWorkingSetBytes ) );
            }
            Console.WriteLine();
            Console.WriteLine( "parse  - every column read as text, no value asked for" );
            Console.WriteLine( "typed  - each single-typed column given its own type, no value asked for" );
            Console.WriteLine( "values - typed, and GetValues called for every record" );
            Console.WriteLine( "mapper - read onto entities through a type mapper, which is a different path entirely" );
            Console.WriteLine();
            Console.WriteLine( "Mean total time is over {0} cold loads, each a process that starts, reads the file once and exits,", Runs );
            Console.WriteLine( "with the quickest and slowest beside it. That is how the library is mostly used, so nothing here is" );
            Console.WriteLine( "amortised over reads a real caller never performs." );
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

        /// <summary>
        ///     The fastest and slowest of the reads behind a mean.
        /// </summary>
        private static string Spread( RunResult result )
        {
            return Duration( result.FastestRead ) + " - " + Duration( result.SlowestRead );
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
