using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the fixed-length record reader directly, with buffers small enough that records straddle every refill.
    /// </summary>
    [TestClass]
    public class FixedLengthRecordParserTester
    {
        private static readonly int[] BufferSizes = [ 1, 2, 3, 5, 8, CharBufferReader.DefaultBufferSize ];

        private static List<string> Parse( string text, FixedLengthOptions options, int bufferSize )
        {
            var parser = new FixedLengthRecordParser( new StringReader( text ), null, options, bufferSize );
            List<string> records = [];
            while (!parser.IsEndOfStream())
            {
                records.Add( parser.ReadRecord() );
            }
            return records;
        }

        private static async Task<List<string>> ParseAsync( string text, FixedLengthOptions options, int bufferSize )
        {
            var parser = new FixedLengthRecordParser( new StringReader( text ), null, options, bufferSize );
            List<string> records = [];
            while (!await parser.IsEndOfStreamAsync())
            {
                records.Add( await parser.ReadRecordAsync() );
            }
            return records;
        }

        private static void AssertRecords( string text, FixedLengthOptions options, params string[] expected )
        {
            foreach (var size in BufferSizes)
            {
                CollectionAssert.AreEqual( expected, Parse( text, options ?? new FixedLengthOptions(), size ), $"Buffer of {size}." );
            }
        }

        [TestMethod]
        public void TestReadRecord_AnyLineBreak_EndsTheRecord()
        {
            AssertRecords( "abc\r\ndef\nghi\rjkl", null, "abc", "def", "ghi", "jkl" );
        }

        [TestMethod]
        public void TestReadRecord_TrailingLineBreak_DoesNotProduceAnEmptyRecord()
        {
            AssertRecords( "abc\r\n", null, "abc" );
        }

        [TestMethod]
        public void TestReadRecord_BlankLine_IsAnEmptyRecord()
        {
            AssertRecords( "abc\r\n\r\ndef", null, "abc", "", "def" );
        }

        [TestMethod]
        public void TestReadRecord_CustomSeparator_IsHonouredAndPartialMatchesAreNot()
        {
            AssertRecords( "aEbENDcENDEND", new FixedLengthOptions { RecordSeparator = "END" }, "aEb", "c", "" );
        }

        [TestMethod]
        public void TestReadRecord_RecordLargerThanTheBuffer_GrowsTheBuffer()
        {
            var record = new string( 'x', 5000 );

            AssertRecords( record + "\r\n" + record, null, record, record );
        }

        [TestMethod]
        public void TestReadRecord_NoRecordSeparator_ReadsByTheSchemaWidth()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "a" ), new Window( 3 ) );
            var parser = new FixedLengthRecordParser( new StringReader( "abcdef" ), schema, new FixedLengthOptions { HasRecordSeparator = false } );

            Assert.IsFalse( parser.IsEndOfStream() );
            Assert.AreEqual( "abc", parser.ReadRecord() );
            Assert.IsFalse( parser.IsEndOfStream() );
            Assert.AreEqual( "def", parser.ReadRecord() );
            Assert.IsTrue( parser.IsEndOfStream() );
        }

        [TestMethod]
        public void TestIsEndOfStream_EmptyInput_IsTrueImmediately()
        {
            AssertRecords( "", null );
        }

        [TestMethod]
        public async Task TestReadRecordAsync_MatchesTheSynchronousParser()
        {
            const string text = "abc\r\n\r\ndef\nghi\rjkl\r\n";
            foreach (var size in BufferSizes)
            {
                CollectionAssert.AreEqual( Parse( text, new FixedLengthOptions(), size ), await ParseAsync( text, new FixedLengthOptions(), size ), $"Buffer of {size}." );
            }
        }
    }
}
