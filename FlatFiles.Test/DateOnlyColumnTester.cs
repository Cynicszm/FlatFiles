using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the DateOnlyColumn class: a calendar date with no time of day, parsed and formatted through the
    ///     column's own format and provider, then the options' provider, then the current culture.
    /// </summary>
    [TestClass]
    public class DateOnlyColumnTester
    {
        [TestMethod]
        public void TestCtor_NameBlank_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>( () => new DateOnlyColumn( "    " ) );
        }

        [TestMethod]
        public void TestCtor_SetsName_Trimmed()
        {
            var column = new DateOnlyColumn( " Born   " );

            Assert.AreEqual( "Born", column.ColumnName );
            Assert.AreEqual( typeof( DateOnly ), column.ColumnType );
        }

        [TestMethod]
        public void TestParse_NoFormatString_ParsesGenerically()
        {
            var column = new DateOnlyColumn( "born" ) { FormatProvider = CultureInfo.InvariantCulture };

            Assert.AreEqual( new DateOnly( 2013, 1, 19 ), column.Parse( null, "01/19/2013" ) );
            Assert.AreEqual( new DateOnly( 2013, 1, 19 ), column.Parse( null, "2013-01-19" ) );
        }

        [TestMethod]
        public void TestParse_FormatString_ParsesExactly()
        {
            var column = new DateOnlyColumn( "born" ) { InputFormat = "yyyyMMdd", FormatProvider = CultureInfo.InvariantCulture };

            Assert.AreEqual( new DateOnly( 2013, 1, 19 ), column.Parse( null, "20130119" ) );
            Assert.ThrowsExactly<FormatException>( () => column.Parse( null, "2013-01-19" ), "An exact format admits nothing else." );
        }

        [TestMethod]
        public void TestParse_FormatProvider_SteersTheDayMonthOrder()
        {
            var british = new DateOnlyColumn( "born" ) { FormatProvider = CultureInfo.GetCultureInfo( "en-GB" ) };
            var american = new DateOnlyColumn( "born" ) { FormatProvider = CultureInfo.GetCultureInfo( "en-US" ) };

            Assert.AreEqual( new DateOnly( 2013, 3, 4 ), british.Parse( null, "04/03/2013" ) );
            Assert.AreEqual( new DateOnly( 2013, 4, 3 ), american.Parse( null, "04/03/2013" ) );
        }

        [TestMethod]
        public void TestParse_ValueWithATime_Throws()
        {
            var column = new DateOnlyColumn( "born" ) { InputFormat = "yyyy-MM-dd", FormatProvider = CultureInfo.InvariantCulture };

            Assert.ThrowsExactly<FormatException>( () => column.Parse( null, "2013-01-19 10:30" ) );
        }

        [TestMethod]
        public void TestParse_ValueBlank_NullReturned()
        {
            var column = new DateOnlyColumn( "born" );

            Assert.IsNull( column.Parse( null, "    " ) );
        }

        [TestMethod]
        public void TestFormat_NoFormatString_UsesTheShortDatePattern()
        {
            var column = new DateOnlyColumn( "born" ) { FormatProvider = CultureInfo.InvariantCulture };

            Assert.AreEqual( "01/19/2013", column.Format( null, new DateOnly( 2013, 1, 19 ) ) );
        }

        [TestMethod]
        public void TestFormat_FormatStringAndProvider_Steer()
        {
            var column = new DateOnlyColumn( "born" ) { OutputFormat = "d MMMM yyyy", FormatProvider = CultureInfo.GetCultureInfo( "fr-FR" ) };

            Assert.AreEqual( "19 janvier 2013", column.Format( null, new DateOnly( 2013, 1, 19 ) ) );
        }

        [TestMethod]
        public void TestFormat_Null_UsesTheNullFormatter()
        {
            var column = new DateOnlyColumn( "born" ) { NullFormatter = NullFormatter.ForValue( "NULL" ) };

            Assert.AreEqual( "NULL", column.Format( null, null ) );
            Assert.IsNull( column.Parse( null, "NULL" ) );
        }

        [TestMethod]
        public void TestRoundTrip_ThroughTheReaderAndWriter()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "name" ) );
            schema.AddColumn( new DateOnlyColumn( "born" ) { InputFormat = "yyyy-MM-dd", OutputFormat = "yyyy-MM-dd" } );
            var stringWriter = new System.IO.StringWriter();
            var writer = new DelimitedWriter( stringWriter, schema, new DelimitedOptions { RecordSeparator = "\n" } );
            writer.Write( [ "Ada", new DateOnly( 1815, 12, 10 ) ] );
            writer.Write( [ "Unknown", null ] );

            Assert.AreEqual( "Ada,1815-12-10\nUnknown,\n", stringWriter.ToString() );

            var reader = new DelimitedReader( new System.IO.StringReader( stringWriter.ToString() ), schema );
            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( new DateOnly( 1815, 12, 10 ), reader.GetValues()[1] );
            Assert.IsTrue( reader.Read() );
            Assert.IsNull( reader.GetValues()[1] );
        }
    }
}
