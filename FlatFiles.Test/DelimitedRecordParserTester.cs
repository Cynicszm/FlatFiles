using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the delimited tokeniser directly, with buffers small enough that records straddle every refill, so the
    ///     re-scan after a refill and the buffer growth are exercised alongside the ordinary paths.
    /// </summary>
    [TestClass]
    public class DelimitedRecordParserTester
    {
        private static readonly int[] BufferSizes = [ 1, 2, 3, 5, 8, CharBufferReader.DefaultBufferSize ];

        private static List<(string Record, string[] Values)> Parse( string text, DelimitedOptions options, int bufferSize )
        {
            var parser = new DelimitedRecordParser( new CharBufferReader( new StringReader( text ), bufferSize ), options );
            var records = new List<(string, string[])>();
            while (!parser.IsEndOfStream())
            {
                records.Add( parser.ReadRecord() );
            }
            return records;
        }

        private static async Task<List<(string Record, string[] Values)>> ParseAsync( string text, DelimitedOptions options, int bufferSize )
        {
            var parser = new DelimitedRecordParser( new CharBufferReader( new StringReader( text ), bufferSize ), options );
            var records = new List<(string, string[])>();
            while (!await parser.IsEndOfStreamAsync())
            {
                records.Add( await parser.ReadRecordAsync() );
            }
            return records;
        }

        private static void AssertValues( string text, DelimitedOptions options, params string[][] expected )
        {
            foreach (var size in BufferSizes)
            {
                var records = Parse( text, options ?? new DelimitedOptions(), size );
                Assert.AreEqual( expected.Length, records.Count, $"Record count with a buffer of {size}." );
                for (int index = 0; index != expected.Length; ++index)
                {
                    CollectionAssert.AreEqual( expected[index], records[index].Values, $"Record {index} with a buffer of {size}." );
                }
            }
        }

        [TestMethod]
        public void TestReadRecord_AnyLineBreak_EndsTheRecord()
        {
            AssertValues( "a,b\r\nc,d\nef,g\rh,i", null, [ "a", "b" ], [ "c", "d" ], [ "ef", "g" ], [ "h", "i" ] );
        }

        [TestMethod]
        public void TestReadRecord_NoTrailingLineBreak_ReadsTheLastRecord()
        {
            AssertValues( "a,b", null, [ "a", "b" ] );
            AssertValues( "a,b\r\n", null, [ "a", "b" ] );
        }

        [TestMethod]
        public void TestReadRecord_QuotedValue_KeepsSeparatorsAndLineBreaksAndUndoublesQuotes()
        {
            AssertValues( "\"x,\"\"y\"\"\r\nz\",1\r\n", null, [ "x,\"y\"\r\nz", "1" ] );
        }

        [TestMethod]
        public void TestReadRecord_QuoteInsideUnquotedValue_IsLiteral()
        {
            AssertValues( "a\"b,c", null, [ "a\"b", "c" ] );
        }

        [TestMethod]
        public void TestReadRecord_LeadingWhiteSpace_IsDroppedAndTrailingKept()
        {
            AssertValues( " a , b \r\n", null, [ "a ", "b " ] );
            AssertValues( " a , b \r\n", new DelimitedOptions { PreserveWhiteSpace = true }, [ " a ", " b " ] );
        }

        [TestMethod]
        public void TestReadRecord_WhiteSpaceOnlyValue_IsEmptyUnlessPreserved()
        {
            AssertValues( "a,  \r\n", null, [ "a", "" ] );
            AssertValues( "a,  \r\n", new DelimitedOptions { PreserveWhiteSpace = true }, [ "a", "  " ] );
        }

        [TestMethod]
        public void TestReadRecord_WhiteSpaceAroundQuotedValue_IsDroppedUnlessPreserved()
        {
            AssertValues( "  \"a\"  ,b", null, [ "a", "b" ] );
            AssertValues( "\"a\"  ,b", new DelimitedOptions { PreserveWhiteSpace = true }, [ "a  ", "b" ] );
            // With whitespace preserved the quote is no longer the first character, so the value is not quoted at all.
            AssertValues( "  \"a\",b", new DelimitedOptions { PreserveWhiteSpace = true }, [ "  \"a\"", "b" ] );
        }

        [TestMethod]
        public void TestReadRecord_UnmatchedQuote_Throws()
        {
            foreach (var size in BufferSizes)
            {
                Assert.ThrowsExactly<DelimitedSyntaxException>( () => Parse( "\"abc", new DelimitedOptions(), size ), $"Buffer of {size}." );
                Assert.ThrowsExactly<DelimitedSyntaxException>( () => Parse( "\"a\"x,b", new DelimitedOptions(), size ), $"Buffer of {size}." );
            }
        }

        [TestMethod]
        public void TestReadRecord_EmptyValuesAndBlankLines_AreReported()
        {
            AssertValues( "a,,\r\n\r\nb", null, [ "a", "", "" ], [ "" ], [ "b" ] );
        }

        [TestMethod]
        public void TestReadRecord_CustomSeparators_AreHonoured()
        {
            AssertValues( "a||b||c\nd", new DelimitedOptions { Separator = "||", RecordSeparator = "\n" }, [ "a", "b", "c" ], [ "d" ] );
            AssertValues( "a|b||c", new DelimitedOptions { Separator = "||" }, [ "a|b", "c" ] );
            AssertValues( "a\tb\r\nc\td", new DelimitedOptions { Separator = "\t" }, [ "a", "b" ], [ "c", "d" ] );
            AssertValues( "a,bENDc,dEND", new DelimitedOptions { RecordSeparator = "END" }, [ "a", "b" ], [ "c", "d" ] );
            AssertValues( "a,bENc,d", new DelimitedOptions { RecordSeparator = "END" }, [ "a", "bENc", "d" ] );
        }

        [TestMethod]
        public void TestReadRecord_SeparatorIsPrefixOfRecordSeparator_TellsThemApart()
        {
            AssertValues( "a;b;;c;d;;", new DelimitedOptions { Separator = ";", RecordSeparator = ";;" }, [ "a", "b" ], [ "c", "d" ] );
        }

        [TestMethod]
        public void TestReadRecord_ValueLargerThanTheBuffer_GrowsTheBuffer()
        {
            var plain = new string( 'p', 5000 );
            var quoted = new string( 'q', 2500 ) + "\"" + new string( 'r', 2500 );

            AssertValues( plain + ",\"" + quoted.Replace( "\"", "\"\"" ) + "\"\r\n", null, [ plain, quoted ] );
        }

        [TestMethod]
        public void TestReadRecord_RecordText_IsTheLineWithoutItsSeparator()
        {
            foreach (var size in BufferSizes)
            {
                var kept = Parse( "\"a,b\",c\r\nd,e", new DelimitedOptions { PreserveRecordText = true }, size );
                Assert.AreEqual( "\"a,b\",c", kept[0].Record, $"Buffer of {size}." );
                Assert.AreEqual( "d,e", kept[1].Record, $"Buffer of {size}." );

                var dropped = Parse( "\"a,b\",c\r\n", new DelimitedOptions(), size );
                Assert.AreEqual( "", dropped[0].Record, $"Buffer of {size}." );
            }
        }

        [TestMethod]
        public void TestIsEndOfStream_EmptyInput_IsTrueImmediately()
        {
            foreach (var size in BufferSizes)
            {
                Assert.AreEqual( 0, Parse( "", new DelimitedOptions(), size ).Count, $"Buffer of {size}." );
            }
        }

        [TestMethod]
        public async Task TestReadRecordAsync_MatchesTheSynchronousParser()
        {
            const string text = "a,\"b,\"\"c\"\"\r\n\",  d \r\n\r\ne,f\ng";
            var options = new DelimitedOptions { PreserveRecordText = true };
            foreach (var size in BufferSizes)
            {
                var synchronous = Parse( text, options, size );
                var asynchronous = await ParseAsync( text, options, size );

                Assert.AreEqual( synchronous.Count, asynchronous.Count, $"Buffer of {size}." );
                for (int index = 0; index != synchronous.Count; ++index)
                {
                    Assert.AreEqual( synchronous[index].Record, asynchronous[index].Record, $"Record {index} with a buffer of {size}." );
                    CollectionAssert.AreEqual( synchronous[index].Values, asynchronous[index].Values, $"Record {index} with a buffer of {size}." );
                }
            }
        }
    }
}
