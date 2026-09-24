using System;
using System.Data;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Drives every accessor on FlatFileDataReader and every by-name and nullable extension on IFlatFileDataRecord
    ///     over one record with every value present and one with every value blank, with DBNull returned and with null
    ///     returned.
    /// </summary>
    [TestClass]
    public class DataRecordExtensionsSurfaceTester
    {
        public enum Colour
        {
            Red = 1,
            Green = 2
        }

        private static DelimitedSchema Schema()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new BooleanColumn( "Bool" ) );
            schema.AddColumn( new ByteColumn( "Byte" ) );
            schema.AddColumn( new ByteArrayColumn( "Bytes" ) );
            schema.AddColumn( new CharColumn( "Char" ) );
            schema.AddColumn( new CharArrayColumn( "Chars" ) );
            schema.AddColumn( new DateTimeColumn( "DateTime" ) { InputFormat = "yyyy-MM-dd HH:mm" } );
            schema.AddColumn( new DateOnlyColumn( "DateOnly" ) { InputFormat = "yyyy-MM-dd" } );
            schema.AddColumn( new TimeOnlyColumn( "TimeOnly" ) { InputFormat = "HH:mm" } );
            schema.AddColumn( new DateTimeOffsetColumn( "DateTimeOffset" ) { InputFormat = "o" } );
            schema.AddColumn( new DecimalColumn( "Decimal" ) { FormatProvider = CultureInfo.InvariantCulture } );
            schema.AddColumn( new DoubleColumn( "Double" ) { FormatProvider = CultureInfo.InvariantCulture } );
            schema.AddColumn( new SingleColumn( "Float" ) { FormatProvider = CultureInfo.InvariantCulture } );
            schema.AddColumn( new GuidColumn( "Guid" ) );
            schema.AddColumn( new Int16Column( "Int16" ) );
            schema.AddColumn( new Int32Column( "Int32" ) );
            schema.AddColumn( new Int64Column( "Int64" ) );
            schema.AddColumn( new SByteColumn( "SByte" ) );
            schema.AddColumn( new StringColumn( "String" ) );
            schema.AddColumn( new TimeSpanColumn( "TimeSpan" ) );
            schema.AddColumn( new UInt16Column( "UInt16" ) );
            schema.AddColumn( new UInt32Column( "UInt32" ) );
            schema.AddColumn( new UInt64Column( "UInt64" ) );
            schema.AddColumn( new EnumColumn<Colour>( "Colour" ) );
            schema.AddColumn( new Int32Column( "ColourNumber" ) );
            schema.AddColumn( new StringColumn( "ColourName" ) );
            return schema;
        }

        private const string Full = "true,7,abc,z,ab,2026-09-18 10:30,2026-09-18,10:30,2026-09-18T10:30:00.0000000+01:00,1.5,2.5,3.5,12345678-1234-1234-1234-123456789012,16,32,64,-7,text,01:30:00,18,34,66,Green,2,Red";

        private const string Blank = ",,,,,,,,,,,,,,,,,,,,,,,,";

        private static FlatFileDataReader Reader( string record, FlatFileDataReaderOptions options = null! )
        {
            var reader = new FlatFileDataReader( new DelimitedReader( new StringReader( record + "\n" ), Schema() ), options );
            Assert.IsTrue( reader.Read() );
            return reader;
        }

        [TestMethod]
        public void TestAccessors_ByOrdinalAndByName_ReturnEveryValue()
        {
            var reader = Reader( Full );

            Assert.IsTrue( reader.GetBoolean( 0 ) );
            Assert.IsTrue( reader.GetBoolean( "Bool" ) );
            Assert.AreEqual( (byte) 7, reader.GetByte( "Byte" ) );
            Assert.AreEqual( 'z', reader.GetChar( 3 ) );
            Assert.AreEqual( 'z', reader.GetChar( "Char" ) );
            Assert.AreEqual( new DateTime( 2026, 9, 18, 10, 30, 0 ), reader.GetDateTime( "DateTime" ) );
            Assert.AreEqual( new DateOnly( 2026, 9, 18 ), reader.GetDateOnly( "DateOnly" ) );
            Assert.AreEqual( new TimeOnly( 10, 30 ), reader.GetTimeOnly( "TimeOnly" ) );
            Assert.AreEqual( new DateTimeOffset( 2026, 9, 18, 10, 30, 0, TimeSpan.FromHours( 1 ) ), reader.GetDateTimeOffset( 8 ) );
            Assert.AreEqual( new DateTimeOffset( 2026, 9, 18, 10, 30, 0, TimeSpan.FromHours( 1 ) ), reader.GetDateTimeOffset( "DateTimeOffset" ) );
            Assert.AreEqual( 1.5m, reader.GetDecimal( "Decimal" ) );
            Assert.AreEqual( 2.5, reader.GetDouble( "Double" ) );
            Assert.AreEqual( 3.5f, reader.GetFloat( "Float" ) );
            Assert.AreEqual( new Guid( "12345678-1234-1234-1234-123456789012" ), reader.GetGuid( "Guid" ) );
            Assert.AreEqual( (short) 16, reader.GetInt16( "Int16" ) );
            Assert.AreEqual( 32, reader.GetInt32( "Int32" ) );
            Assert.AreEqual( 64L, reader.GetInt64( "Int64" ) );
            Assert.AreEqual( (sbyte) -7, reader.GetSByte( "SByte" ) );
            Assert.AreEqual( "text", reader.GetString( "String" ) );
            Assert.AreEqual( TimeSpan.FromMinutes( 90 ), reader.GetTimeSpan( "TimeSpan" ) );
            Assert.AreEqual( (ushort) 18, reader.GetUInt16( "UInt16" ) );
            Assert.AreEqual( 34u, reader.GetUInt32( "UInt32" ) );
            Assert.AreEqual( 66ul, reader.GetUInt64( "UInt64" ) );
            Assert.AreEqual( "text", reader["String"] );
            Assert.AreEqual( "text", reader[17] );
            Assert.AreEqual( "text", reader.GetValue( "String" ) );
            Assert.AreEqual( 17, reader.GetOrdinal( "String" ) );
            Assert.AreEqual( "String", reader.GetName( 17 ) );
            Assert.AreEqual( typeof( string ), reader.GetFieldType( 17 ) );
            Assert.AreEqual( typeof( string ), reader.GetFieldType( "String" ) );
            Assert.AreEqual( "String", reader.GetDataTypeName( 17 ) );
            Assert.AreEqual( "String", reader.GetDataTypeName( "String" ) );
            Assert.AreEqual( 25, reader.FieldCount );
            Assert.AreEqual( 0, ((IDataReader) reader).Depth );
            Assert.AreEqual( 0, ((IDataReader) reader).RecordsAffected );
        }

        [TestMethod]
        public void TestNullableAccessors_PresentAndBlank()
        {
            var full = Reader( Full );
            Assert.IsTrue( full.GetNullableBoolean( 0 ) );
            Assert.IsTrue( full.GetNullableBoolean( "Bool" ) );
            Assert.AreEqual( (byte) 7, full.GetNullableByte( 1 ) );
            Assert.AreEqual( (byte) 7, full.GetNullableByte( "Byte" ) );
            Assert.AreEqual( 'z', full.GetNullableChar( 3 ) );
            Assert.AreEqual( 'z', full.GetNullableChar( "Char" ) );
            Assert.AreEqual( new DateTime( 2026, 9, 18, 10, 30, 0 ), full.GetNullableDateTime( 5 ) );
            Assert.AreEqual( new DateTime( 2026, 9, 18, 10, 30, 0 ), full.GetNullableDateTime( "DateTime" ) );
            Assert.AreEqual( new DateOnly( 2026, 9, 18 ), full.GetNullableDateOnly( 6 ) );
            Assert.AreEqual( new TimeOnly( 10, 30 ), full.GetNullableTimeOnly( 7 ) );
            Assert.IsNotNull( full.GetNullableDateTimeOffset( 8 ) );
            Assert.IsNotNull( full.GetNullableDateTimeOffset( "DateTimeOffset" ) );
            Assert.AreEqual( 1.5m, full.GetNullableDecimal( 9 ) );
            Assert.AreEqual( 1.5m, full.GetNullableDecimal( "Decimal" ) );
            Assert.AreEqual( 2.5, full.GetNullableDouble( 10 ) );
            Assert.AreEqual( 2.5, full.GetNullableDouble( "Double" ) );
            Assert.AreEqual( 3.5f, full.GetNullableFloat( 11 ) );
            Assert.AreEqual( 3.5f, full.GetNullableFloat( "Float" ) );
            Assert.IsNotNull( full.GetNullableGuid( 12 ) );
            Assert.IsNotNull( full.GetNullableGuid( "Guid" ) );
            Assert.AreEqual( (short) 16, full.GetNullableInt16( 13 ) );
            Assert.AreEqual( (short) 16, full.GetNullableInt16( "Int16" ) );
            Assert.AreEqual( 32, full.GetNullableInt32( 14 ) );
            Assert.AreEqual( 32, full.GetNullableInt32( "Int32" ) );
            Assert.AreEqual( 64L, full.GetNullableInt64( 15 ) );
            Assert.AreEqual( 64L, full.GetNullableInt64( "Int64" ) );
            Assert.AreEqual( (sbyte) -7, full.GetNullableSByte( 16 ) );
            Assert.AreEqual( (sbyte) -7, full.GetNullableSByte( "SByte" ) );
            Assert.AreEqual( "text", full.GetNullableString( 17 ) );
            Assert.AreEqual( "text", full.GetNullableString( "String" ) );
            Assert.AreEqual( TimeSpan.FromMinutes( 90 ), full.GetNullableTimeSpan( 18 ) );
            Assert.AreEqual( TimeSpan.FromMinutes( 90 ), full.GetNullableTimeSpan( "TimeSpan" ) );
            Assert.AreEqual( (ushort) 18, full.GetNullableUInt16( 19 ) );
            Assert.AreEqual( (ushort) 18, full.GetNullableUInt16( "UInt16" ) );
            Assert.AreEqual( 34u, full.GetNullableUInt32( 20 ) );
            Assert.AreEqual( 34u, full.GetNullableUInt32( "UInt32" ) );
            Assert.AreEqual( 66ul, full.GetNullableUInt64( 21 ) );
            Assert.AreEqual( 66ul, full.GetNullableUInt64( "UInt64" ) );

            var blank = Reader( Blank );
            Assert.IsNull( blank.GetNullableBoolean( "Bool" ) );
            Assert.IsNull( blank.GetNullableByte( "Byte" ) );
            Assert.IsNull( blank.GetNullableChar( "Char" ) );
            Assert.IsNull( blank.GetNullableDateTime( "DateTime" ) );
            Assert.IsNull( blank.GetNullableDateOnly( "DateOnly" ) );
            Assert.IsNull( blank.GetNullableTimeOnly( "TimeOnly" ) );
            Assert.IsNull( blank.GetNullableDateTimeOffset( "DateTimeOffset" ) );
            Assert.IsNull( blank.GetNullableDecimal( "Decimal" ) );
            Assert.IsNull( blank.GetNullableDouble( "Double" ) );
            Assert.IsNull( blank.GetNullableFloat( "Float" ) );
            Assert.IsNull( blank.GetNullableGuid( "Guid" ) );
            Assert.IsNull( blank.GetNullableInt16( "Int16" ) );
            Assert.IsNull( blank.GetNullableInt32( "Int32" ) );
            Assert.IsNull( blank.GetNullableInt64( "Int64" ) );
            Assert.IsNull( blank.GetNullableSByte( "SByte" ) );
            Assert.IsNull( blank.GetNullableString( "String" ) );
            Assert.IsNull( blank.GetNullableTimeSpan( "TimeSpan" ) );
            Assert.IsNull( blank.GetNullableUInt16( "UInt16" ) );
            Assert.IsNull( blank.GetNullableUInt32( "UInt32" ) );
            Assert.IsNull( blank.GetNullableUInt64( "UInt64" ) );
            Assert.IsTrue( blank.IsDBNull( 0 ) );
            Assert.AreEqual( DBNull.Value, blank.GetValue( 0 ), "With the default options a null is returned as DBNull." );
        }

        [TestMethod]
        public void TestBytesAndChars_CopyIntoTheCallersBuffer()
        {
            var reader = Reader( Full );
            var bytes = new byte[5];
            var chars = new char[4];

            Assert.AreEqual( 2, reader.GetBytes( 2, 1, bytes, 1, 2 ) );
            Assert.AreEqual( 2, reader.GetBytes( "Bytes", 1, bytes, 1, 2 ) );
            Assert.AreEqual( 2, reader.GetChars( 4, 0, chars, 2, 2 ) );
            Assert.AreEqual( 2, reader.GetChars( "Chars", 0, chars, 2, 2 ) );

            CollectionAssert.AreEqual( new byte[] { 0, (byte) 'b', (byte) 'c', 0, 0 }, bytes );
            CollectionAssert.AreEqual( new[] { '\0', '\0', 'a', 'b' }, chars );
            Assert.ThrowsExactly<ArgumentNullException>( () => reader.GetBytes( 2, 0, null, 0, 1 ) );
            Assert.ThrowsExactly<ArgumentNullException>( () => reader.GetChars( 4, 0, null, 0, 1 ) );
            Assert.ThrowsExactly<InvalidCastException>( () => ((IDataRecord) reader).GetData( 0 ), "Nested readers are not a flat file concept." );
            Assert.ThrowsExactly<InvalidCastException>( () => reader.GetData( "Bool" ) );
        }

        [TestMethod]
        public void TestEnumAccessors_FromTheEnumColumnANumberAndAName()
        {
            var reader = Reader( Full );

            Assert.AreEqual( Colour.Green, reader.GetEnum<Colour>( 22 ) );
            Assert.AreEqual( Colour.Green, reader.GetEnum<Colour>( "Colour" ) );
            Assert.AreEqual( Colour.Green, reader.GetEnum<Colour>( "ColourNumber" ), "An integer column converts through the enum's underlying type." );
            Assert.AreEqual( Colour.Red, reader.GetEnum<Colour>( "ColourName" ), "A string column parses by name." );
            Assert.AreEqual( Colour.Green, reader.GetNullableEnum<Colour>( 22 ) );
            Assert.AreEqual( Colour.Green, reader.GetNullableEnum<Colour>( "Colour" ) );
            Assert.AreEqual( Colour.Red, reader.GetEnum<int?, Colour>( 23, n => n == 2 ? Colour.Red : Colour.Green ), "A mapper delegate decides." );
            Assert.AreEqual( Colour.Red, reader.GetEnum<int?, Colour>( "ColourNumber", n => n == 2 ? Colour.Red : Colour.Green ) );
            Assert.AreEqual( Colour.Green, reader.GetNullableEnum<string, Colour>( 24, s => s == "Red" ? Colour.Green : null ) );
            Assert.AreEqual( Colour.Green, reader.GetNullableEnum<string, Colour>( "ColourName", s => s == "Red" ? Colour.Green : null ) );

            var blank = Reader( Blank );
            Assert.IsNull( blank.GetNullableEnum<Colour>( "Colour" ) );
            Assert.IsNull( blank.GetNullableEnum<int?, Colour>( "ColourNumber", n => n is null ? null : Colour.Red ), "The mapper sees the blank as null and answers in kind." );
            Assert.ThrowsExactly<InvalidCastException>( () => blank.GetEnum<Colour>( "Colour" ) );
        }

        [TestMethod]
        public void TestGetValueGeneric_ConvertsThroughTheRequestedType()
        {
            var reader = Reader( Full );

            Assert.AreEqual( 32L, reader.GetValue<long>( "Int32" ), "IConvertible values change type." );
            Assert.AreEqual( "32", reader.GetValue<string>( 14 ) );
            Assert.AreEqual( 1.5m, reader.GetValue<decimal?>( "Decimal" ) );
            Assert.AreEqual( new Guid( "12345678-1234-1234-1234-123456789012" ), reader.GetValue<Guid>( "Guid" ) );
            Assert.AreEqual( Colour.Green, reader.GetValue<Colour>( "ColourNumber" ) );

            var blank = Reader( Blank );
            Assert.IsNull( blank.GetValue<int?>( "Int32" ) );
            Assert.IsNull( blank.GetValue<string>( "String" ) );
            Assert.ThrowsExactly<InvalidCastException>( () => blank.GetValue<int>( "Int32" ) );
        }

        [TestMethod]
        public void TestGetValues_WithAndWithoutDBNull()
        {
            var dbNull = Reader( Blank );
            var values = new object[3];
            Assert.AreEqual( 3, dbNull.GetValues( values ) );
            Assert.AreEqual( DBNull.Value, values[0] );

            var nulls = Reader( Blank, new FlatFileDataReaderOptions { IsDBNullReturned = false } );
            var plain = new object[3];
            Assert.AreEqual( 3, nulls.GetValues( plain ) );
            Assert.IsNull( plain[0] );
            Assert.IsNull( nulls.GetValue( 0 ) );
            Assert.IsNull( nulls["String"] );
            Assert.IsTrue( nulls.IsDBNull( 0 ), "IsDBNull still reports the blank whichever way it is returned." );

            var full = Reader( Full );
            var wide = new object[30];
            Assert.AreEqual( 25, full.GetValues( wide ), "Only as many values as there are columns are copied." );
            Assert.AreEqual( "text", wide[17] );
        }

        [TestMethod]
        public void TestSchemaTable_DescribesEveryColumn()
        {
            var reader = Reader( Full );

            var table = reader.GetSchemaTable();

            Assert.AreEqual( 25, table.Rows.Count );
            Assert.AreEqual( "Bool", table.Rows[0]["ColumnName"] );
            Assert.AreEqual( typeof( bool ), table.Rows[0]["DataType"] );
            Assert.IsFalse( reader.IsClosed );
            Assert.IsFalse( ((IDataReader) reader).NextResult(), "A flat file has one result set." );
            reader.Close();
            Assert.IsTrue( reader.IsClosed );
            reader.Dispose();
        }
    }
}
