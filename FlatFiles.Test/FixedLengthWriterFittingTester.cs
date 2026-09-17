using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the padding and truncation the fixed-length writer applies in place once a value's text is known.
    /// </summary>
    [TestClass]
    public class FixedLengthWriterFittingTester
    {
        private static string Write( FixedLengthSchema schema, FixedLengthOptions options, params object[] values )
        {
            options.HasRecordSeparator = false;
            var output = new StringWriter();
            var writer = new FixedLengthWriter( output, schema, options );
            writer.Write( values );
            return output.ToString();
        }

        private static FixedLengthSchema Schema( params Window[] windows )
        {
            var schema = new FixedLengthSchema();
            for (var index = 0; index != windows.Length; ++index)
            {
                schema.AddColumn( new StringColumn( "col" + index ), windows[index] );
            }
            return schema;
        }

        [TestMethod]
        public void TestWrite_ShortValueLeftAligned_IsPaddedOnTheRightWithTheWindowFill()
        {
            var schema = Schema( new Window( 6 ) { FillCharacter = '.' } );

            Assert.AreEqual( "ab....", Write( schema, new FixedLengthOptions(), "ab" ) );
        }

        [TestMethod]
        public void TestWrite_ShortValueRightAligned_IsPaddedOnTheLeftWithTheOptionsFill()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "n" ), new Window( 5 ) );
            var options = new FixedLengthOptions { Alignment = FixedAlignment.RightAligned, FillCharacter = '0' };

            Assert.AreEqual( "00042", Write( schema, options, 42 ) );
        }

        [TestMethod]
        public void TestWrite_LongValue_KeepsTheEndByDefault()
        {
            Assert.AreEqual( "fgh", Write( Schema( new Window( 3 ) ), new FixedLengthOptions(), "abcdefgh" ) );
        }

        [TestMethod]
        public void TestWrite_LongValueWithTrailingTruncation_KeepsTheStart()
        {
            var schema = Schema( new Window( 3 ) { TruncationPolicy = OverflowTruncationPolicy.TruncateTrailing } );

            Assert.AreEqual( "abc", Write( schema, new FixedLengthOptions(), "abcdefgh" ) );
        }

        [TestMethod]
        public void TestWrite_LongValueWithLeadingTruncationOnTheWindow_KeepsTheEnd()
        {
            var schema = Schema( new Window( 3 ) { TruncationPolicy = OverflowTruncationPolicy.TruncateLeading } );

            Assert.AreEqual( "fgh", Write( schema, new FixedLengthOptions(), "abcdefgh" ) );
        }

        [TestMethod]
        public void TestWrite_LongValueWithLeadingTruncationInTheOptions_KeepsTheEnd()
        {
            var options = new FixedLengthOptions { TruncationPolicy = OverflowTruncationPolicy.TruncateLeading };

            Assert.AreEqual( "fgh", Write( Schema( new Window( 3 ) ), options, "abcdefgh" ) );
        }

        [TestMethod]
        public void TestWrite_LongValueWithThrowPolicy_Throws()
        {
            var schema = Schema( new Window( 3 ) { TruncationPolicy = OverflowTruncationPolicy.ThrowException } );

            var exception = Assert.ThrowsExactly<RecordProcessingException>( () => Write( schema, new FixedLengthOptions(), "abcdefgh" ) );

            Assert.IsInstanceOfType<FlatFileException>( exception.InnerException );
        }

        [TestMethod]
        public void TestWrite_ValueExactlyFillsTheWindow_IsUnchanged()
        {
            Assert.AreEqual( "abc", Write( Schema( new Window( 3 ) ), new FixedLengthOptions(), "abc" ) );
        }

        [TestMethod]
        public void TestWrite_SeveralColumns_AreFittedIndependently()
        {
            var schema = Schema(
                new Window( 6 ) { FillCharacter = '.' },
                new Window( 5 ) { Alignment = FixedAlignment.RightAligned, FillCharacter = '0' },
                new Window( 3 ) { TruncationPolicy = OverflowTruncationPolicy.TruncateLeading } );

            Assert.AreEqual( "ab....00042fgh", Write( schema, new FixedLengthOptions(), "ab", "42", "abcdefgh" ) );
        }

        [TestMethod]
        public void TestWrite_LongValueRightAligned_GrowsTheBufferAndPadsCorrectly()
        {
            var schema = Schema( new Window( 600 ) { Alignment = FixedAlignment.RightAligned } );
            var value = new string( 'x', 500 );

            Assert.AreEqual( new string( ' ', 100 ) + value, Write( schema, new FixedLengthOptions(), value ) );
        }

        [TestMethod]
        public void TestWrite_HeaderRecord_IsFittedToTheWindows()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "Id" ), new Window( 4 ) );
            schema.AddColumn( new StringColumn( "Name" ), new Window( 2 ) );
            var options = new FixedLengthOptions { IsFirstRecordHeader = true, RecordSeparator = "\n" };
            var output = new StringWriter();
            var writer = new FixedLengthWriter( output, schema, options );

            writer.Write( [ 7, "Bob" ] );

            // The header is fitted like any record; the default truncation policy keeps the end of a long name.
            Assert.AreEqual( "Id  me\n7   ob\n", output.ToString() );
        }

        [TestMethod]
        public async Task TestWriteAsync_ProducesTheSameTextAsTheSynchronousWriter()
        {
            var schema = Schema(
                new Window( 6 ) { FillCharacter = '.' },
                new Window( 5 ) { Alignment = FixedAlignment.RightAligned, FillCharacter = '0' } );
            var options = new FixedLengthOptions { IsFirstRecordHeader = true, RecordSeparator = "\n" };
            var synchronous = new StringWriter();
            var asynchronous = new StringWriter();
            var writer = new FixedLengthWriter( synchronous, schema, options );
            var asyncWriter = new FixedLengthWriter( asynchronous, schema, options );
            object[] values = [ "ab", "42" ];

            writer.Write( values );
            await asyncWriter.WriteAsync( values );

            Assert.AreEqual( "col0..0col1\nab....00042\n", synchronous.ToString() );
            Assert.AreEqual( synchronous.ToString(), asynchronous.ToString() );
        }
    }
}
