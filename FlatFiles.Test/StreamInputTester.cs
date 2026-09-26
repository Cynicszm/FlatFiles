using System.IO;
using System.Linq;
using System.Text;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests reading a document straight from a stream: the encoding it is read as, the byte order mark that
    ///     must not survive into the first value, and the stream that must still be open afterwards.
    /// </summary>
    [TestClass]
    public class StreamInputTester
    {
        [TestMethod]
        public void TestDelimited_ReadsFromAStream()
        {
            using var stream = Utf8( "Bob,1\r\nSusan,2\r\n", withByteOrderMark: false );
            var reader = new DelimitedReader( stream, Schema(), Options );

            CollectionAssert.AreEqual( new[] { "Bob", "Susan" }, Names( reader ) );
        }

        [TestMethod]
        public void TestDelimited_AByteOrderMarkDoesNotBecomeTheFirstValue()
        {
            // The bug this exists to stop: a mark left in the text turns "Bob" into "﻿Bob", and a header into
            // a column whose name nothing matches.
            using var stream = Utf8( "Bob,1\r\n", withByteOrderMark: true );
            var reader = new DelimitedReader( stream, Schema(), Options );

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( "Bob", reader.GetValues()[0] );
        }

        [TestMethod]
        public void TestDelimited_AByteOrderMarkDoesNotBecomeTheFirstColumnName()
        {
            using var stream = Utf8( "Name,Id\r\nBob,1\r\n", withByteOrderMark: true );
            var options = new DelimitedOptions { RecordSeparator = "\r\n", IsFirstRecordSchema = true };
            var reader = new DelimitedReader( stream, options );

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( "Name", reader.GetSchema()!.ColumnDefinitions[0].ColumnName );
        }

        [TestMethod]
        public void TestDelimited_TheStreamIsLeftOpen()
        {
            // A reader does not own what it was handed, and none of these readers is disposable, so a caller who
            // opened the stream still has it afterwards.
            using var stream = Utf8( "Bob,1\r\n", withByteOrderMark: false );
            var reader = new DelimitedReader( stream, Schema(), Options );

            Names( reader );

            Assert.IsTrue( stream.CanRead, "The stream should still be usable by whoever opened it." );
        }

        [TestMethod]
        public void TestDelimited_AnEncodingCanBeAskedFor()
        {
            var bytes = Encoding.Unicode.GetBytes( "Bob,1\r\n" );
            using var stream = new MemoryStream( bytes );
            var reader = new DelimitedReader( stream, Schema(), Options, Encoding.Unicode );

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( "Bob", reader.GetValues()[0] );
        }

        [TestMethod]
        public void TestDelimited_AByteOrderMarkBeatsTheEncodingAskedFor()
        {
            // A file that says what it is should be read as what it says.
            var bytes = Encoding.Unicode.GetPreamble().Concat( Encoding.Unicode.GetBytes( "Bob,1\r\n" ) ).ToArray();
            using var stream = new MemoryStream( bytes );
            var reader = new DelimitedReader( stream, Schema(), Options, Encoding.UTF8 );

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( "Bob", reader.GetValues()[0] );
        }

        [TestMethod]
        public void TestFixedLength_ReadsFromAStream()
        {
            using var stream = Utf8( "Bob       01\r\nSusan     02\r\n", withByteOrderMark: true );
            var options = new FixedLengthOptions { RecordSeparator = "\r\n" };
            var reader = new FixedLengthReader( stream, FixedSchema(), options );

            CollectionAssert.AreEqual( new[] { "Bob", "Susan" }, Names( reader ) );
        }

        [TestMethod]
        public void TestMapper_ReadsFromAStream()
        {
            using var stream = Utf8( "Bob,1\r\n", withByteOrderMark: true );
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Id );

            var people = mapper.Read( stream, Options ).ToList();

            Assert.ContainsSingle( people );
            Assert.AreEqual( "Bob", people[0].Name );
            Assert.AreEqual( 1, people[0].Id );
        }

        [TestMethod]
        public void TestMapper_GetsATypedReaderOverAStream()
        {
            using var stream = Utf8( "Bob,1\r\n", withByteOrderMark: false );
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Id );

            var typedReader = mapper.GetReader( stream, Options );

            Assert.IsTrue( typedReader.Read() );
            Assert.AreEqual( "Bob", typedReader.Current.Name );
        }

        [TestMethod]
        public void TestAutoMapped_ReadsFromAStream()
        {
            using var stream = Utf8( "Name,Id\r\nBob,1\r\n", withByteOrderMark: true );

            var typedReader = DelimitedTypeMapper.GetAutoMappedReader<Person>( stream, Options );

            Assert.IsTrue( typedReader.Read() );
            Assert.AreEqual( "Bob", typedReader.Current.Name, "A mark left in the header would leave the first property unmatched." );
        }

        private static MemoryStream Utf8( string text, bool withByteOrderMark )
        {
            var encoding = new UTF8Encoding( withByteOrderMark );
            var bytes = withByteOrderMark
                ? encoding.GetPreamble().Concat( encoding.GetBytes( text ) ).ToArray()
                : encoding.GetBytes( text );
            return new MemoryStream( bytes );
        }

        private static DelimitedOptions Options => new() { RecordSeparator = "\r\n" };

        private static DelimitedSchema Schema()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "Name" ) );
            schema.AddColumn( new StringColumn( "Id" ) );
            return schema;
        }

        private static FixedLengthSchema FixedSchema()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "Name" ), new Window( 10 ) );
            schema.AddColumn( new StringColumn( "Id" ), new Window( 2 ) );
            return schema;
        }

        private static string[] Names( IReader reader )
        {
            var names = new System.Collections.Generic.List<string>();
            while (reader.Read())
            {
                names.Add( (string) reader.GetValues()[0]! );
            }
            return [.. names];
        }

        public class Person
        {
            public string Name { get; set; } = string.Empty;

            public int Id { get; set; }
        }
    }
}
