using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     Keeps the sample files. They are committed, compressed, beside the profiles, and unpacked into the
    ///     build output to be read. Nothing here generates one: that happens only when somebody asks for it,
    ///     because the files are the input the measurements are gated against, and an input that regenerates
    ///     itself is not one.
    /// </summary>
    /// <remarks>
    ///     Compressed because one of them is 102 MB, past what a repository will take as a single file, and the
    ///     six together come to 210 MB uncompressed against 56 MB packed. Unpacking is deterministic, so the
    ///     bytes a run reads are the bytes that were committed, which is the whole point of committing them.
    /// </remarks>
    internal static class FileStore
    {
        public const string Extension = ".gz";

        /// <summary>
        ///     Unpacks the sample into the build output if it is not already there, and answers where it is.
        ///     Throws when there is no sample to unpack, rather than quietly making one.
        /// </summary>
        public static string Restore( string packed, string working )
        {
            if (!File.Exists( packed ))
            {
                throw new FileNotFoundException(
                    string.Format( CultureInfo.CurrentCulture,
                        "There is no sample at {0}. The samples are committed, not built: generate them deliberately with the generate command, and expect the baseline to need taking again.",
                        packed ) );
            }
            if (File.Exists( working ) && File.GetLastWriteTimeUtc( working ) >= File.GetLastWriteTimeUtc( packed ))
            {
                return working;
            }
            Directory.CreateDirectory( Path.GetDirectoryName( working )! );
            using (var source = File.OpenRead( packed ))
            using (var decompressor = new GZipStream( source, CompressionMode.Decompress ))
            using (var destination = new FileStream( working, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20 ))
            {
                decompressor.CopyTo( destination, 1 << 20 );
            }
            return working;
        }

        /// <summary>
        ///     Packs a freshly generated sample so it can be committed.
        /// </summary>
        public static long Pack( string working, string packed )
        {
            Directory.CreateDirectory( Path.GetDirectoryName( packed )! );
            using (var source = new FileStream( working, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20 ))
            using (var destination = File.Create( packed ))
            using (var compressor = new GZipStream( destination, CompressionLevel.Optimal ))
            {
                source.CopyTo( compressor, 1 << 20 );
            }
            return new FileInfo( packed ).Length;
        }

        /// <summary>
        ///     A hash of the file as it will be read, so a run can say whether it is reading what the baseline was
        ///     taken against.
        /// </summary>
        public static string Hash( string path )
        {
            using var stream = new FileStream( path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20 );
            return Convert.ToHexStringLower( SHA256.HashData( stream ) );
        }
    }
}
