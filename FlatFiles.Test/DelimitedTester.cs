using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    [TestClass]
    public class DelimitedTester
    {
        [TestMethod]
        public void ShouldNotFindRecordsInEmptyFile()
        {
            var source = string.Empty;
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions { IsFirstRecordSchema = false };
            var reader = new DelimitedReader( stringReader, options );
            Assert.IsFalse( reader.Read(), "No records should be read from an empty file." );
        }

        [TestMethod]
        public void ShouldFindOneRecordOneColumn_CharactersFollowedByEndOfStream()
        {
            const string source = "Hello";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions { IsFirstRecordSchema = false };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "Hello" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldFindOneRecordTwoColumn_CharactersFollowedByEndOfStream()
        {
            const string source = "Hello,World";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions { IsFirstRecordSchema = false };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "Hello", "World" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldFindTwoRecordsOneColumn()
        {
            const string source = "Hello\r\nWorld\r\n";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions { IsFirstRecordSchema = false };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "Hello" ],
                [ "World" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldFindTwoRecordsOneColumn_MissingClosingRecordSeparator()
        {
            const string source = "Hello\r\nWorld";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions { IsFirstRecordSchema = false };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "Hello" ],
                [ "World" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldFindOneRecordTwoColumn_EOROverlapsEOT()
        {
            const string source = "a\rb\r\n";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions 
            { 
                IsFirstRecordSchema = false, 
                RecordSeparator = "\r\n", 
                Separator = "\r" 
            };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "a", "b" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldFindTwoRecordsOneColumn_EOROverlapsEOT()
        {
            const string source = "a\r\nb";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions 
            { 
                IsFirstRecordSchema = false, 
                RecordSeparator = "\r\n", 
                Separator = "\r" 
            };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "a" ],
                [ "b" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldFindOneRecordTwoColumn_EOTOverlapsEOR()
        {
            const string source = "a\r\nb\r";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions 
            { 
                IsFirstRecordSchema = false, 
                RecordSeparator = "\r", 
                Separator = "\r\n" 
            };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "a", "b" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldFindTwoRecordsOneColumn_EOTOverlapsEOR()
        {
            const string source = "a\rb";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions 
            { 
                IsFirstRecordSchema = false, 
                RecordSeparator = "\r", 
                Separator = "\r\n" 
            };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "a" ],
                [ "b" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldSeparateBlanks()
        {
            const string source = ",";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions { IsFirstRecordSchema = false };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ null, null ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldSeparateBlanksAcrossRecords()
        {
            const string source = ",,\r\n,,";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions { IsFirstRecordSchema = false };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ null, null, null ],
                [ null, null, null ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldHandleSingleEmptyRecord()
        {
            const string source = "\r\n";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions { IsFirstRecordSchema = false };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ null ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldHandleMultipleEmptyRecords()
        {
            const string source = "\r\n\r\n";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions { IsFirstRecordSchema = false };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ null ],
                [ null ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldStripLeadingWhitespace()
        {
            const string source = " a";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions { IsFirstRecordSchema = false };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "a" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldPreserveLeadingWhitespaceIfConfigured()
        {
            const string source = " a";
            var stringReader = new StringReader( source );
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) { Trim = false } );
            var options = new DelimitedOptions 
            { 
                IsFirstRecordSchema = false, 
                PreserveWhiteSpace = true 
            };
            var reader = new DelimitedReader( stringReader, schema, options );
            object[][] expected =
            [
                [ " a" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldStripLeadingWhitespace_MultipleSpaces_TwoColumn()
        {
            const string source = "  a, \t\n  b";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = false,
                RecordSeparator = "\r\n"
            };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "a", "b" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldPreserveLeadingWhitespaceIfConfigured_MultipleSpaces_TwoColumn()
        {
            const string source = "  a, \t\n  b";
            var stringReader = new StringReader( source );
            //DelimitedSchema schema = new DelimitedSchema();
            //schema.AddColumn(new StringColumn("a") { Trim = false });
            //schema.AddColumn(new StringColumn("b") { Trim = false });
            var options = new DelimitedOptions 
            { 
                IsFirstRecordSchema = false, 
                RecordSeparator = "\r\n", 
                PreserveWhiteSpace = true 
            };
            var reader = new DelimitedReader( stringReader, /*schema,*/ options );
            object[][] expected =
            [
                [ "  a", " \t\n  b" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldStripTrailingWhitespace()
        {
            const string source = "a ";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions { IsFirstRecordSchema = false };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "a" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldPreserveTrailingWhitespaceIfConfigured()
        {
            const string source = "a ";
            var stringReader = new StringReader( source );
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) { Trim = false } );
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = false, 
                PreserveWhiteSpace = true 
            };
            var reader = new DelimitedReader( stringReader, schema, options );
            object[][] expected =
            [
                [ "a " ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldStripTrailingWhitespace_MultipleSpaces_TwoColumn()
        {
            const string source = "a  ,b \t\n  ";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = false,
                RecordSeparator = "\r\n"
            };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "a", "b" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldPreserveTrailingWhitespaceIfConfigured_MultipleSpaces_TwoColumn()
        {
            const string source = "a  ,b \t\n  ";
            var stringReader = new StringReader( source );
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) { Trim = false } );
            schema.AddColumn( new StringColumn( "b" ) { Trim = false } );
            var options = new DelimitedOptions { IsFirstRecordSchema = false, RecordSeparator = "\r\n", PreserveWhiteSpace = true };
            var reader = new DelimitedReader( stringReader, schema, options );
            object[][] expected =
            [
                [ "a  ", "b \t\n  " ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldStripLeadingAndTrailingWhitespace()
        {
            const string source = " a ";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions { IsFirstRecordSchema = false };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "a" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldPreserveLeadingAndTrailingWhitespaceIfConfigured()
        {
            const string source = " a ";
            var stringReader = new StringReader( source );
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) { Trim = false } );
            var options = new DelimitedOptions { IsFirstRecordSchema = false, PreserveWhiteSpace = true };
            var reader = new DelimitedReader( stringReader, schema, options );
            object[][] expected =
            [
                [ " a " ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldStripLeadingAndTrailingWhitespace_MultipleSpaces_TwoColumn()
        {
            const string source = "  a  , \t\n  b \t\n  ";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions { IsFirstRecordSchema = false, RecordSeparator = "\r\n" };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "a", "b" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldPreserveLeadingAndTrailingWhitespaceIfConfigured_MultipleSpaces_TwoColumn()
        {
            const string source = "  a  , \t\n  b \t\n  ";
            var stringReader = new StringReader( source );
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) { Trim = false } );
            schema.AddColumn( new StringColumn( "b" ) { Trim = false } );
            var options = new DelimitedOptions 
            { 
                IsFirstRecordSchema = false, 
                RecordSeparator = "\r\n", 
                PreserveWhiteSpace = true 
            };
            var reader = new DelimitedReader( stringReader, schema, options );
            object[][] expected =
            [
                [ "  a  ", " \t\n  b \t\n  " ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldFindOneColumnOneRecordIfAllWhitespace()
        {
            const string source = " \t\n\r ";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions { IsFirstRecordSchema = false, RecordSeparator = "\r\n" };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ null ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldPreserveWhiteSpaceIfConfigured_AllWhitespace()
        {
            const string source = " \t\n\r ";
            var stringReader = new StringReader( source );
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) { Trim = false, NullFormatter = NullFormatter.ForValue( null ) } );
            var options = new DelimitedOptions 
            {
                IsFirstRecordSchema = false, 
                RecordSeparator = "\r\n", 
                PreserveWhiteSpace = true 
            };
            var reader = new DelimitedReader( stringReader, schema, options );
            object[][] expected =
            [
                [ " \t\n\r " ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldNotStripEmbeddedWhitespace()
        {
            const string source = " a b ";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions { IsFirstRecordSchema = false };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "a b" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldNotStripEmbeddedWhitespace_PreservingWhiteSpace()
        {
            const string source = " a b ";
            var stringReader = new StringReader( source );
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) { Trim = false } );
            var options = new DelimitedOptions { IsFirstRecordSchema = false, PreserveWhiteSpace = true };
            var reader = new DelimitedReader( stringReader, schema, options );
            object[][] expected =
            [
                [ " a b " ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldHandleDoubleWhitespaceAsSeparator()
        {
            const string source = " a  b ";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions { IsFirstRecordSchema = false, Separator = "  " };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "a", "b" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldIgnoreInvalidSeparatorSharingSharedPrefix()
        {
            const string source = "axxcb";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions 
            { 
                IsFirstRecordSchema = false, 
                Separator = "xxa", 
                RecordSeparator = "xxb" 
            };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "axxcb" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldHandleLongUndoOperation()
        {
            const string source = "axxxb";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions { IsFirstRecordSchema = false, Separator = "xxxx" };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "axxxb" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldExtractQuotedValue()
        {
            const string source = "'a'";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = false,
                Quote = '\''
            };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "a" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldIncludeSpacesBetweenQuotes()
        {
            const string source = "' a  '";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = false,
                Quote = '\''
            };
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) { Trim = false } );
            var reader = new DelimitedReader( stringReader, schema, options );
            object[][] expected =
            [
                [ " a  " ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldIgnoreSeparatorsBetweenQuotes()
        {
            const string source = "'a,b'";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = false,
                Quote = '\''
            };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "a,b" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldHandleEscapedQuotesWithinQuotes()
        {
            const string source = "'a''b'";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = false,
                Quote = '\''
            };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "a'b" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldIgnoreLeadingWhiteSpaceBeforeQuote()
        {
            const string source = "   'a'";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = false,
                Quote = '\''
            };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "a" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldIgnoreTrailingWhiteSpaceAfterQuote()
        {
            const string source = "'a' ";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = false,
                Quote = '\''
            };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "a" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldThrowSyntaxExceptionIfQuoteFollowedByEOS()
        {
            const string source = "'";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = false,
                Quote = '\''
            };
            var reader = new DelimitedReader( stringReader, options );
            Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );
        }

        [TestMethod]
        public void ShouldThrowSyntaxExceptionIfQuoteFollowedByNonSeparator()
        {
            const string source = "'a'b";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = false,
                Quote = '\''
            };
            var reader = new DelimitedReader( stringReader, options );
            Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );
        }

        [TestMethod]
        public void ShouldIgnoreRecordSeparatorsWithinQuotes()
        {
            const string source = @"'John','Smith','123 Playtown Place', 'Grangewood','CA' ,12345,'John likes to travel to far away places.
His favorite travel spots are Tannis, Venice and Chicago.
When he''s not traveling, he''s at home with his lovely wife, children and leather armchair.'
Mary,Smith,'1821 Grover''s Village',West Chattingham,WA,43221,'Likes cats.'";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = false,
                Quote = '\''
            };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                new object[] 
                { "John", "Smith", "123 Playtown Place", "Grangewood", "CA", "12345", @"John likes to travel to far away places.
His favorite travel spots are Tannis, Venice and Chicago.
When he's not traveling, he's at home with his lovely wife, children and leather armchair."
                },
                [ "Mary", "Smith", "1821 Grover's Village", "West Chattingham", "WA", "43221", "Likes cats." ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldHandleSeparatorAfterQuoteIfPreservingWhiteSpace()
        {
            const string source = "26087,C Country C,,1,3,7,Randy E,(555) 555-5500,,\"P.O.Box 60,\",,,Woodsland,CA,56281,,,,0292315c-0daa-df11-9397-0019b9e7d4cd,,0,8713cbdd-fb50-dc11-a545-000423c05bf1,40,79527,,False";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = false,
                PreserveWhiteSpace = true
            };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [
                    "26087",
                    "C Country C",
                    null,
                    "1",
                    "3",
                    "7",
                    "Randy E",
                    "(555) 555-5500",
                    null,
                    "P.O.Box 60,",
                    null,
                    null,
                    "Woodsland",
                    "CA",
                    "56281",
                    null,
                    null,
                    null,
                    "0292315c-0daa-df11-9397-0019b9e7d4cd",
                    null,
                    "0",
                    "8713cbdd-fb50-dc11-a545-000423c05bf1",
                    "40",
                    "79527",
                    null,
                    "False" 
                ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldHandleEmbeddedQuotes()
        {
            const string source = "x\ty\tabc\"def\tz";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = false,
                Separator = "\t"
            };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [ "x", "y", "abc\"def", "z" ]
            ];
            AssertRecords( expected, reader );
        }

        [TestMethod]
        public void ShouldHandleEmbeddedQuotes2()
        {
            const string source = "DebtConversionConvertedInstrumentAmount1\tus-gaap/2019" +
                "\t0\t0\tmonetary\tD\tC\tDebt Conversion, Converted Instrument, Amount" +
                "\tThe value of the financial instrument(s) that the original debt is being converted into in a noncash (or part noncash) transaction. \"Part noncash refers to that portion of the transaction not resulting in cash receipts or cash payments in the period.";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = false,
                Separator = "\t",
                PreserveWhiteSpace = false,
                QuoteBehavior = QuoteBehavior.Never
            };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [
                    "DebtConversionConvertedInstrumentAmount1",
                    "us-gaap/2019", 
                    "0", 
                    "0",
                    "monetary",
                    "D",
                    "C",
                    "Debt Conversion, Converted Instrument, Amount",
                    "The value of the financial instrument(s) that the original debt is being converted into in a noncash (or part noncash) transaction. \"Part noncash refers to that portion of the transaction not resulting in cash receipts or cash payments in the period."
                ]
            ];
            AssertRecords( expected, reader );
        }

        //[TestMethod]
#pragma warning disable CA1822 // Mark members as static
        public void ShouldHandleNonTerminatingEmbeddedQuotes()
#pragma warning restore CA1822 // Mark members as static
        {
            const string source = "This\tis\t\"not\"the\tend\"\tof\tthe\tmessage";
            var stringReader = new StringReader( source );
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = false,
                Separator = "\t"
            };
            var reader = new DelimitedReader( stringReader, options );
            object[][] expected =
            [
                [
                    "This", "is", "not\"the\tend", "of", "the", "message"
                ]
            ];
            AssertRecords( expected, reader );
        }

        private static void AssertRecords( object[][] expected, DelimitedReader reader )
        {
            for (var recordIndex = 0; recordIndex != expected.Length; ++recordIndex)
            {
                Assert.IsTrue( reader.Read(), $"The record could not be read (Record {recordIndex})." );
                var actualValues = reader.GetValues();
                var expectedValues = expected[recordIndex];
                AssertRecord( expectedValues, actualValues );
            }
            Assert.IsFalse( reader.Read(), "There were more records read than expected." );
        }

        private static void AssertRecord( object[] expected, object[] actual )
        {
            CollectionAssert.AreEqual( expected, actual );
        }
    }
}
