using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the by-name and enum lookups on IDataRecord and IFlatFileDataRecord, which the positional
    ///     tests elsewhere do not reach.
    /// </summary>
    [TestClass]
    public class DataRecordExtensionsLookupTester
    {
        public enum Colour
        {
            Red,
            Green
        }

        private static FlatFileDataReader OpenRecord()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new Int32Column( "id" ) );
            schema.AddColumn( new StringColumn( "name" ) );
            schema.AddColumn( new ByteColumn( "small" ) );
            schema.AddColumn( new UInt16Column( "u16" ) );
            schema.AddColumn( new UInt32Column( "u32" ) );
            schema.AddColumn( new UInt64Column( "u64" ) );
            schema.AddColumn( new SByteColumn( "sb" ) );
            schema.AddColumn( new TimeSpanColumn( "span" ) );
            schema.AddColumn( new StringColumn( "colour" ) );
            var reader = new DelimitedReader( new StringReader( "1,Bob,7,65535,4000000000,18000000000000000000,-5,01:02:03,Green\r\n" ), schema );
            var record = new FlatFileDataReader( reader );
            Assert.IsTrue( record.Read(), "The record should have been read." );
            return record;
        }

        [TestMethod]
        public void TestGetByName_UnsignedSignedAndTimeSpanValues()
        {
            var record = OpenRecord();

            Assert.AreEqual( (ushort) 65535, record.GetUInt16( "u16" ) );
            Assert.AreEqual( 4000000000u, record.GetUInt32( "u32" ) );
            Assert.AreEqual( 18000000000000000000UL, record.GetUInt64( "u64" ) );
            Assert.AreEqual( (sbyte) -5, record.GetSByte( "sb" ) );
            Assert.AreEqual( new TimeSpan( 1, 2, 3 ), record.GetTimeSpan( "span" ) );
            Assert.AreEqual( (byte?) 7, record.GetNullableByte( "small" ) );
            Assert.AreEqual( (ulong?) 18000000000000000000UL, record.GetNullableUInt64( "u64" ) );
        }

        [TestMethod]
        public void TestGetEnum_ByNameAndOrdinal_WithAndWithoutAMapper()
        {
            var record = OpenRecord();

            Assert.AreEqual( Colour.Green, record.GetEnum<Colour>( "colour" ) );
            Assert.AreEqual( Colour.Green, record.GetEnum<string, Colour>( "colour", value => Enum.Parse<Colour>( value ) ) );
            Assert.AreEqual( Colour.Green, record.GetEnum<string, Colour>( 8, value => Enum.Parse<Colour>( value ) ) );
            Assert.AreEqual( (Colour?) Colour.Green, record.GetNullableEnum<Colour>( "colour" ) );
            Assert.AreEqual( (Colour?) Colour.Green, record.GetNullableEnum<string, Colour>( "colour", value => (Colour?) Enum.Parse<Colour>( value ) ) );
            Assert.AreEqual( (Colour?) Colour.Green, record.GetNullableEnum<string, Colour>( 8, value => (Colour?) Enum.Parse<Colour>( value ) ) );
        }

        [TestMethod]
        public void TestByName_ValueAndNullChecks()
        {
            var record = OpenRecord();

            Assert.IsFalse( record.IsDBNull( "name" ) );
            Assert.AreEqual( "Bob", record.GetValue( "name" ) );
            Assert.AreEqual( "Bob", record.GetNullableString( "name" ) );
        }
    }
}
