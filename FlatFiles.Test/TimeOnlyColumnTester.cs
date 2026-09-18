using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the TimeOnlyColumn class: a time of day with no date, parsed and formatted through the column's own
    ///     format and provider, then the options' provider, then the current culture.
    /// </summary>
    [TestClass]
    public class TimeOnlyColumnTester
    {
        [TestMethod]
        public void TestCtor_NameBlank_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>( () => new TimeOnlyColumn( "    " ) );
        }

        [TestMethod]
        public void TestCtor_SetsName_Trimmed()
        {
            var column = new TimeOnlyColumn( " Opens   " );

            Assert.AreEqual( "Opens", column.ColumnName );
            Assert.AreEqual( typeof( TimeOnly ), column.ColumnType );
        }

        [TestMethod]
        public void TestParse_NoFormatString_ParsesGenerically()
        {
            var column = new TimeOnlyColumn( "opens" ) { FormatProvider = CultureInfo.InvariantCulture };

            Assert.AreEqual( new TimeOnly( 9, 30 ), column.Parse( null, "09:30" ) );
            Assert.AreEqual( new TimeOnly( 21, 30, 15 ), column.Parse( null, "21:30:15" ) );
        }

        [TestMethod]
        public void TestParse_FormatString_ParsesExactly()
        {
            var column = new TimeOnlyColumn( "opens" ) { InputFormat = "HHmmss", FormatProvider = CultureInfo.InvariantCulture };

            Assert.AreEqual( new TimeOnly( 9, 30, 5 ), column.Parse( null, "093005" ) );
            Assert.ThrowsExactly<FormatException>( () => column.Parse( null, "09:30:05" ), "An exact format admits nothing else." );
        }

        [TestMethod]
        public void TestParse_TwelveHourClockWithDesignator()
        {
            var column = new TimeOnlyColumn( "opens" ) { InputFormat = "h:mm tt", FormatProvider = CultureInfo.GetCultureInfo( "en-US" ) };

            Assert.AreEqual( new TimeOnly( 21, 30 ), column.Parse( null, "9:30 PM" ) );
        }

        [TestMethod]
        public void TestParse_ValueWithADate_Throws()
        {
            var column = new TimeOnlyColumn( "opens" ) { InputFormat = "HH:mm", FormatProvider = CultureInfo.InvariantCulture };

            Assert.ThrowsExactly<FormatException>( () => column.Parse( null, "2013-01-19 10:30" ) );
        }

        [TestMethod]
        public void TestParse_ValueBlank_NullReturned()
        {
            var column = new TimeOnlyColumn( "opens" );

            Assert.IsNull( column.Parse( null, "    " ) );
        }

        [TestMethod]
        public void TestFormat_NoFormatString_UsesTheShortTimePattern()
        {
            var column = new TimeOnlyColumn( "opens" ) { FormatProvider = CultureInfo.InvariantCulture };

            Assert.AreEqual( "09:30", column.Format( null, new TimeOnly( 9, 30, 15 ) ) );
        }

        [TestMethod]
        public void TestFormat_FormatString_KeepsTheSecondsAndFraction()
        {
            var column = new TimeOnlyColumn( "opens" ) { OutputFormat = "HH:mm:ss.fff", FormatProvider = CultureInfo.InvariantCulture };

            Assert.AreEqual( "09:30:15.250", column.Format( null, new TimeOnly( 9, 30, 15, 250 ) ) );
        }

        [TestMethod]
        public void TestFormat_Null_UsesTheNullFormatter()
        {
            var column = new TimeOnlyColumn( "opens" ) { NullFormatter = NullFormatter.ForValue( "--:--" ) };

            Assert.AreEqual( "--:--", column.Format( null, null ) );
            Assert.IsNull( column.Parse( null, "--:--" ) );
        }

        [TestMethod]
        public void TestRoundTrip_ThroughTheReaderAndWriter()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "shop" ), new Window( 6 ) );
            schema.AddColumn( new TimeOnlyColumn( "opens" ) { InputFormat = "HH:mm", OutputFormat = "HH:mm" }, new Window( 5 ) );
            var stringWriter = new System.IO.StringWriter();
            var writer = new FixedLengthWriter( stringWriter, schema, new FixedLengthOptions { RecordSeparator = "\n" } );
            writer.Write( [ "Bakery", new TimeOnly( 6, 30 ) ] );
            writer.Write( [ "Closed", null ] );

            Assert.AreEqual( "Bakery06:30\nClosed     \n", stringWriter.ToString() );

            var reader = new FixedLengthReader( new System.IO.StringReader( stringWriter.ToString() ), schema );
            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( new TimeOnly( 6, 30 ), reader.GetValues()[1] );
            Assert.IsTrue( reader.Read() );
            Assert.IsNull( reader.GetValues()[1] );
        }
    }
}
