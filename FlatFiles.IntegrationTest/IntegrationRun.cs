using System;
using System.Diagnostics;
using System.IO;

namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     What one file measured, averaged over several reads of it.
    /// </summary>
    internal sealed class RunResult
    {
        public string Profile { get; set; } = string.Empty;

        public string Scenario { get; set; } = string.Empty;

        /// <summary>
        ///     How many times the file was read to arrive at these figures.
        /// </summary>
        public int Reads { get; set; }

        public long Records { get; set; }

        public long SkippedRecords { get; set; }

        public long FileSize { get; set; }

        /// <summary>
        ///     The mean of the reads. The first of them is cold, so it carries whatever the runtime had left to
        ///     compile; the mean says so rather than hiding it behind a warm-up that the gate would then have to
        ///     trust.
        /// </summary>
        public TimeSpan Elapsed { get; set; }

        /// <summary>
        ///     The shortest and longest read, so that the spread behind the mean is visible.
        /// </summary>
        public TimeSpan FastestRead { get; set; }

        public TimeSpan SlowestRead { get; set; }

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
        /// <summary>
        ///     How many times each file is read before its figures are reported. One read is enough for
        ///     allocation, which is deterministic, and nowhere near enough for time, which is not.
        /// </summary>
        public const int Reads = 5;

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
            var size = new FileInfo( path ).Length;
            var records = 0L;
            var skipped = 0L;
            var totalTicks = 0L;
            var totalAllocated = 0L;
            var fastest = long.MaxValue;
            var slowest = 0L;

            using var watcher = new PeakMemoryWatcher();
            for (var read = 0; read != Reads; ++read)
            {
                // A settled heap before each read, so its allocation figure is that read's and nothing else's.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                records = 0;
                skipped = 0;
                var before = GC.GetTotalAllocatedBytes( true );
                var watch = Stopwatch.StartNew();
                ReadOnce( profile, path, scenario, ref records, ref skipped );
                watch.Stop();

                totalAllocated += GC.GetTotalAllocatedBytes( true ) - before;
                totalTicks += watch.Elapsed.Ticks;
                fastest = Math.Min( fastest, watch.Elapsed.Ticks );
                slowest = Math.Max( slowest, watch.Elapsed.Ticks );
            }
            watcher.Dispose();

            return new RunResult
            {
                Profile = profile.Name,
                Scenario = scenario,
                Reads = Reads,
                Records = records,
                SkippedRecords = skipped,
                FileSize = size,
                Elapsed = TimeSpan.FromTicks( totalTicks / Reads ),
                FastestRead = TimeSpan.FromTicks( fastest ),
                SlowestRead = TimeSpan.FromTicks( slowest ),
                AllocatedBytes = totalAllocated / Reads,
                PeakManagedBytes = watcher.Peak,
                PeakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64
            };
        }

        /// <summary>
        ///     One complete read: open the file, build the schema, construct the reader, take every record, and
        ///     let go of all of it. Everything the figures cover happens in here.
        /// </summary>
        private static void ReadOnce( FileProfile profile, string path, string scenario, ref long records, ref long skipped )
        {
            var typed = scenario is "typed" or "values";
            var wantsValues = scenario == "values";
            var taken = 0L;
            var refused = 0L;

            using (var stream = new FileStream( path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20 ))
            using (var text = new StreamReader( stream ))
            {
                IReader reader = profile.IsFixedLength ? FixedLength( profile, text, typed ) : Delimited( profile, text, typed );
                // A record the schema cannot take - one carrying a separator inside a field, or one no layout
                // recognises - is counted and skipped, which is what a caller does with a file this size.
                reader.RecordError += ( _, e ) =>
                {
                    ++refused;
                    e.IsHandled = true;
                };
                while (reader.Read())
                {
                    ++taken;
                    if (wantsValues)
                    {
                        GC.KeepAlive( reader.GetValues() );
                    }
                }
            }
            records = taken;
            skipped = refused;
        }
    }
}
