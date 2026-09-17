using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    [TestClass]
    public class FixedLengthWriterTester
    {
        [TestMethod]
        public void ShouldUseLeadingTruncationByDefault()
        {
            var options = new FixedLengthOptions();
            Assert.AreEqual( OverflowTruncationPolicy.TruncateLeading, options.TruncationPolicy );
        }

        [TestMethod]
        public void ShouldTruncateOverflow()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "Default" ), new Window( 5 ) );
            schema.AddColumn( new StringColumn( "Leading" ), new Window( 5 ) { TruncationPolicy = OverflowTruncationPolicy.TruncateLeading } );
            schema.AddColumn( new StringColumn( "Trailing" ), new Window( 5 ) { TruncationPolicy = OverflowTruncationPolicy.TruncateTrailing } );
            var options = new FixedLengthOptions
            {
                TruncationPolicy = OverflowTruncationPolicy.TruncateLeading // this is the default anyway
            };

            var stringWriter = new StringWriter();
            var writer = new FixedLengthWriter( stringWriter, schema, options );
            writer.Write( [ "Pineapple", "Pineapple", "Pineapple" ] );

            var output = stringWriter.ToString();
            
            var expected = "appleapplePinea" + Environment.NewLine;
            Assert.AreEqual( expected, output );
        }

        [TestMethod]
        public void ShouldWriteHeader()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "First" ), new Window( 10 ) { FillCharacter = '@' } );
            schema.AddColumn( new StringColumn( "Second" ), new Window( 10 ) { FillCharacter = '!' } );
            schema.AddColumn( new StringColumn( "Third" ), new Window( 10 ) { FillCharacter = '$' } );
            var options = new FixedLengthOptions { IsFirstRecordHeader = true };

            var stringWriter = new StringWriter();
            var writer = new FixedLengthWriter( stringWriter, schema, options );
            writer.Write( [ "Apple", "Grape", "Pear" ] );

            var output = stringWriter.ToString();

            var expected = "First@@@@@Second!!!!Third$$$$$" 
                + Environment.NewLine 
                + "Apple@@@@@Grape!!!!!Pear$$$$$$"
                + Environment.NewLine;
            Assert.AreEqual( expected, output );
        }

        [TestMethod]
        public void ShouldWriteHeader_IgnoredColumns()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "First" ), new Window( 10 ) { FillCharacter = '@' } );
            schema.AddColumn( new IgnoredColumn(), new Window( 1 ) { FillCharacter = '|' } );
            schema.AddColumn( new StringColumn( "Second" ), new Window( 10 ) { FillCharacter = '!' } );
            schema.AddColumn( new IgnoredColumn(), new Window( 1 ) { FillCharacter = '|' } );
            schema.AddColumn( new StringColumn( "Third" ), new Window( 10 ) { FillCharacter = '$' } );
            var options = new FixedLengthOptions { IsFirstRecordHeader = true };

            var stringWriter = new StringWriter();
            var writer = new FixedLengthWriter( stringWriter, schema, options );
            writer.Write( [ "Apple", "Grape", "Pear" ] );

            var output = stringWriter.ToString();

            var expected = "First@@@@@|Second!!!!|Third$$$$$"
                + Environment.NewLine
                + "Apple@@@@@|Grape!!!!!|Pear$$$$$$"
                + Environment.NewLine;
            Assert.AreEqual( expected, output );
        }

        [TestMethod]
        public void ShouldWriteHeader_NoRecordSeparator()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "First" ), new Window( 10 ) { FillCharacter = '@' } );
            schema.AddColumn( new StringColumn( "Second" ), new Window( 10 ) { FillCharacter = '!' } );
            schema.AddColumn( new StringColumn( "Third" ), new Window( 10 ) { FillCharacter = '$' } );
            var options = new FixedLengthOptions { IsFirstRecordHeader = true, HasRecordSeparator = false };

            var stringWriter = new StringWriter();
            var writer = new FixedLengthWriter( stringWriter, schema, options );
            writer.Write( [ "Apple", "Grape", "Pear" ] );

            var output = stringWriter.ToString();

            const string expected = "First@@@@@Second!!!!Third$$$$$Apple@@@@@Grape!!!!!Pear$$$$$$";
            Assert.AreEqual( expected, output );
        }

        [TestMethod]
        public void ShouldHandleNullValues()
        {

            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "NullableInt32" ), new Window( 5 ) );

            var stringWriter = new StringWriter();
            var writer = new FixedLengthWriter( stringWriter, schema );
            writer.Write( [ null ] );

            var output = stringWriter.ToString();
            var expected = "     " + Environment.NewLine;
            Assert.AreEqual( expected, output );
        }

        [TestMethod]
        public void ShouldWriteSchemaIfExplicit()
        {
            var stringWriter = new StringWriter();
            // Explicitly indicate that the first record is NOT the schema
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "Col1" ), 10 );
            var writer = new FixedLengthWriter( stringWriter, schema, new FixedLengthOptions
            {
                IsFirstRecordHeader = false
            } );
            writer.WriteSchema();  // Explicitly write the schema
            writer.Write( [ "a" ] );

            var stringReader = new StringReader( stringWriter.ToString() );
            var reader = new FixedLengthReader( stringReader, schema, new FixedLengthOptions { IsFirstRecordHeader = true } );

            Assert.IsTrue( reader.Read(), "The record was not retrieved after the schema." );
            Assert.IsFalse( reader.Read(), "Encountered more than the expected number of records." );
        }
    }
}
