using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Auto-maps an entity with a property of every type the delimited mapper can deduce a column for, nullable and
    ///     not, through the auto-mapped reader, its asynchronous form and the auto-mapped writer, so every entry in the
    ///     column lookup and both member kinds of the writer are exercised.
    /// </summary>
    [TestClass]
    public class AutoMapEverythingTester
    {
        public sealed class Plain
        {
            public bool Bool { get; set; }

            public bool? NullableBool { get; set; }

            public byte[] Bytes { get; set; }

            public byte Byte { get; set; }

            public byte? NullableByte { get; set; }

            public char[] Chars { get; set; }

            public char Char { get; set; }

            public char? NullableChar { get; set; }

            public DateTime DateTime { get; set; }

            public DateTime? NullableDateTime { get; set; }

            public DateTimeOffset DateTimeOffset { get; set; }

            public DateTimeOffset? NullableDateTimeOffset { get; set; }

            public DateOnly DateOnly { get; set; }

            public DateOnly? NullableDateOnly { get; set; }

            public TimeOnly TimeOnly { get; set; }

            public TimeOnly? NullableTimeOnly { get; set; }

            public decimal Decimal { get; set; }

            public decimal? NullableDecimal { get; set; }

            public double Double { get; set; }

            public double? NullableDouble { get; set; }

            public Guid Guid { get; set; }

            public Guid? NullableGuid { get; set; }

            public short Int16 { get; set; }

            public short? NullableInt16 { get; set; }

            public int Int32 { get; set; }

            public int? NullableInt32 { get; set; }

            public long Int64 { get; set; }

            public long? NullableInt64 { get; set; }

            public sbyte SByte { get; set; }

            public sbyte? NullableSByte { get; set; }

            public float Single { get; set; }

            public float? NullableSingle { get; set; }

            public string Text { get; set; }

            public TimeSpan TimeSpan { get; set; }

            public TimeSpan? NullableTimeSpan { get; set; }

            public ushort UInt16 { get; set; }

            public ushort? NullableUInt16 { get; set; }

            public uint UInt32 { get; set; }

            public uint? NullableUInt32 { get; set; }

            public ulong UInt64 { get; set; }

            public ulong? NullableUInt64 { get; set; }

            public int Field;
        }

        private static readonly string Header = string.Join( ",", typeof( Plain ).GetProperties().Select( p => p.Name ).Append( "Field" ) );

        private const string Values = "true,false,abc,7,8,ab,z,y,2026-09-18 10:30:00,2026-09-19 11:00:00,2026-09-18T10:30:00.0000000+01:00,2026-09-19T11:00:00.0000000+01:00,2026-09-18,2026-09-19,10:30,11:00,1.5,2.5,3.5,4.5,12345678-1234-1234-1234-123456789012,12345678-1234-1234-1234-123456789013,16,17,32,33,64,65,-7,-8,5.5,6.5,text,01:30:00,02:30:00,18,19,34,35,66,67,99";

        /// <summary>
        ///     Every nullable slot blank, every non-nullable slot filled.
        /// </summary>
        private const string Blanks = "true,,abc,7,,ab,z,,2026-09-18 10:30:00,,2026-09-18T10:30:00.0000000+01:00,,2026-09-18,,10:30,,1.5,,3.5,,12345678-1234-1234-1234-123456789012,,16,,32,,64,,-7,,5.5,,text,01:30:00,,18,,34,,66,,99";

        private static DelimitedOptions Options => new() { FormatProvider = CultureInfo.InvariantCulture, RecordSeparator = "\n" };

        [TestMethod]
        public void TestAutoMappedReader_EveryColumnType_IsDeducedAndParsed()
        {
            var text = Header + "\n" + Values + "\n" + Blanks + "\n";

            var read = DelimitedTypeMapper.GetAutoMappedReader<Plain>( new StringReader( text ), Options ).ReadAll().ToList();

            Assert.HasCount( 2, read );
            var full = read[0];
            Assert.IsTrue( full.Bool );
            Assert.IsFalse( full.NullableBool );
            CollectionAssert.AreEqual( "abc"u8.ToArray(), full.Bytes, "A byte array column holds the text's UTF-8 bytes." );
            Assert.AreEqual( (byte) 7, full.Byte );
            Assert.AreEqual( (byte) 8, full.NullableByte );
            CollectionAssert.AreEqual( new[] { 'a', 'b' }, full.Chars );
            Assert.AreEqual( 'z', full.Char );
            Assert.AreEqual( 'y', full.NullableChar );
            Assert.AreEqual( new DateTime( 2026, 9, 18, 10, 30, 0 ), full.DateTime );
            Assert.AreEqual( new DateTime( 2026, 9, 19, 11, 0, 0 ), full.NullableDateTime );
            Assert.AreEqual( new DateTimeOffset( 2026, 9, 18, 10, 30, 0, TimeSpan.FromHours( 1 ) ), full.DateTimeOffset );
            Assert.AreEqual( new DateTimeOffset( 2026, 9, 19, 11, 0, 0, TimeSpan.FromHours( 1 ) ), full.NullableDateTimeOffset );
            Assert.AreEqual( new DateOnly( 2026, 9, 18 ), full.DateOnly );
            Assert.AreEqual( new DateOnly( 2026, 9, 19 ), full.NullableDateOnly );
            Assert.AreEqual( new TimeOnly( 10, 30 ), full.TimeOnly );
            Assert.AreEqual( new TimeOnly( 11, 0 ), full.NullableTimeOnly );
            Assert.AreEqual( 1.5m, full.Decimal );
            Assert.AreEqual( 2.5m, full.NullableDecimal );
            Assert.AreEqual( 3.5, full.Double );
            Assert.AreEqual( 4.5, full.NullableDouble );
            Assert.AreEqual( new Guid( "12345678-1234-1234-1234-123456789012" ), full.Guid );
            Assert.AreEqual( new Guid( "12345678-1234-1234-1234-123456789013" ), full.NullableGuid );
            Assert.AreEqual( (short) 16, full.Int16 );
            Assert.AreEqual( (short) 17, full.NullableInt16 );
            Assert.AreEqual( 32, full.Int32 );
            Assert.AreEqual( 33, full.NullableInt32 );
            Assert.AreEqual( 64L, full.Int64 );
            Assert.AreEqual( 65L, full.NullableInt64 );
            Assert.AreEqual( (sbyte) -7, full.SByte );
            Assert.AreEqual( (sbyte) -8, full.NullableSByte );
            Assert.AreEqual( 5.5f, full.Single );
            Assert.AreEqual( 6.5f, full.NullableSingle );
            Assert.AreEqual( "text", full.Text );
            Assert.AreEqual( TimeSpan.FromMinutes( 90 ), full.TimeSpan );
            Assert.AreEqual( TimeSpan.FromMinutes( 150 ), full.NullableTimeSpan );
            Assert.AreEqual( (ushort) 18, full.UInt16 );
            Assert.AreEqual( (ushort) 19, full.NullableUInt16 );
            Assert.AreEqual( 34u, full.UInt32 );
            Assert.AreEqual( 35u, full.NullableUInt32 );
            Assert.AreEqual( 66ul, full.UInt64 );
            Assert.AreEqual( 67ul, full.NullableUInt64 );
            Assert.AreEqual( 99, full.Field, "Public fields are auto-mapped as well as properties." );

            var blank = read[1];
            Assert.IsNull( blank.NullableBool );
            Assert.IsNull( blank.NullableByte );
            Assert.IsNull( blank.NullableChar );
            Assert.IsNull( blank.NullableDateTime );
            Assert.IsNull( blank.NullableDateTimeOffset );
            Assert.IsNull( blank.NullableDateOnly );
            Assert.IsNull( blank.NullableTimeOnly );
            Assert.IsNull( blank.NullableDecimal );
            Assert.IsNull( blank.NullableDouble );
            Assert.IsNull( blank.NullableGuid );
            Assert.IsNull( blank.NullableInt16 );
            Assert.IsNull( blank.NullableInt32 );
            Assert.IsNull( blank.NullableInt64 );
            Assert.IsNull( blank.NullableSByte );
            Assert.IsNull( blank.NullableSingle );
            Assert.IsNull( blank.NullableTimeSpan );
            Assert.IsNull( blank.NullableUInt16 );
            Assert.IsNull( blank.NullableUInt32 );
            Assert.IsNull( blank.NullableUInt64 );
        }

        [TestMethod]
        public async Task TestAutoMappedReaderAsync_ReadsTheSameEntities()
        {
            var text = Header + "\n" + Values + "\n" + Blanks + "\n";

            var reader = await DelimitedTypeMapper.GetAutoMappedReaderAsync<Plain>( new StringReader( text ), Options );
            var count = 0;
            while (await reader.ReadAsync())
            {
                Assert.AreEqual( "text", reader.Current.Text );
                count++;
            }

            Assert.AreEqual( 2, count );
        }

        [TestMethod]
        public void TestAutoMappedWriter_WritesEveryPropertyAndField_AndReadsBack()
        {
            var text = Header + "\n" + Values + "\n" + Blanks + "\n";
            var entities = DelimitedTypeMapper.GetAutoMappedReader<Plain>( new StringReader( text ), Options ).ReadAll().ToList();
            var output = new StringWriter();

            var writer = DelimitedTypeMapper.GetAutoMappedWriter<Plain>( output, Options );
            writer.WriteSchema();
            foreach (var entity in entities)
            {
                writer.Write( entity );
            }
            var written = output.ToString();
            var again = DelimitedTypeMapper.GetAutoMappedReader<Plain>( new StringReader( written ), Options ).ReadAll().ToList();

            Assert.StartsWith( Header + "\n", written, "The header is the property names in declaration order, then the field." );
            Assert.HasCount( 2, again );
            Assert.AreEqual( entities[0].Guid, again[0].Guid );
            Assert.AreEqual( entities[0].Field, again[0].Field );
            Assert.IsNull( again[1].NullableGuid );
            Assert.IsNotNull( writer.GetSchema() );
            Assert.IsNotNull( writer.Writer );
        }

        [TestMethod]
        public void TestAutoMappedWriter_WithAResolver_RenamesAndSkipsColumns()
        {
            var output = new StringWriter();
            var resolver = AutoMapResolver.For( member => member.Name == "Field" ? null : member.Name.ToUpperInvariant(), member => member.Name == "Text" ? 0 : 1 );

            var writer = DelimitedTypeMapper.GetAutoMappedWriter<Plain>( output, Options, resolver );
            writer.WriteSchema();

            var header = output.ToString().TrimEnd( '\n' ).Split( ',' );
            Assert.AreEqual( "TEXT", header[0], "The lowest position sorts first." );
            Assert.IsFalse( header.Contains( "FIELD" ), "A null column name leaves the member out." );
            Assert.IsTrue( header.All( h => h == h.ToUpperInvariant() ) );
        }
    }
}
