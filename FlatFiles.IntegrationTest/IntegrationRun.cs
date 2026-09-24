using System;
using System.Diagnostics;
using System.IO;

namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     What one file cost to load, averaged over several runs of it.
    /// </summary>
    internal sealed class RunResult
    {
        public string Profile { get; set; } = string.Empty;

        public string Scenario { get; set; } = string.Empty;

        /// <summary>
        ///     How many separate runs these figures are the mean of.
        /// </summary>
        public int Reads { get; set; }

        public long Records { get; set; }

        public long SkippedRecords { get; set; }

        public long FileSize { get; set; }

        /// <summary>
        ///     The mean of the runs.
        /// </summary>
        public TimeSpan Elapsed { get; set; }

        /// <summary>
        ///     The quickest and slowest of the runs, so that the spread behind the mean is visible.
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
    ///     Four scenarios. The first three are each a step further than the last: <c>parse</c> reads every record
    ///     and parses every value as text without asking for any of it, which is the floor; <c>typed</c> does the
    ///     same with each column given its own type, so the difference is what parsing a value into something
    ///     costs; and <c>values</c> adds a call to <see cref="IReader.GetValues" /> per record, which is what a
    ///     caller that keeps the values pays on top.
    ///     <para>
    ///         <c>mapper</c> is not a step further but a different path: the same file read onto entities through
    ///         a type mapper. Nothing in the other three builds an entity, asks the code generator for anything,
    ///         or uses the setters a mapper makes per column, so nothing in them can see a change there. A
    ///         release shipped with a factory built per record because this scenario did not exist.
    ///     </para>
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

        /// <summary>
        ///     Reads the file once and reports what that cost.
        /// </summary>
        /// <remarks>
        ///     Once, cold, and in a process that has done nothing else, because that is how the library is
        ///     mostly used: a job starts, loads a file as fast as it can, and exits. Everything a job pays for is
        ///     therefore inside the measurement - the runtime compiling the parse path on first use, the schema
        ///     being built, the file being opened - and none of it is amortised over reads that a real caller
        ///     never performs. The noise that comes with measuring a cold start is dealt with by repeating the
        ///     whole process and averaging, which is the caller's job rather than this method's.
        /// </remarks>
        public static RunResult Execute( FileProfile profile, string path, string scenario )
        {
            var size = new FileInfo( path ).Length;
            var records = 0L;
            var skipped = 0L;

            using var watcher = new PeakMemoryWatcher();
            var before = GC.GetTotalAllocatedBytes( true );
            var watch = Stopwatch.StartNew();
            ReadOnce( profile, path, scenario, ref records, ref skipped );
            watch.Stop();
            var allocated = GC.GetTotalAllocatedBytes( true ) - before;
            watcher.Dispose();

            return new RunResult
            {
                Profile = profile.Name,
                Scenario = scenario,
                Reads = 1,
                Records = records,
                SkippedRecords = skipped,
                FileSize = size,
                Elapsed = watch.Elapsed,
                FastestRead = watch.Elapsed,
                SlowestRead = watch.Elapsed,
                AllocatedBytes = allocated,
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
                if (scenario == "mapper")
                {
                    (records, skipped) = ReadMapped( profile, text );
                    return;
                }
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

        /// <summary>
        ///     One complete read onto entities. A fixed-length file of several layouts takes a mapper per layout
        ///     behind the same rules the schema selector uses, which is what a caller with such a file writes.
        /// </summary>
        private static (long Taken, long Refused) ReadMapped( FileProfile profile, TextReader text )
        {
            var taken = 0L;
            var refused = 0L;
            if (profile.IsFixedLength)
            {
                var options = new FixedLengthOptions
                {
                    RecordSeparator = profile.RecordSeparator
                };
                var reader = MapperFactory.CreateSelector( profile ).GetReader( text, options );
                reader.RecordError += ( _, e ) =>
                {
                    ++refused;
                    e.IsHandled = true;
                };
                while (reader.Read())
                {
                    ++taken;
                }
                return (taken, refused);
            }
            var delimitedOptions = new DelimitedOptions
            {
                Separator = profile.Separator,
                RecordSeparator = profile.RecordSeparator,
                IsFirstRecordSchema = true
            };
            var delimitedReader = MapperFactory.Create( profile ).GetReader( text, delimitedOptions );
            delimitedReader.RecordError += ( _, e ) =>
            {
                ++refused;
                e.IsHandled = true;
            };
            while (delimitedReader.Read())
            {
                ++taken;
            }
            return (taken, refused);
        }
    }
}
