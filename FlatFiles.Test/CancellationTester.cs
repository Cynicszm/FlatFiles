using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests that the cancellation token given to an asynchronous read or write reaches the underlying reader or
    ///     writer, that an already-cancelled token stops the call before any work, that a token cancelled while the
    ///     stream is waiting stops the wait, and that the overloads without a token still hand the I/O layer
    ///     <see cref="CancellationToken.None" />.
    /// </summary>
    [TestClass]
    public class CancellationTester
    {
        private const string Csv = "Id,Name\r\n1,Bob\r\n2,Jane\r\n";
        private const string Fixed = "1  Bob \r\n2  Jane\r\n";

        private static DelimitedSchema DelimitedSchema()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new Int32Column( "Id" ) );
            schema.AddColumn( new StringColumn( "Name" ) );
            return schema;
        }

        private static FixedLengthSchema FixedSchema()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "Id" ), new Window( 3 ) );
            schema.AddColumn( new StringColumn( "Name" ), new Window( 4 ) );
            return schema;
        }

        private static IDelimitedTypeMapper<Person> Mapper()
        {
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( p => p.Id ).ColumnName( "Id" );
            mapper.Property( p => p.Name ).ColumnName( "Name" );
            return mapper;
        }

        private static CancellationToken Cancelled()
        {
            var source = new CancellationTokenSource();
            source.Cancel();
            return source.Token;
        }

        [TestMethod]
        public async Task TestReadAsync_TokenReachesTheTextReader()
        {
            var source = new CancellationTokenSource();
            var recording = new RecordingReader( Csv );
            var reader = new DelimitedReader( recording, DelimitedSchema(), new DelimitedOptions { IsFirstRecordSchema = true } );

            Assert.IsTrue( await reader.ReadAsync( source.Token ) );

            Assert.IsTrue( recording.Tokens.Count > 0, "The reader should have been asked for text." );
            Assert.IsTrue( recording.Tokens.All( t => t == source.Token ), "Every read should carry the caller's token." );
        }

        [TestMethod]
        public async Task TestReadAsync_WithoutAToken_HandsTheReaderNone()
        {
            var recording = new RecordingReader( Csv );
            var reader = new DelimitedReader( recording, DelimitedSchema(), new DelimitedOptions { IsFirstRecordSchema = true } );

            Assert.IsTrue( await reader.ReadAsync() );

            Assert.IsTrue( recording.Tokens.All( t => t == CancellationToken.None ) );
        }

        [TestMethod]
        public async Task TestFixedLengthReadAsync_TokenReachesTheTextReader()
        {
            var source = new CancellationTokenSource();
            var recording = new RecordingReader( Fixed );
            var reader = new FixedLengthReader( recording, FixedSchema(), new FixedLengthOptions { RecordSeparator = "\r\n" } );

            Assert.IsTrue( await reader.ReadAsync( source.Token ) );

            Assert.IsTrue( recording.Tokens.Count > 0 && recording.Tokens.All( t => t == source.Token ) );
        }

        [TestMethod]
        public async Task TestReadAsync_AlreadyCancelled_ThrowsBeforeReadingAnything()
        {
            var recording = new RecordingReader( Csv );
            var reader = new DelimitedReader( recording, DelimitedSchema(), new DelimitedOptions { IsFirstRecordSchema = true } );

            await Assert.ThrowsExactlyAsync<OperationCanceledException>( async () => await reader.ReadAsync( Cancelled() ) );

            Assert.AreEqual( 0, recording.Tokens.Count, "A cancelled token should stop the call before the stream is touched." );
        }

        [TestMethod]
        public async Task TestReadAsync_CancelledWhileWaitingForTheStream_StopsWaiting()
        {
            using var source = new CancellationTokenSource();
            var reader = new DelimitedReader( new BlockingReader(), DelimitedSchema() );
            var pending = reader.ReadAsync( source.Token );
            Assert.IsFalse( pending.IsCompleted, "The reader should be waiting on a stream that never delivers." );

            source.CancelAfter( 50 );

            await Assert.ThrowsExactlyAsync<TaskCanceledException>( async () => await pending );
        }

        [TestMethod]
        public async Task TestSkipAsyncAndGetSchemaAsync_AlreadyCancelled_Throw()
        {
            var reader = new DelimitedReader( new StringReader( Csv ), DelimitedSchema(), new DelimitedOptions { IsFirstRecordSchema = true } );

            await Assert.ThrowsExactlyAsync<OperationCanceledException>( async () => await reader.SkipAsync( Cancelled() ) );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( async () => await reader.GetSchemaAsync( Cancelled() ) );
        }

        [TestMethod]
        public async Task TestWriteAsync_TokenReachesTheTextWriter()
        {
            var source = new CancellationTokenSource();
            var recording = new RecordingWriter();
            var writer = new DelimitedWriter( recording, DelimitedSchema(), new DelimitedOptions { IsFirstRecordSchema = true, RecordSeparator = "\r\n" } );

            await writer.WriteAsync( [ 1, "Bob" ], source.Token );

            Assert.IsTrue( recording.Tokens.Count >= 2, "The header, the record and their separators should each have been written." );
            Assert.IsTrue( recording.Tokens.All( t => t == source.Token ) );
            Assert.AreEqual( Csv[..^"2,Jane\r\n".Length], recording.ToString() );
        }

        [TestMethod]
        public async Task TestFixedLengthWriteAsync_TokenReachesTheTextWriter()
        {
            var source = new CancellationTokenSource();
            var recording = new RecordingWriter();
            var writer = new FixedLengthWriter( recording, FixedSchema(), new FixedLengthOptions { RecordSeparator = "\r\n" } );

            await writer.WriteAsync( [ 1, "Bob" ], source.Token );
            await writer.WriteRawAsync( "raw", true, source.Token );

            Assert.IsTrue( recording.Tokens.Count >= 3 && recording.Tokens.All( t => t == source.Token ) );
            Assert.AreEqual( "1  Bob \r\nraw\r\n", recording.ToString() );
        }

        [TestMethod]
        public async Task TestWriteAsync_AlreadyCancelled_WritesNothing()
        {
            var recording = new RecordingWriter();
            var writer = new DelimitedWriter( recording, DelimitedSchema(), new DelimitedOptions { IsFirstRecordSchema = true } );

            await Assert.ThrowsExactlyAsync<OperationCanceledException>( async () => await writer.WriteAsync( [ 1, "Bob" ], Cancelled() ) );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( async () => await writer.WriteSchemaAsync( Cancelled() ) );

            Assert.AreEqual( "", recording.ToString() );
        }

        [TestMethod]
        public async Task TestTypedReaderAndWriter_TokenFlowsThroughTheMapper()
        {
            var source = new CancellationTokenSource();
            var mapper = Mapper();
            var recordingWriter = new RecordingWriter();
            await mapper.WriteAsync( recordingWriter, [ new Person { Id = 1, Name = "Bob" } ], new DelimitedOptions { IsFirstRecordSchema = true }, source.Token );
            Assert.IsTrue( recordingWriter.Tokens.All( t => t == source.Token ), "The mapper's write should carry the token to the stream." );

            var recordingReader = new RecordingReader( recordingWriter.ToString() );
            List<Person> people = [];
            await foreach (var person in mapper.ReadAsync( recordingReader, new DelimitedOptions { IsFirstRecordSchema = true }, source.Token ))
            {
                people.Add( person );
            }

            Assert.AreEqual( 1, people.Count );
            Assert.IsTrue( recordingReader.Tokens.All( t => t == source.Token ), "The mapper's read should carry the token to the stream." );
        }

        [TestMethod]
        public async Task TestReadAllAsync_WithCancellation_FlowsTheEnumeratorToken()
        {
            // The token can also arrive through await foreach's WithCancellation rather than the method parameter.
            var source = new CancellationTokenSource();
            var recording = new RecordingReader( Csv );
            var typedReader = Mapper().GetReader( recording, new DelimitedOptions { IsFirstRecordSchema = true } );

            var count = 0;
            await foreach (var _ in typedReader.ReadAllAsync().WithCancellation( source.Token ))
            {
                ++count;
            }

            Assert.AreEqual( 2, count );
            Assert.IsTrue( recording.Tokens.All( t => t == source.Token ) );
        }

        [TestMethod]
        public async Task TestWriteAllAsync_AlreadyCancelled_WritesNothing()
        {
            var recording = new RecordingWriter();
            var typedWriter = Mapper().GetWriter( recording, new DelimitedOptions { IsFirstRecordSchema = true } );

            await Assert.ThrowsExactlyAsync<OperationCanceledException>( async () => await typedWriter.WriteAllAsync( [ new Person { Id = 1, Name = "Bob" } ], Cancelled() ) );

            Assert.AreEqual( "", recording.ToString() );
        }

        [TestMethod]
        public async Task TestParameterlessOverloads_StillWork()
        {
            var mapper = Mapper();
            var output = new StringWriter();
            await mapper.WriteAsync( output, [ new Person { Id = 1, Name = "Bob" }, new Person { Id = 2, Name = "Jane" } ], new DelimitedOptions { IsFirstRecordSchema = true, RecordSeparator = "\r\n" } );
            Assert.AreEqual( Csv, output.ToString() );

            var typedReader = mapper.GetReader( new StringReader( Csv ), new DelimitedOptions { IsFirstRecordSchema = true } );
            Assert.IsTrue( await typedReader.ReadAsync() );
            Assert.IsTrue( await typedReader.SkipAsync() );
            Assert.IsFalse( await typedReader.ReadAsync() );
        }

        [TestMethod]
        public async Task TestInterfaceDefaults_WithoutAToken_HandTheImplementationNone()
        {
            var recorder = new TokenRecordingTypedWriter();
            ITypedWriter<string> writer = recorder;
            await writer.WriteAsync( "record" );
            await writer.WriteSchemaAsync();

            Assert.HasCount( 2, recorder.Recorded );
            Assert.IsFalse( recorder.Recorded[0].CanBeCanceled );
            Assert.IsFalse( recorder.Recorded[1].CanBeCanceled );
        }

        [TestMethod]
        public async Task TestInterfaceDefaults_WithAToken_PassItThrough()
        {
            using var source = new CancellationTokenSource();
            var recorder = new TokenRecordingTypedWriter();
            ITypedWriter<string> writer = recorder;
            await writer.WriteAsync( "record", source.Token );

            Assert.HasCount( 1, recorder.Recorded );
            Assert.AreEqual( source.Token, recorder.Recorded[0] );
        }

        public sealed class Person
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;
        }
        /// <summary>
        ///     An implementation outside the library that supplies only the required members, so the overloads without a
        ///     token are the interface's defaults.
        /// </summary>
        private sealed class TokenRecordingTypedWriter : ITypedWriter<string>
        {
            public List<CancellationToken> Recorded { get; } = [];

            public IWriter Writer => throw new NotSupportedException();

            public event EventHandler<ColumnErrorEventArgs>? ColumnError { add { } remove { } }

            public event EventHandler<RecordErrorEventArgs>? RecordError { add { } remove { } }

            public ISchema GetSchema() => null!;

            public void WriteSchema() { }

            public Task WriteSchemaAsync( CancellationToken cancellationToken )
            {
                Recorded.Add( cancellationToken );
                return Task.CompletedTask;
            }

            public void Write( string entity ) { }

            public Task WriteAsync( string entity, CancellationToken cancellationToken )
            {
                Recorded.Add( cancellationToken );
                return Task.CompletedTask;
            }
        }


        /// <summary>
        ///     A reader over a string that records the token passed to each asynchronous read.
        /// </summary>
        private sealed class RecordingReader( string text ) : TextReader
        {
            private readonly StringReader inner = new( text );

            public List<CancellationToken> Tokens { get; } = [];

            public override int Read( Span<char> buffer )
            {
                return inner.Read( buffer );
            }

            public override int Read()
            {
                return inner.Read();
            }

            // The library reads through ReadBlockAsync, and TextReader's own ReadBlockAsync does not go through
            // ReadAsync, so this is the member that has to record the token.
            public override ValueTask<int> ReadBlockAsync( Memory<char> buffer, CancellationToken cancellationToken = default )
            {
                Tokens.Add( cancellationToken );
                return new ValueTask<int>( inner.ReadBlock( buffer.Span ) );
            }
        }

        /// <summary>
        ///     A reader whose asynchronous read never completes on its own, only through its token.
        /// </summary>
        private sealed class BlockingReader : TextReader
        {
            public override async ValueTask<int> ReadBlockAsync( Memory<char> buffer, CancellationToken cancellationToken = default )
            {
                await Task.Delay( Timeout.Infinite, cancellationToken );
                return 0;
            }
        }

        /// <summary>
        ///     A writer that records the token passed to each asynchronous write and keeps the text.
        /// </summary>
        private sealed class RecordingWriter : TextWriter
        {
            private readonly StringWriter inner = new();

            public List<CancellationToken> Tokens { get; } = [];

            public override System.Text.Encoding Encoding => inner.Encoding;

            public override void Write( char value )
            {
                inner.Write( value );
            }

            public override Task WriteAsync( ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default )
            {
                Tokens.Add( cancellationToken );
                inner.Write( buffer.Span );
                return Task.CompletedTask;
            }

            public override string ToString()
            {
                return inner.ToString();
            }
        }
    }
}
