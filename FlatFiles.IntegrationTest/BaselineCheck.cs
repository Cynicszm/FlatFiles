using System;
using System.Collections.Generic;
using System.Globalization;

namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     Compares a set of runs against the committed baseline and says whether anything moved. Used before a
    ///     release: a file that now yields a different number of records, or a read that now allocates
    ///     measurably more or less, is a change somebody has to have meant.
    /// </summary>
    internal static class BaselineCheck
    {
        /// <summary>
        ///     Whether the samples about to be read are the ones the baseline was taken from. A figure measured
        ///     against different bytes says nothing, so this runs before anything is measured.
        /// </summary>
        public static bool VerifySamples( Baseline baseline, List<(string Name, string Path)> samples )
        {
            if (baseline.Files.Count == 0)
            {
                Console.WriteLine( "The baseline records no sample hashes. Take it again to have the samples pinned." );
                return true;
            }
            Console.WriteLine( "{0,-14}{1,16}   {2}", "Sample", "Bytes", "" );
            var passed = true;
            foreach (var (name, path) in samples)
            {
                var expected = baseline.FindFile( name );
                var bytes = new System.IO.FileInfo( path ).Length;
                if (expected is null)
                {
                    passed = false;
                    Console.WriteLine( "{0,-14}{1,16:N0}   FAILED: the baseline does not know this sample", name, bytes );
                    continue;
                }
                var hash = FileStore.Hash( path );
                var same = hash == expected.Sha256 && bytes == expected.Bytes;
                passed &= same;
                Console.WriteLine( "{0,-14}{1,16:N0}   {2}", name, bytes,
                    same ? "as committed" : "FAILED: this is not the sample the baseline was taken from" );
            }
            if (!passed)
            {
                Console.WriteLine();
                Console.WriteLine( "The samples have changed. If they were regenerated on purpose, take a new baseline;" );
                Console.WriteLine( "otherwise restore them, because a figure measured against different bytes means nothing." );
            }
            return passed;
        }

        public static bool Compare( Baseline baseline, List<RunResult> results )
        {
            Console.WriteLine();
            Console.WriteLine( new string( '=', 104 ) );
            Console.WriteLine( "AGAINST THE BASELINE" );
            Console.WriteLine( new string( '=', 104 ) );
            Console.WriteLine( "{0,-14}{1,-13}{2,12}{3,14}{4,14}{5,10}   {6}",
                "Profile", "Scenario", "Records", "Bytes/rec", "Baseline", "Change", "" );

            var passed = true;
            var seen = new HashSet<string>();
            foreach (var result in results)
            {
                seen.Add( result.Profile + "/" + result.Scenario );
                var expected = baseline.Find( result.Profile, result.Scenario );
                if (expected is null)
                {
                    passed = false;
                    Console.WriteLine( "{0,-14}{1,-13}{2,12:N0}{3,14:N0}{4,14}{5,10}   {6}",
                        result.Profile, result.Scenario, result.Records,
                        result.BytesPerRecord, "-", "-", "FAILED: not in the baseline" );
                    continue;
                }
                var reasons = new List<string>();
                if (result.Records != expected.Records)
                {
                    reasons.Add( string.Format( CultureInfo.CurrentCulture, "records {0:N0} -> {1:N0}", expected.Records, result.Records ) );
                }
                // No sample is meant to have a record refused, so one that does is worth failing over whatever
                // the other figures say. The record count would catch it too, but not say what happened.
                if (result.SkippedRecords != 0)
                {
                    reasons.Add( string.Format( CultureInfo.CurrentCulture, "{0:N0} records were refused", result.SkippedRecords ) );
                }
                // What a write produced is pinned exactly. Allocation says what a write cost; this says what it
                // was, and a writer that quietly starts writing something else is the failure worth catching.
                if (expected.Written.Length != 0 && result.Written != expected.Written)
                {
                    reasons.Add( result.Written == "unstable"
                        ? "the same records wrote different bytes from one run to the next"
                        : "what it writes has changed" );
                }
                var change = expected.BytesPerRecord == 0 ? 0 : result.BytesPerRecord / expected.BytesPerRecord - 1;
                if (Math.Abs( change ) > baseline.Tolerance.BytesPerRecord)
                {
                    reasons.Add( string.Format( CultureInfo.CurrentCulture, "allocation moved {0:+0.0%;-0.0%}", change ) );
                }
                passed &= reasons.Count == 0;
                Console.WriteLine( "{0,-14}{1,-13}{2,12:N0}{3,14:N0}{4,14:N0}{5,10}   {6}",
                    result.Profile, result.Scenario, result.Records,
                    result.BytesPerRecord, expected.BytesPerRecord,
                    change.ToString( "+0.0%;-0.0%;0.0%", CultureInfo.CurrentCulture ),
                    reasons.Count == 0 ? string.Empty : "FAILED: " + string.Join( "; ", reasons ) );
            }

            foreach (var expected in baseline.Measurements)
            {
                if (seen.Contains( expected.Profile + "/" + expected.Scenario ))
                {
                    continue;
                }
                passed = false;
                Console.WriteLine( "{0,-14}{1,-13}{2,12}{3,14}{4,14:N0}{5,10}   {6}",
                    expected.Profile, expected.Scenario, "-", "-", expected.BytesPerRecord, "-",
                    "FAILED: the baseline has this and the run did not produce it" );
            }

            Console.WriteLine();
            Console.WriteLine( passed
                ? "Every figure is where the baseline left it."
                : "Something moved. Either the change was intended, in which case take a new baseline and say so in the changelog, or it was not." );
            Console.WriteLine( "Allocation is gated at {0:P0} and what a write produced is gated to the byte; how long a read or a write takes is",
                baseline.Tolerance.BytesPerRecord );
            Console.WriteLine( "reported but never gated, because it is not stable enough to gate on." );
            return passed;
        }
    }
}
