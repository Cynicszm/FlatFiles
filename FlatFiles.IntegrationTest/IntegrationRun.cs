using System;
using System.Diagnostics;
using System.IO;

namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     What one run of one file measured.
    /// </summary>
    internal sealed class RunResult
    {
        public string Profile { get; set; } = string.Empty;

        public string Scenario { get; set; } = string.Empty;

        public long Records { get; set; }

        public long SkippedRecords { get; set; }

        public long FileSize { get; set; }

        public TimeSpan Elapsed { get; set; }

        public long AllocatedBytes { get; set; }

        public long PeakManagedBytes { get; set; }

        public long PeakWorkingSetBytes { get; set; }

        public double BytesPerRecord => Records == 0 ? 0 : (double) AllocatedBytes / Records;

        public double RecordsPerSecond => Elapsed.TotalSeconds <= 0 ? 0 : Records / Elapsed.TotalSeconds;

        public double MegabytesPerSecond => Elapsed.TotalSeconds <= 0 ? 0 : FileSize / 1024.0 / 1024.0 / Elapsed.TotalSeconds;
    }

    /// <summary>
    ///     Reads one generated file from end to end and reports what it cost.
    /// </summary>
    /// <remarks>
    ///     Three scenarios, each a step further than the last. <c>parse</c> reads every record and parses every
    ///     value as text without asking for any of it, which is the floor. <c>typed</c> does the same with each
    ///     column given its own type, so the difference is what parsing a value into something costs.
    ///     <c>values</c> adds a call to <see cref="IReader.GetValues" /> per record, which is what a caller that
    ///     keeps the values pays on top.
    /// </remarks>
    internal static class IntegrationRun
    {
        private static DelimitedReader Delimited( FileProfile profile, TextReader text, bool typed )
        {
            var options = new DelimitedOptions
            {
                Separator = profile.Separator,
                RecordSeparator = profile.RecordSeparator,
                IsFirstRecordSchema = true
            };
            return new DelimitedReader( text, SchemaFactory.Create( profile, typed ), options );
        }

        private static FixedLengthReader FixedLength( FileProfile profile, TextReader text, bool typed )
        {
            var options = new FixedLengthOptions
            {
                RecordSeparator = profile.RecordSeparator
            };
            return new FixedLengthReader( text, SchemaFactory.CreateSelector( profile, typed ), options );
        }

        public static RunResult Execute( FileProfile profile, string path, string scenario )
        {
            var typed = scenario is "typed" or "values";
            var wantsValues = scenario == "values";
            var size = new FileInfo( path ).Length;

            // A settled heap before the measurement, so the allocation figure is this run's and nothing else's.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var records = 0L;
            var skipped = 0L;
            var before = GC.GetTotalAllocatedBytes( true );
            using var watcher = new PeakMemoryWatcher();
            var watch = Stopwatch.StartNew();

            using (var stream = new FileStream( path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20 ))
            using (var text = new StreamReader( stream ))
            {
                IReader reader = profile.IsFixedLength ? FixedLength( profile, text, typed ) : Delimited( profile, text, typed );
                // A record the schema cannot take - one carrying a separator inside a field, or one no layout
                // recognises - is counted and skipped, which is what a caller does with a file this size.
                reader.RecordError += ( _, e ) =>
                {
                    ++skipped;
                    e.IsHandled = true;
                };
                while (reader.Read())
                {
                    ++records;
                    if (wantsValues)
                    {
                        GC.KeepAlive( reader.GetValues() );
                    }
                }
            }

            watch.Stop();
            var allocated = GC.GetTotalAllocatedBytes( true ) - before;
            watcher.Dispose();

            return new RunResult
            {
                Profile = profile.Name,
                Scenario = scenario,
                Records = records,
                SkippedRecords = skipped,
                FileSize = size,
                Elapsed = watch.Elapsed,
                AllocatedBytes = allocated,
                PeakManagedBytes = watcher.Peak,
                PeakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64
            };
        }
    }
}
