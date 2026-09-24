using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{

    /// <summary>
    ///     Tests the DateTimeColumn class.
    /// </summary>
    [TestClass]
    public class DateTimeColumnTester
    {
        /// <summary>
        ///     An exception should be thrown if name is blank.
        /// </summary>
        [TestMethod]
        public void TestCtor_NameBlank_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>( () => new DateTimeColumn( "    " ) );
        }

        /// <summary>
        ///     If someone tries to pass a name that contains leading or trailing whitespace, it will be trimmed.
        ///     The name will also be made lower case.
        /// </summary>
        [TestMethod]
        public void TestCtor_SetsName_Trimmed()
        {
            var column = new DateTimeColumn( " Name   " );
            Assert.AreEqual( "Name", column.ColumnName );
        }

        /// <summary>
        ///     If no format string is provided, a generic parse will be attempted.
        /// </summary>
        [TestMethod]
        public void TestParse_NoFormatString_ParsesGenerically()
        {
            var column = new DateTimeColumn( "created" );
            var actual = (DateTime) column.Parse( null, "1/19/2013" )!;
            var expected = new DateTime( 2013, 1, 19 );
            Assert.AreEqual( expected, actual );
        }

        /// <summary>
        ///     If no format string is provided, a generic parse will be attempted.
        /// </summary>
        [TestMethod]
        public void TestParse_FormatProvider_NoFormatString_ParsesGenerically()
        {
            var column = new DateTimeColumn( "created" )
            {
                FormatProvider = CultureInfo.CurrentCulture
            };
            var actual = (DateTime) column.Parse( null, "1/19/2013" )!;
            var expected = new DateTime( 2013, 1, 19 );
            Assert.AreEqual( expected, actual );
        }

        /// <summary>
        ///     If no format string is provided, an exact parse will be attempted.
        /// </summary>
        [TestMethod]
        public void TestParse_FormatString_ParsesExactly()
        {
            var column = new DateTimeColumn( "created" )
            {
                InputFormat = "d"
            };
            var actual = (DateTime) column.Parse( null, "1/19/2013" )!;
            var expected = new DateTime( 2013, 1, 19 );
            Assert.AreEqual( expected, actual );
        }

        /// <summary>
        ///     If no format string is provided, an exact parse will be attempted.
        /// </summary>
        [TestMethod]
        public void TestParse_FormatProvider_FormatString_ParsesExactly()
        {
            var column = new DateTimeColumn( "created" )
            {
                InputFormat = "d",
                FormatProvider = CultureInfo.InvariantCulture
            };
            var actual = (DateTime) column.Parse( null, "01/19/2013" )!;
            var expected = new DateTime( 2013, 1, 19 );
            Assert.AreEqual( expected, actual );
        }

        /// <summary>
        ///     If the value is blank and the field is not required, null will be returned.
        /// </summary>
        [TestMethod]
        public void TestParse_ValueBlank_NullReturned()
        {
            var column = new DateTimeColumn( "created" );
            var actual = (DateTime?) column.Parse( null, "    " );
            Assert.IsNull( actual );
        }
    }
}
