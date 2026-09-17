using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the EnumColumn class.
    /// </summary>
    [TestClass]
    public class EnumColumnTester
    {
        public enum MyEnum
        {
            First = 1,
            Second = 2
        }

        // This test became obsolete with C# 7.3, which introduced the enum generic constraint
        ///     // <summary>
        ///     // An exception should be thrown if TEnum is not an enumeration.
        ///     // </summary>
        //[TestMethod]
        //public void TestCtor_NonEnum_Throws()
        //{
        //    Assert.ThrowsExactly<ArgumentException>(() => new EnumColumn<DateTime>("    "));
        //}

        /// <summary>
        ///     An exception should be thrown if name is blank.
        /// </summary>
        [TestMethod]
        public void TestCtor_NameBlank_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>( () => new EnumColumn<MyEnum>( "    " ) );
        }

        /// <summary>
        ///     If someone tries to pass a name that contains leading or trailing whitespace, it will be trimmed.
        ///     The name will also be made lower case.
        /// </summary>
        [TestMethod]
        public void TestCtor_SetsName_Trimmed()
        {
            var column = new EnumColumn<MyEnum>( " Name   " );
            Assert.AreEqual( "Name", column.ColumnName );
        }

        /// <summary>
        ///     If the value is blank and the field is not required, null will be returned.
        /// </summary>
        [TestMethod]
        public void TestParse_ValueBlank_NullReturned()
        {
            var column = new EnumColumn<MyEnum>( "count" );
            var actual = (MyEnum?) column.Parse( null, "    " );
            MyEnum? expected = null;
            Assert.AreEqual( expected, actual );
        }

        /// <summary>
        ///     If the value is a valid integer value, the equivalent enum value should be returned.
        /// </summary>
        [TestMethod]
        public void TestParse_Int32Value_EnumReturned()
        {
            var column = new EnumColumn<MyEnum>( "count" );
            var actual = (MyEnum) column.Parse( null, "1" );
            const MyEnum expected = MyEnum.First;
            Assert.AreEqual( expected, actual );
        }

        /// <summary>
        ///     If the value is a valid string value, the equivalent enum value should be returned.
        /// </summary>
        [TestMethod]
        public void TestParse_StringValue_EnumReturned()
        {
            var column = new EnumColumn<MyEnum>( "count" );
            var actual = (MyEnum) column.Parse( null, "First" );
            const MyEnum expected = MyEnum.First;
            Assert.AreEqual( expected, actual );
        }

        /// <summary>
        ///     By default, enums should be written as integers.
        /// </summary>
        [TestMethod]
        public void TestFormat_IntegerStringReturned()
        {
            var column = new EnumColumn<MyEnum>( "count" );
            var actual = column.Format( null, MyEnum.First );
            const string expected = "1";
            Assert.AreEqual( expected, actual );
        }

        /// <summary>
        ///     The given formatter should be used.
        /// </summary>
        [TestMethod]
        public void TestFormat_OverrideFormatter_UsesCustomFormatter()
        {
            var column = new EnumColumn<MyEnum>( "count" )
            {
                Formatter = e => e.ToString()
            };
            var actual = column.Format( null, MyEnum.First );
            const string expected = "First";
            Assert.AreEqual( expected, actual );
        }
        [TestMethod]
        public void TestParserAndFormatter_SetToNull_RestoreTheDefaults()
        {
            var column = new EnumColumn<DayOfWeek>( "day" )
            {
                Parser = _ => DayOfWeek.Sunday,
                Formatter = _ => "custom"
            };
            Assert.AreEqual( DayOfWeek.Sunday, column.Parse( null, "Friday" ) );
            Assert.AreEqual( "custom", column.Format( null, DayOfWeek.Friday ) );

            column.Parser = null;
            column.Formatter = null;

            Assert.AreEqual( DayOfWeek.Friday, column.Parse( null, "friday" ), "The default parser ignores case." );
            Assert.AreEqual( "5", column.Format( null, DayOfWeek.Friday ), "The default formatter writes the underlying number." );
        }
    }
}
