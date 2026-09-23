using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     What these files cost when the baseline was taken, and how far a figure may move before the check
    ///     calls it a change rather than noise.
    /// </summary>
    /// <remarks>
    ///     Two figures are gated and one is not. How many records a sample yields is exact: a change there is a
    ///     change in what the library does with a real-shaped file, whether or not anybody meant it, and it
    ///     catches a record being refused as surely as a count of refusals would, because a refused record is one
    ///     the run did not yield. What a read allocates is deterministic to the byte for a given file and runtime,
    ///     so it is gated on a tolerance that exists only to absorb a runtime's own housekeeping. How long a read
    ///     takes is not gated at all - the same unchanged code has measured 11 ms and 22 ms on the same machine
    ///     within a minute - so it is reported and left to a human.
    /// </remarks>
    internal sealed class Baseline
    {
        public const string FileName = "Baseline.json";

        public string Note { get; set; } = string.Empty;

        public Tolerances Tolerance { get; set; } = new();

        /// <summary>
        ///     The samples these figures were measured from, by hash. A run that reads different bytes is not
        ///     comparable with this baseline whatever the figures say, so the check refuses it rather than
        ///     reporting a difference it cannot explain.
        /// </summary>
        public List<SampleFile> Files { get; set; } = [];

        public List<Measurement> Measurements { get; set; } = [];

        public static Baseline Load( string path )
        {
            using var stream = File.OpenRead( path );
            return JsonSerializer.Deserialize( stream, BaselineJson.Default.Baseline )!;
        }

        public void Save( string path )
        {
            using var stream = File.Create( path );
            JsonSerializer.Serialize( stream, this, BaselineJson.Default.Baseline );
        }

        public Measurement? Find( string profile, string scenario )
        {
            return Measurements.Find( x => x.Profile == profile && x.Scenario == scenario );
        }

        public SampleFile? FindFile( string name )
        {
            return Files.Find( x => x.Name == name );
        }
    }

    internal sealed class Tolerances
    {
        /// <summary>
        ///     How far the bytes a record allocates may move, as a fraction. Two percent absorbs a runtime's own
        ///     variation without hiding a change worth knowing about; the smallest change this suite has measured
        ///     in anger was eight times that.
        /// </summary>
        public double BytesPerRecord { get; set; } = 0.02;
    }

    internal sealed class SampleFile
    {
        public string Name { get; set; } = string.Empty;

        public long Bytes { get; set; }

        public string Sha256 { get; set; } = string.Empty;
    }

    internal sealed class Measurement
    {
        public string Profile { get; set; } = string.Empty;

        public string Scenario { get; set; } = string.Empty;

        public long Records { get; set; }

        public double BytesPerRecord { get; set; }
    }

    [JsonSourceGenerationOptions( PropertyNameCaseInsensitive = true, WriteIndented = true )]
    [JsonSerializable( typeof( Baseline ) )]
    internal sealed partial class BaselineJson : JsonSerializerContext
    {
    }
}
