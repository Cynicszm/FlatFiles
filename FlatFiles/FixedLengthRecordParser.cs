using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FlatFiles.Properties;

namespace FlatFiles
{
    /// <summary>
    ///     Reads a fixed-length file one record at a time, either up to a record separator or, when the file has no
    ///     separators, by the total width of the schema's windows.
    /// </summary>
    internal sealed class FixedLengthRecordParser
    {
        private readonly IRecordReader recordReader;

        public FixedLengthRecordParser( TextReader reader, FixedLengthSchema? schema, FixedLengthOptions options, int bufferSize = CharBufferReader.DefaultBufferSize )
        {
            // When no record separator is specified, we must rely on the total width of the windows
            // to figure out how much to read. If a separator is provided, we just read up to that
            // separator. We can then pass that record to the schema selector to determine the schema
            // afterwards.
            if (options.HasRecordSeparator)
            {
                recordReader = new SeparatorRecordReader( new CharBufferReader( reader, bufferSize ), options.RecordSeparator );
            }
            else if (schema is null)
            {
                throw new FlatFileException( Resources.RecordSeparatorRequired );
            }
            else
            {
                recordReader = new FixedLengthRecordReader( reader, schema.TotalWidth );
            }
        }

        public bool IsEndOfStream()
        {
            return recordReader.IsEndOfStream();
        }

        public ValueTask<bool> IsEndOfStreamAsync( CancellationToken cancellationToken = default )
        {
            return recordReader.IsEndOfStreamAsync( cancellationToken );
        }

        public string ReadRecord()
        {
            return recordReader.ReadRecord();
        }

        public Task<string> ReadRecordAsync( CancellationToken cancellationToken = default )
        {
            return recordReader.ReadRecordAsync( cancellationToken );
        }

        private interface IRecordReader
        {
            bool IsEndOfStream();

            ValueTask<bool> IsEndOfStreamAsync( CancellationToken cancellationToken );

            string ReadRecord();

            Task<string> ReadRecordAsync( CancellationToken cancellationToken );
        }

        /// <summary>
        ///     Reads records that end at a separator, scanning the buffered text for the next candidate rather than
        ///     examining each character. Fixed-length reads its column values out of the record text, so unlike the
        ///     delimited reader it always keeps that text.
        /// </summary>
        private sealed class SeparatorRecordReader( CharBufferReader buffer, string? separator ) : IRecordReader
        {
            private readonly RecordSeparatorMatcher matcher = new( separator );

            public bool IsEndOfStream()
            {
                if (buffer is { Available: 0, IsEndOfStream: false })
                {
                    buffer.Fill();
                }
                return buffer is { IsEndOfStream: true, Available: 0 };
            }

            public async ValueTask<bool> IsEndOfStreamAsync( CancellationToken cancellationToken = default )
            {
                if (buffer is { Available: 0, IsEndOfStream: false })
                {
                    await buffer.FillAsync( cancellationToken ).ConfigureAwait( false );
                }
                return buffer is { IsEndOfStream: true, Available: 0 };
            }

            public string ReadRecord()
            {
                string? record;
                while (!TryReadRecord( out record ))
                {
                    buffer.Fill();
                }
                return record;
            }

            public async Task<string> ReadRecordAsync( CancellationToken cancellationToken = default )
            {
                string? record;
                while (!TryReadRecord( out record ))
                {
                    await buffer.FillAsync( cancellationToken ).ConfigureAwait( false );
                }
                return record;
            }

            private bool TryReadRecord( [NotNullWhen( true )] out string? record )
            {
                var text = buffer.Span;
                var scan = 0;
                while (true)
                {
                    var index = text[scan..].IndexOfAny( matcher.StartCharacters );
                    if (index < 0)
                    {
                        if (!buffer.IsEndOfStream)
                        {
                            record = null;
                            return false;
                        }
                        record = new string( text );
                        buffer.Consume( text.Length );
                        return true;
                    }
                    var candidate = scan + index;
                    if (text.Length - candidate < matcher.MaximumLength && !buffer.IsEndOfStream)
                    {
                        record = null;
                        return false;
                    }
                    if (matcher.IsMatch( text, candidate, out var length ))
                    {
                        record = new string( text[..candidate] );
                        buffer.Consume( candidate + length );
                        return true;
                    }
                    scan = candidate + 1;
                }
            }
        }

        private sealed class FixedLengthRecordReader( TextReader reader, int totalWidth ) : IRecordReader
        {
            private readonly char[] buffer = new char[totalWidth];
            private int length;
            private bool isEndOfStream;

            public bool IsEndOfStream()
            {
                if (isEndOfStream)
                {
                    return true;
                }
                length = reader.ReadBlock( buffer );
                if (length != 0)
                {
                    return false;
                }
                isEndOfStream = true;
                return true;
            }

            public async ValueTask<bool> IsEndOfStreamAsync( CancellationToken cancellationToken = default )
            {
                if (isEndOfStream)
                {
                    return true;
                }
                length = await reader.ReadBlockAsync( buffer, cancellationToken ).ConfigureAwait( false );
                if (length != 0)
                {
                    return false;
                }
                isEndOfStream = true;
                return true;
            }

            public string ReadRecord()
            {
                return new string( buffer, 0, length );
            }

            public Task<string> ReadRecordAsync( CancellationToken cancellationToken = default )
            {
                return Task.FromResult( new string( buffer, 0, length ) );
            }
        }
    }
}
