using System;
using System.Globalization;
using System.IO;
using System.Linq;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests that DateOnly and TimeOnly properties map through the delimited, fixed-length, dynamic and auto-mapped
    ///     type mappers and through the data reader, nullable and not.
    /// </summary>
    [TestClass]
    public class DateOnlyTimeOnlyMappingTester
    {
        private static readonly Appointment[] Appointments =
        [
            new() { Who = "Ada", Day = new DateOnly( 2026, 9, 18 ), Starts = new TimeOnly( 9, 30 ), Ends = new TimeOnly( 10, 15 ), Reviewed = new DateOnly( 2026, 9, 19 ) },
            new() { Who = "Bob", Day = new DateOnly( 2026, 12, 1 ), Starts = new TimeOnly( 14, 0 ), Ends = null, Reviewed = null }
        ];

        [TestMethod]
        public void TestDelimitedMapper_RoundTripsWithFormats()
        {
            var mapper = DelimitedTypeMapper.Define<Appointment>();
            mapper.Property( a => a.Who );
            mapper.Property( a => a.Day ).InputFormat( "yyyyMMdd" ).OutputFormat( "yyyyMMdd" );
            mapper.Property( a => a.Starts ).OutputFormat( "HH:mm" ).FormatProvider( CultureInfo.InvariantCulture );
            mapper.Property( a => a.Ends ).OutputFormat( "HH:mm" ).FormatProvider( CultureInfo.InvariantCulture );
            mapper.Property( a => a.Reviewed ).InputFormat( "yyyy-MM-dd" ).OutputFormat( "yyyy-MM-dd" );
            var options = new DelimitedOptions { RecordSeparator = "\n" };
            var writer = new StringWriter();

            mapper.Write( writer, Appointments, options );
            var text = writer.ToString();
            var read = mapper.Read( new StringReader( text ), options ).ToList();

            Assert.AreEqual( "Ada,20260918,09:30,10:15,2026-09-19\nBob,20261201,14:00,,\n", text );
            Assert.HasCount( 2, read );
            Assert.AreEqual( new DateOnly( 2026, 9, 18 ), read[0].Day );
            Assert.AreEqual( new TimeOnly( 10, 15 ), read[0].Ends );
            Assert.AreEqual( new DateOnly( 2026, 9, 19 ), read[0].Reviewed );
            Assert.AreEqual( new TimeOnly( 14, 0 ), read[1].Starts );
            Assert.IsNull( read[1].Ends );
            Assert.IsNull( read[1].Reviewed );
        }

        [TestMethod]
        public void TestFixedLengthMapper_RoundTripsInWindows()
        {
            var mapper = FixedLengthTypeMapper.Define<Appointment>();
            mapper.Property( a => a.Who, 4 );
            mapper.Property( a => a.Day, 8 ).InputFormat( "yyyyMMdd" ).OutputFormat( "yyyyMMdd" );
            mapper.Property( a => a.Starts, 5 ).InputFormat( "HH:mm" ).OutputFormat( "HH:mm" );
            mapper.Property( a => a.Ends, 5 ).InputFormat( "HH:mm" ).OutputFormat( "HH:mm" );
            mapper.Property( a => a.Reviewed, 10 ).InputFormat( "yyyy-MM-dd" ).OutputFormat( "yyyy-MM-dd" );
            var options = new FixedLengthOptions { RecordSeparator = "\n" };
            var writer = new StringWriter();

            mapper.Write( writer, Appointments, options );
            var text = writer.ToString();
            var read = mapper.Read( new StringReader( text ), options ).ToList();

            Assert.AreEqual( "Ada 2026091809:3010:152026-09-19\nBob 2026120114:00               \n", text );
            Assert.AreEqual( new TimeOnly( 9, 30 ), read[0].Starts );
            Assert.IsNull( read[1].Ends );
            Assert.IsNull( read[1].Reviewed );
        }

        [TestMethod]
        public void TestDynamicMapper_ExposesDateOnlyAndTimeOnlyProperties()
        {
            var mapper = DelimitedTypeMapper.DefineDynamic( typeof( Appointment ) );
            mapper.StringProperty( "Who" );
            mapper.DateOnlyProperty( "Day" ).InputFormat( "yyyy-MM-dd" );
            mapper.TimeOnlyProperty( "Starts" ).InputFormat( "HH:mm" );
            mapper.TimeOnlyProperty( "Ends" ).InputFormat( "HH:mm" );
            mapper.DateOnlyProperty( "Reviewed" ).InputFormat( "yyyy-MM-dd" );

            var read = mapper.Read( new StringReader( "Ada,2026-09-18,09:30,,\n" ) ).Cast<Appointment>().Single();

            Assert.AreEqual( new DateOnly( 2026, 9, 18 ), read.Day );
            Assert.AreEqual( new TimeOnly( 9, 30 ), read.Starts );
            Assert.IsNull( read.Ends, "A blank maps to null on a nullable TimeOnly." );
            Assert.IsNull( read.Reviewed );
        }

        [TestMethod]
        public void TestDynamicFixedLengthMapper_ExposesDateOnlyAndTimeOnlyProperties()
        {
            var mapper = FixedLengthTypeMapper.DefineDynamic( typeof( Appointment ) );
            mapper.StringProperty( "Who", 4 );
            mapper.DateOnlyProperty( "Day", 10 ).InputFormat( "yyyy-MM-dd" );
            mapper.TimeOnlyProperty( "Starts", 5 ).InputFormat( "HH:mm" );

            var read = mapper.Read( new StringReader( "Ada 2026-09-1809:30\n" ) ).Cast<Appointment>().Single();

            Assert.AreEqual( new DateOnly( 2026, 9, 18 ), read.Day );
            Assert.AreEqual( new TimeOnly( 9, 30 ), read.Starts );
        }

        [TestMethod]
        public void TestAutoMap_ChoosesTheNewColumnsFromThePropertyTypes()
        {
            const string text = "Who,Day,Starts,Ends,Reviewed\nAda,2026-09-18,09:30,10:15,2026-09-19\nBob,2026-12-01,14:00,,\n";
            var options = new DelimitedOptions { FormatProvider = CultureInfo.InvariantCulture };

            var read = DelimitedTypeMapper.GetAutoMappedReader<Appointment>( new StringReader( text ), options ).ReadAll().ToList();

            Assert.HasCount( 2, read );
            Assert.AreEqual( new DateOnly( 2026, 9, 18 ), read[0].Day );
            Assert.AreEqual( new TimeOnly( 10, 15 ), read[0].Ends );
            Assert.IsNull( read[1].Ends );
            Assert.IsNull( read[1].Reviewed );
        }

        [TestMethod]
        public void TestDataReader_GetDateOnlyAndGetTimeOnly_ByOrdinalAndName()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new DateOnlyColumn( "Day" ) { InputFormat = "yyyy-MM-dd" } );
            schema.AddColumn( new TimeOnlyColumn( "Starts" ) { InputFormat = "HH:mm" } );
            schema.AddColumn( new TimeOnlyColumn( "Ends" ) { InputFormat = "HH:mm" } );
            var reader = new FlatFileDataReader( new DelimitedReader( new StringReader( "2026-09-18,09:30,\n" ), schema ) );

            Assert.IsTrue( reader.Read() );

            Assert.AreEqual( new DateOnly( 2026, 9, 18 ), reader.GetDateOnly( 0 ) );
            Assert.AreEqual( new DateOnly( 2026, 9, 18 ), reader.GetDateOnly( "Day" ) );
            Assert.AreEqual( new TimeOnly( 9, 30 ), reader.GetTimeOnly( 1 ) );
            Assert.AreEqual( new TimeOnly( 9, 30 ), reader.GetNullableTimeOnly( "Starts" ) );
            Assert.IsNull( reader.GetNullableTimeOnly( 2 ) );
            Assert.AreEqual( new DateOnly( 2026, 9, 18 ), reader.GetNullableDateOnly( "Day" ) );
        }

        [TestMethod]
        public void TestDataRecordInterface_DefaultMembers_CastTheValue()
        {
            IFlatFileDataRecord record = new ValueRecord( new DateOnly( 2026, 9, 18 ), new TimeOnly( 9, 30 ) );

            Assert.AreEqual( new DateOnly( 2026, 9, 18 ), record.GetDateOnly( 0 ) );
            Assert.AreEqual( new TimeOnly( 9, 30 ), record.GetTimeOnly( 1 ) );
        }

        public sealed class Appointment
        {
            public string Who { get; set; } = string.Empty;

            public DateOnly Day { get; set; }

            public TimeOnly Starts { get; set; }

            public TimeOnly? Ends { get; set; }

            public DateOnly? Reviewed { get; set; }
        }

        /// <summary>
        ///     An implementation from outside the library that supplies only the required members, so GetDateOnly and
        ///     GetTimeOnly are the interface's defaults.
        /// </summary>
        private sealed class ValueRecord( params object[] values ) : IFlatFileDataRecord
        {
            public object GetValue( int i ) => values[i];

            public int FieldCount => values.Length;

            public object this[int i] => values[i];

            public object this[string name] => throw new NotSupportedException();

            public bool GetBoolean( int i ) => (bool) values[i];

            public byte GetByte( int i ) => (byte) values[i];

            public long GetBytes( int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length ) => throw new NotSupportedException();

            public char GetChar( int i ) => (char) values[i];

            public long GetChars( int i, long fieldoffset, char[]? buffer, int bufferoffset, int length ) => throw new NotSupportedException();

            public System.Data.IDataReader GetData( int i ) => throw new NotSupportedException();

            public string GetDataTypeName( int i ) => values[i].GetType().Name;

            public DateTime GetDateTime( int i ) => (DateTime) values[i];

            public decimal GetDecimal( int i ) => (decimal) values[i];

            public double GetDouble( int i ) => (double) values[i];

            public Type GetFieldType( int i ) => values[i].GetType();

            public float GetFloat( int i ) => (float) values[i];

            public Guid GetGuid( int i ) => (Guid) values[i];

            public short GetInt16( int i ) => (short) values[i];

            public int GetInt32( int i ) => (int) values[i];

            public long GetInt64( int i ) => (long) values[i];

            public string GetName( int i ) => i.ToString( CultureInfo.InvariantCulture );

            public int GetOrdinal( string name ) => int.Parse( name, CultureInfo.InvariantCulture );

            public string GetString( int i ) => (string) values[i];

            public int GetValues( object[] destination )
            {
                Array.Copy( values, destination, Math.Min( values.Length, destination.Length ) );
                return Math.Min( values.Length, destination.Length );
            }

            public bool IsDBNull( int i ) => values[i] is null;

            public DateTimeOffset GetDateTimeOffset( int i ) => (DateTimeOffset) values[i];

            public sbyte GetSByte( int i ) => (sbyte) values[i];

            public TimeSpan GetTimeSpan( int i ) => (TimeSpan) values[i];

            public ushort GetUInt16( int i ) => (ushort) values[i];

            public uint GetUInt32( int i ) => (uint) values[i];

            public ulong GetUInt64( int i ) => (ulong) values[i];
        }
    }
}
