using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the quoting the delimited writer applies in place once a value's text is known.
    /// </summary>
    [TestClass]
    public class DelimitedWriterEscapingTester
    {
        private static DelimitedSchema Schema( int columns )
        {
            var schema = new DelimitedSchema();
            for (var index = 0; index != columns; ++index)
            {
                schema.AddColumn( new StringColumn( "col" + index ) );
            }
            return schema;
        }

        private static string Write( DelimitedOptions options, params object[] values )
        {
            options.RecordSeparator ??= "\n";
            var output = new StringWriter();
            var writer = new DelimitedWriter( output, Schema( values.Length ), options );
            writer.Write( values );
            return output.ToString();
        }

        [TestMethod]
        public void TestWrite_ValueContainsTheSeparator_IsQuoted()
        {
            Assert.AreEqual( "\"a,b\",c\n", Write( new DelimitedOptions(), "a,b", "c" ) );
        }

        [TestMethod]
        public void TestWrite_ValueContainsQuotes_IsQuotedWithTheQuotesDoubled()
        {
            Assert.AreEqual( "\"say \"\"hi\"\"\"\n", Write( new DelimitedOptions(), "say \"hi\"" ) );
        }

        [TestMethod]
        public void TestWrite_ValueStartsOrEndsWithWhitespace_IsQuoted()
        {
            Assert.AreEqual( "\" x\",\"y \",z\n", Write( new DelimitedOptions(), " x", "y ", "z" ) );
        }

        [TestMethod]
        public void TestWrite_ValueContainsTheRecordSeparator_IsQuoted()
        {
            Assert.AreEqual( "\"line1\nline2\"\n", Write( new DelimitedOptions(), "line1\nline2" ) );
        }

        [TestMethod]
        public void TestWrite_EmptyValue_IsLeftEmptyByDefault()
        {
            Assert.AreEqual( ",x\n", Write( new DelimitedOptions(), "", "x" ) );
        }

        [TestMethod]
        public void TestWrite_AlwaysQuote_QuotesEvenEmptyValues()
        {
            Assert.AreEqual( "\"\",\"x\"\n", Write( new DelimitedOptions { QuoteBehavior = QuoteBehavior.AlwaysQuote }, "", "x" ) );
        }

        [TestMethod]
        public void TestWrite_NeverQuote_LeavesTheSeparatorInPlace()
        {
            Assert.AreEqual( "a,b,c\n", Write( new DelimitedOptions { QuoteBehavior = QuoteBehavior.Never }, "a,b", "c" ) );
        }

        [TestMethod]
        public void TestWrite_MultiCharacterSeparator_IsDetectedAndUsed()
        {
            Assert.AreEqual( "\"a||b\"||c\n", Write( new DelimitedOptions { Separator = "||" }, "a||b", "c" ) );
        }

        [TestMethod]
        public void TestWrite_CustomQuoteCharacter_IsDoubled()
        {
            Assert.AreEqual( "'it''s'\n", Write( new DelimitedOptions { Quote = '\'' }, "it's" ) );
        }

        [TestMethod]
        public void TestWrite_LongValueWithQuotes_GrowsTheBufferAndEscapesCorrectly()
        {
            var left = new string( 'a', 300 );
            var right = new string( 'b', 300 );

            var written = Write( new DelimitedOptions(), left + "\"" + right );

            Assert.AreEqual( "\"" + left + "\"\"" + right + "\"\n", written );
        }

        [TestMethod]
        public void TestWrite_ManyColumns_AreAllSeparated()
        {
            var values = new object[100];
            for (var index = 0; index != values.Length; ++index)
            {
                values[index] = "value" + index;
            }

            var written = Write( new DelimitedOptions(), values );

            Assert.AreEqual( string.Join( ",", values ) + "\n", written );
        }

        [TestMethod]
        public void TestWriteSchema_ColumnNameNeedsQuoting_IsQuoted()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "first,name" ) );
            schema.AddColumn( new StringColumn( "b" ) );
            var output = new StringWriter();
            var writer = new DelimitedWriter( output, schema, new DelimitedOptions { RecordSeparator = "\n", IsFirstRecordSchema = true } );

            writer.Write( [ "x", "y" ] );

            Assert.AreEqual( "\"first,name\",b\nx,y\n", output.ToString() );
        }

        [TestMethod]
        public async Task TestWriteAsync_ProducesTheSameTextAsTheSynchronousWriter()
        {
            var options = new DelimitedOptions { RecordSeparator = "\n", IsFirstRecordSchema = true };
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "first,name" ) );
            schema.AddColumn( new Int32Column( "n" ) );
            var synchronous = new StringWriter();
            var asynchronous = new StringWriter();
            var writer = new DelimitedWriter( synchronous, schema, options );
            var asyncWriter = new DelimitedWriter( asynchronous, schema, options );
            object[] values = [ "a \"quoted\" value", 42 ];

            writer.Write( values );
            await asyncWriter.WriteAsync( values );

            Assert.AreEqual( "\"first,name\",n\n\"a \"\"quoted\"\" value\",42\n", synchronous.ToString() );
            Assert.AreEqual( synchronous.ToString(), asynchronous.ToString() );
        }
    }
}
