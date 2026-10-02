using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Splitting one record's text into its values, for a caller who has the text already.
    /// </summary>
    /// <remarks>
    ///     The parser that knows how to do this is internal, so the only way to split a line without writing a
    ///     quoting rule by hand was to stand a schemaless reader up around each one.
    /// </remarks>
    [TestClass]
    public class ParseRecordTester
    {
        [TestMethod]
        public void TestPlainRecord()
        {
            Assert.AreEqual( "a|b|c", string.Join( "|", DelimitedReader.ParseRecord( "a,b,c" ) ) );
        }

        /// <summary>The whole point: a separator inside a quoted value belongs to the value.</summary>
        [TestMethod]
        public void TestSeparatorInsideQuotes_BelongsToTheValue()
        {
            Assert.AreEqual( "a|b,c|d", string.Join( "|", DelimitedReader.ParseRecord( "a,\"b,c\",d" ) ) );
        }

        /// <summary>
        ///     A record separator inside a quoted value belongs to the value too, which is why this reads a record
        ///     rather than a line: what looks like two lines is one record.
        /// </summary>
        [TestMethod]
        public void TestRecordSeparatorInsideQuotes_IsOneRecord()
        {
            var text = "a,\"b" + Environment.NewLine + "c\",d";
            var values = DelimitedReader.ParseRecord( text );

            Assert.HasCount( 3, values );
            Assert.AreEqual( "b" + Environment.NewLine + "c", values[1] );
        }

        [TestMethod]
        public void TestTrailingRecordSeparator_IsNotASecondRecord()
        {
            Assert.AreEqual( "a|b",
                string.Join( "|", DelimitedReader.ParseRecord( "a,b" + Environment.NewLine ) ) );
        }

        /// <summary>
        ///     Refused rather than quietly answered with the first, because a caller who thought they had one
        ///     record and did not should hear about it.
        /// </summary>
        [TestMethod]
        public void TestMoreThanOneRecord_IsRefused()
        {
            var text = "a,b" + Environment.NewLine + "c,d";
            Assert.ThrowsExactly<ArgumentException>( () => DelimitedReader.ParseRecord( text ) );
        }

        [TestMethod]
        public void TestNoRecordAtAll_IsEmpty()
        {
            Assert.IsEmpty( DelimitedReader.ParseRecord( string.Empty ) );
        }

        /// <summary>A record of empty values is a record, and is not the same as no record.</summary>
        [TestMethod]
        public void TestEmptyValues_AreValues()
        {
            Assert.AreEqual( "|", string.Join( "|", DelimitedReader.ParseRecord( "," ) ) );
        }

        [TestMethod]
        public void TestUnreadableRecord_Raises()
        {
            Assert.ThrowsExactly<DelimitedSyntaxException>( () => DelimitedReader.ParseRecord( "a,\"b" ) );
        }

        [TestMethod]
        public void TestOptionsAreHonoured()
        {
            var options = new DelimitedOptions { Separator = "\t", Quote = '\'' };
            Assert.AreEqual( "a|b\tc|d",
                string.Join( "|", DelimitedReader.ParseRecord( "a\t'b\tc'\td", options ) ) );
        }

        [TestMethod]
        public void TestNullRecord_Raises()
        {
            Assert.ThrowsExactly<ArgumentNullException>( () => DelimitedReader.ParseRecord( null! ) );
        }
    }
}
