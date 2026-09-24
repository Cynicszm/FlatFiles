using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the StringColumn class.
    /// </summary>
    [TestClass]
    public class StringColumnTester
    {
        /// <summary>
        ///     An exception should be thrown if name is blank.
        /// </summary>
        [TestMethod]
        public void TestCtor_NameBlank_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>( () => new StringColumn( "    " ) );
        }

        /// <summary>
        ///     If someone tries to pass a name that contains leading or trailing whitespace, it will be trimmed.
        ///     The name will also be made lower case.
        /// </summary>
        [TestMethod]
        public void TestCtor_SetsName_Trimmed()
        {
            var column = new StringColumn( " Name   " );
            Assert.AreEqual( "Name", column.ColumnName );
        }

        /// <summary>
        ///     If the value is blank, it is interpreted as null.
        /// </summary>
        [TestMethod]
        public void TestParse_ValueBlank_ReturnsNull()
        {
            var column = new StringColumn( "name" );
            var actual = (string) column.Parse( null, "     " )!;
            Assert.IsNull( actual );
        }

        /// <summary>
        ///     If the value is not blank, it is trimmed.
        /// </summary>
        [TestMethod]
        public void TestParse_ValueTrimmed()
        {
            var column = new StringColumn( "name" );
            var actual = (string) column.Parse( null, "  abc 123 " )!;
            const string expected = "abc 123";
            Assert.AreEqual( expected, actual );
        }
    }
}
