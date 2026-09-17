using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the DateTimeOffsetColumn class.
    /// </summary>
    [TestClass]
    public class DateTimeOffsetColumnTester
    {
        /// <summary>
        ///     Gets the time difference between the current time zone&amp;#39;s standard time and Coordinated Universal Time (UTC).
        /// </summary>
        private static TimeSpan BaseUtcOffset => TimeZoneInfo.Local.BaseUtcOffset;

        /// <summary>
        ///     An exception should be thrown if name is blank.
        /// </summary>
        [TestMethod]
        public void TestCtor_NameBlank_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>( () => new DateTimeOffsetColumn( "    " ) );
        }

        /// <summary>
        ///     If someone tries to pass a name that contains leading or trailing whitespace, it will be trimmed.
        ///     The name will also be made lower case.
        /// </summary>
        [TestMethod]
        public void TestCtor_SetsName_Trimmed()
        {
            var column = new DateTimeOffsetColumn( " Name   " );
            Assert.AreEqual( "Name", column.ColumnName );
        }

        /// <summary>
        ///     If no format string is provided, a generic parse will be attempted.
        /// </summary>
        [TestMethod]
        public void TestParse_NoFormatString_ParsesGenerically()
        {
            var column = new DateTimeOffsetColumn( "created" );
            var actual = (DateTimeOffset) column.Parse( null, "1/19/2013" );
            var expected = new DateTime( 2013, 1, 19 );
            Assert.AreEqual( expected, actual );
        }

        /// <summary>
        ///     If no format string is provided, a generic parse will be attempted.
        /// </summary>
        [TestMethod]
        public void TestParse_FormatProvider_NoFormatString_ParsesGenerically()
        {
            var column = new DateTimeOffsetColumn( "created" )
            {
                FormatProvider = CultureInfo.CurrentCulture
            };
            var actual = (DateTimeOffset) column.Parse( null, "1/19/2013" );
            var expected = new DateTimeOffset( 2013, 1, 19, 0, 0, 0, BaseUtcOffset );
            Assert.AreEqual( expected, actual );
        }

        /// <summary>
        ///     If no format string is provided, an exact parse will be attempted.
        /// </summary>
        [TestMethod]
        public void TestParse_FormatString_ParsesExactly()
        {
            var column = new DateTimeOffsetColumn( "created" )
            {
                InputFormat = "f",
                FormatProvider = CultureInfo.InvariantCulture
            };
            var actual = (DateTimeOffset) column.Parse( null, "Saturday, 19 January 2013 01:02" );
            var expected = new DateTimeOffset( 2013, 1, 19, 1, 2, 0, BaseUtcOffset );
            Assert.AreEqual( expected, actual );
        }

        // 
        [TestMethod]
        public void TestParse_FormatString_ParsesIso8601Exactly()
        {
            var column = new DateTimeOffsetColumn( "created" )
            {
                InputFormat = "o"
            };
            var actual = (DateTimeOffset) column.Parse( null, "2013-01-19T01:02:03.0000000-04:00" );
            var expected = new DateTimeOffset( 2013, 1, 19, 1, 2, 3, TimeSpan.FromHours( -4 ) );
            Assert.AreEqual( expected, actual );
        }

        /// <summary>
        ///     If no format string is provided, an exact parse will be attempted.
        /// </summary>
        [TestMethod]
        public void TestParse_FormatProvider_FormatString_ParsesExactly()
        {
            var column = new DateTimeOffsetColumn( "created" )
            {
                InputFormat = "d",
                FormatProvider = CultureInfo.CurrentCulture
            };
            
            var actual = (DateTimeOffset) column.Parse( null, "1/19/2013" );
            var expected = new DateTimeOffset( 2013, 1, 19, 0, 0, 0, BaseUtcOffset );
            Assert.AreEqual( expected, actual );
        }

        /// <summary>
        ///     If the value is blank and the field is not required, null will be returned.
        /// </summary>
        [TestMethod]
        public void TestParse_ValueBlank_NullReturned()
        {
            var column = new DateTimeOffsetColumn( "created" );
            var actual = (DateTime?) column.Parse( null, "    " );
            DateTime? expected = null;
            Assert.AreEqual( expected, actual );
        }

        /// <summary>
        ///     If the value is blank and the field is not required, null will be returned.
        /// </summary>
        [TestMethod]
        public void TestParse_Preprocessor_ShouldBeCalledOnce()
        {
            var preprocessorCallCount = 0;

            var column = new DateTimeOffsetColumn( "created" );
#pragma warning disable CS0618 // Type or member is obsolete
            column.Preprocessor = value => { preprocessorCallCount++; return value; };
#pragma warning restore CS0618 // Type or member is obsolete

            var actual = (DateTime?) column.Parse( null, "    " );
            DateTime? expected = null;
            Assert.AreEqual( 1, preprocessorCallCount, "Preprocessor function should be called exactly once" );
            Assert.AreEqual( expected, actual );
        }
    }
}
