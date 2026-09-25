using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Drives every member of the dynamic configuration interfaces on both type mappers, which are the runtime-typed
    ///     twins of the generic Property overloads, and reads and writes through the dynamic mapper interfaces so the
    ///     untyped writer and the object-typed read paths are exercised end to end.
    /// </summary>
    [TestClass]
    public class DynamicTypeMapperSurfaceTester
    {
        public enum Colour
        {
            Red,
            Green
        }

        public sealed class Nested
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;
        }

        /// <summary>
        ///     One property of every type the dynamic configuration accepts, plus an enum, an ignored slot and a nested
        ///     object.
        /// </summary>
        public sealed class Everything
        {
            public bool Bool { get; set; }

            public byte[] Bytes { get; set; } = [];

            public byte Byte { get; set; }

            public sbyte SByte { get; set; }

            public char[] Chars { get; set; } = [];

            public char Char { get; set; }

            public DateTime DateTime { get; set; }

            public DateOnly DateOnly { get; set; }

            public TimeOnly TimeOnly { get; set; }

            public DateTimeOffset DateTimeOffset { get; set; }

            public decimal Decimal { get; set; }

            public double Double { get; set; }

            public Guid Guid { get; set; }

            public short Int16 { get; set; }

            public ushort UInt16 { get; set; }

            public int Int32 { get; set; }

            public uint UInt32 { get; set; }

            public long Int64 { get; set; }

            public ulong UInt64 { get; set; }

            public float Single { get; set; }

            public string Text { get; set; } = string.Empty;

            public TimeSpan TimeSpan { get; set; }

            public Colour Colour { get; set; }

            public string Custom { get; set; } = string.Empty;

            public Nested Nested { get; set; } = null!;

            public Nested Other { get; set; } = null!;
        }

        private static readonly Guid SampleGuid = new( "12345678-1234-1234-1234-123456789012" );

        private static Everything Sample()
        {
            return new Everything
            {
                Bool = true,
                Bytes = [ 1, 2, 3 ],
                Byte = 7,
                SByte = -7,
                Chars = [ 'a', 'b' ],
                Char = 'z',
                DateTime = new DateTime( 2026, 9, 18, 10, 30, 0 ),
                DateOnly = new DateOnly( 2026, 9, 18 ),
                TimeOnly = new TimeOnly( 10, 30 ),
                DateTimeOffset = new DateTimeOffset( 2026, 9, 18, 10, 30, 0, TimeSpan.FromHours( 1 ) ),
                Decimal = 1.5m,
                Double = 2.5,
                Guid = SampleGuid,
                Int16 = 16,
                UInt16 = 17,
                Int32 = 32,
                UInt32 = 33,
                Int64 = 64,
                UInt64 = 65,
                Single = 3.5f,
                Text = "text",
                TimeSpan = TimeSpan.FromMinutes( 90 ),
                Colour = Colour.Green,
                Custom = "custom",
                Nested = new Nested { Id = 1, Name = "inner" },
                Other = new Nested { Id = 2, Name = "other" }
            };
        }

        private static void AssertSample( Everything read )
        {
            var expected = Sample();
            Assert.AreEqual( expected.Bool, read.Bool );
            CollectionAssert.AreEqual( expected.Bytes, read.Bytes );
            Assert.AreEqual( expected.Byte, read.Byte );
            Assert.AreEqual( expected.SByte, read.SByte );
            CollectionAssert.AreEqual( expected.Chars, read.Chars );
            Assert.AreEqual( expected.Char, read.Char );
            Assert.AreEqual( expected.DateTime, read.DateTime );
            Assert.AreEqual( expected.DateOnly, read.DateOnly );
            Assert.AreEqual( expected.TimeOnly, read.TimeOnly );
            Assert.AreEqual( expected.DateTimeOffset, read.DateTimeOffset );
            Assert.AreEqual( expected.Decimal, read.Decimal );
            Assert.AreEqual( expected.Double, read.Double );
            Assert.AreEqual( expected.Guid, read.Guid );
            Assert.AreEqual( expected.Int16, read.Int16 );
            Assert.AreEqual( expected.UInt16, read.UInt16 );
            Assert.AreEqual( expected.Int32, read.Int32 );
            Assert.AreEqual( expected.UInt32, read.UInt32 );
            Assert.AreEqual( expected.Int64, read.Int64 );
            Assert.AreEqual( expected.UInt64, read.UInt64 );
            Assert.AreEqual( expected.Single, read.Single );
            Assert.AreEqual( expected.Text, read.Text );
            Assert.AreEqual( expected.TimeSpan, read.TimeSpan );
            Assert.AreEqual( expected.Colour, read.Colour );
            Assert.AreEqual( "CUSTOM", read.Custom, "The custom mapping's reader upper-cases." );
            Assert.AreEqual( expected.Nested.Id, read.Nested.Id );
            Assert.AreEqual( expected.Nested.Name, read.Nested.Name );
            Assert.AreEqual( expected.Other.Id, read.Other.Id );
            Assert.AreEqual( expected.Other.Name, read.Other.Name );
        }

        private static IDynamicDelimitedTypeMapper DelimitedMapper( bool optimised = true )
        {
            var mapper = DelimitedTypeMapper.DefineDynamic( typeof( Everything ) );
            mapper.UseFactory( typeof( Everything ), () => new Everything() );
            mapper.OptimiseMapping( optimised );
            mapper.BooleanProperty( "Bool" );
            mapper.ByteArrayProperty( "Bytes" );
            mapper.ByteProperty( "Byte" );
            mapper.SByteProperty( "SByte" );
            mapper.CharArrayProperty( "Chars" );
            mapper.CharProperty( "Char" );
            mapper.DateTimeProperty( "DateTime" ).InputFormat( "yyyy-MM-dd HH:mm" ).OutputFormat( "yyyy-MM-dd HH:mm" );
            mapper.DateOnlyProperty( "DateOnly" ).InputFormat( "yyyy-MM-dd" ).OutputFormat( "yyyy-MM-dd" );
            mapper.TimeOnlyProperty( "TimeOnly" ).InputFormat( "HH:mm" ).OutputFormat( "HH:mm" );
            mapper.DateTimeOffsetProperty( "DateTimeOffset" ).InputFormat( "o" ).OutputFormat( "o" );
            mapper.DecimalProperty( "Decimal" ).FormatProvider( CultureInfo.InvariantCulture );
            mapper.DoubleProperty( "Double" ).FormatProvider( CultureInfo.InvariantCulture );
            mapper.GuidProperty( "Guid" );
            mapper.Int16Property( "Int16" );
            mapper.UInt16Property( "UInt16" );
            mapper.Int32Property( "Int32" );
            mapper.UInt32Property( "UInt32" );
            mapper.Int64Property( "Int64" );
            mapper.UInt64Property( "UInt64" );
            mapper.SingleProperty( "Single" ).FormatProvider( CultureInfo.InvariantCulture );
            mapper.StringProperty( "Text" );
            mapper.TimeSpanProperty( "TimeSpan" );
            mapper.EnumProperty<Colour>( "Colour" );
            mapper.Ignored();
            mapper.CustomMapping( new StringColumn( "Custom" ) )
                .WithReader( ( e, v ) => ((Everything) e!).Custom = ((string) v!).ToUpperInvariant() )
                .WithWriter( e => ((Everything) e!).Custom );
            var nested = DelimitedTypeMapper.Define<Nested>();
            nested.Property( n => n.Id );
            nested.Property( n => n.Name );
            mapper.ComplexProperty( "Nested", nested ).WithOptions( new DelimitedOptions { Separator = ";" } );
            var other = FixedLengthTypeMapper.Define<Nested>();
            other.Property( n => n.Id, 3 );
            other.Property( n => n.Name, 8 );
            mapper.ComplexProperty( "Other", other );
            return mapper;
        }

        private static IDynamicFixedLengthTypeMapper FixedLengthMapper( bool optimised = true )
        {
            var mapper = FixedLengthTypeMapper.DefineDynamic( typeof( Everything ) );
            mapper.UseFactory( typeof( Everything ), () => new Everything() );
            mapper.OptimiseMapping( optimised );
            mapper.BooleanProperty( "Bool", 5 );
            mapper.ByteArrayProperty( "Bytes", 6 );
            mapper.ByteProperty( "Byte", 3 );
            mapper.SByteProperty( "SByte", 4 );
            mapper.CharArrayProperty( "Chars", 2 );
            mapper.CharProperty( "Char", 1 );
            mapper.DateTimeProperty( "DateTime", 16 ).InputFormat( "yyyy-MM-dd HH:mm" ).OutputFormat( "yyyy-MM-dd HH:mm" );
            mapper.DateOnlyProperty( "DateOnly", 10 ).InputFormat( "yyyy-MM-dd" ).OutputFormat( "yyyy-MM-dd" );
            mapper.TimeOnlyProperty( "TimeOnly", 5 ).InputFormat( "HH:mm" ).OutputFormat( "HH:mm" );
            mapper.DateTimeOffsetProperty( "DateTimeOffset", 33 ).InputFormat( "o" ).OutputFormat( "o" );
            mapper.DecimalProperty( "Decimal", 6 ).FormatProvider( CultureInfo.InvariantCulture );
            mapper.DoubleProperty( "Double", 6 ).FormatProvider( CultureInfo.InvariantCulture );
            mapper.GuidProperty( "Guid", 36 );
            mapper.Int16Property( "Int16", 4 );
            mapper.UInt16Property( "UInt16", 4 );
            mapper.Int32Property( "Int32", 4 );
            mapper.UInt32Property( "UInt32", 4 );
            mapper.Int64Property( "Int64", 4 );
            mapper.UInt64Property( "UInt64", 4 );
            mapper.SingleProperty( "Single", 6 ).FormatProvider( CultureInfo.InvariantCulture );
            mapper.StringProperty( "Text", 6 );
            mapper.TimeSpanProperty( "TimeSpan", 8 );
            mapper.EnumProperty<Colour>( "Colour", 6 );
            mapper.Ignored( 2 );
            mapper.CustomMapping( new StringColumn( "Custom" ), 8 )
                .WithReader( ( _, e, v ) => ((Everything) e!).Custom = ((string) v!).ToUpperInvariant() )
                .WithWriter( ( _, e ) => ((Everything) e!).Custom );
            var nested = DelimitedTypeMapper.Define<Nested>();
            nested.Property( n => n.Id );
            nested.Property( n => n.Name );
            mapper.ComplexProperty( "Nested", nested, 12 ).WithOptions( new DelimitedOptions { Separator = ";" } );
            var other = FixedLengthTypeMapper.Define<Nested>();
            other.Property( n => n.Id, 3 );
            other.Property( n => n.Name, 8 );
            mapper.ComplexProperty( "Other", other, 11 );
            return mapper;
        }

        [TestMethod]
        public void TestDelimitedDynamic_EveryMember_RoundTrips()
        {
            var mapper = DelimitedMapper();
            var options = new DelimitedOptions { RecordSeparator = "\n" };
            var writer = new StringWriter();

            mapper.Write( writer, [ Sample() ], options );
            var read = mapper.Read( new StringReader( writer.ToString() ), options ).Cast<Everything>().Single();

            Assert.AreEqual( 27, mapper.GetSchema().ColumnDefinitions.Count, "Every property, the ignored slot, the custom column and both complex columns." );
            AssertSample( read );
        }

        [TestMethod]
        public void TestDelimitedDynamic_Unoptimised_RoundTrips()
        {
            var mapper = DelimitedMapper( false );
            var options = new DelimitedOptions { RecordSeparator = "\n" };
            var writer = new StringWriter();

            mapper.Write( writer, [ Sample() ], options );
            var read = mapper.Read( new StringReader( writer.ToString() ), options ).Cast<Everything>().Single();

            AssertSample( read );
        }

        [TestMethod]
        public void TestFixedLengthDynamic_EveryMember_RoundTrips()
        {
            var mapper = FixedLengthMapper();
            var options = new FixedLengthOptions { RecordSeparator = "\n" };
            var writer = new StringWriter();

            mapper.Write( writer, [ Sample() ], options );
            var read = mapper.Read( new StringReader( writer.ToString() ), options ).Cast<Everything>().Single();

            Assert.AreEqual( 27, mapper.GetSchema().ColumnDefinitions.Count );
            AssertSample( read );
        }

        [TestMethod]
        public void TestFixedLengthDynamic_Unoptimised_RoundTrips()
        {
            var mapper = FixedLengthMapper( false );
            var options = new FixedLengthOptions { RecordSeparator = "\n" };
            var writer = new StringWriter();

            mapper.Write( writer, [ Sample() ], options );
            var read = mapper.Read( new StringReader( writer.ToString() ), options ).Cast<Everything>().Single();

            AssertSample( read );
        }

        [TestMethod]
        public async Task TestDelimitedDynamic_AsyncOverloads_RoundTrip()
        {
            var mapper = DelimitedMapper();
            var options = new DelimitedOptions { RecordSeparator = "\n" };
            var writer = new StringWriter();
            await mapper.WriteAsync( writer, [ Sample() ], options );
            var text = writer.ToString();
            var asyncWriter = new StringWriter();
            await mapper.WriteAsync( asyncWriter, Stream( Sample() ), options, CancellationToken.None );

            Assert.AreEqual( text, asyncWriter.ToString() );
            var count = 0;
            await foreach (var entity in mapper.ReadAsync( new StringReader( text ), options ))
            {
                AssertSample( (Everything) entity );
                count++;
            }
            Assert.AreEqual( 1, count );
        }

        [TestMethod]
        public async Task TestFixedLengthDynamic_AsyncOverloads_RoundTrip()
        {
            var mapper = FixedLengthMapper();
            var options = new FixedLengthOptions { RecordSeparator = "\n" };
            var writer = new StringWriter();
            await mapper.WriteAsync( writer, [ Sample() ], options );
            var text = writer.ToString();
            var asyncWriter = new StringWriter();
            await mapper.WriteAsync( asyncWriter, Stream( Sample() ), options, CancellationToken.None );

            Assert.AreEqual( text, asyncWriter.ToString() );
            var count = 0;
            await foreach (var entity in mapper.ReadAsync( new StringReader( text ), options, CancellationToken.None ))
            {
                AssertSample( (Everything) entity );
                count++;
            }
            Assert.AreEqual( 1, count );
        }

        [TestMethod]
        public async Task TestDynamicWriters_ExposeTheUnderlyingWriterAndEvents()
        {
            var mapper = DelimitedMapper();
            var options = new DelimitedOptions { RecordSeparator = "\n", IsFirstRecordSchema = true };
            var text = new StringWriter();
            var writer = mapper.GetWriter( text, options );
            var columnErrors = 0;
            var recordErrors = 0;
            EventHandler<ColumnErrorEventArgs> onColumn = ( _, _ ) => columnErrors++;
            EventHandler<RecordErrorEventArgs> onRecord = ( _, _ ) => recordErrors++;
            writer.ColumnError += onColumn;
            writer.RecordError += onRecord;

            Assert.IsNotNull( writer.Writer );
            Assert.AreEqual( 27, writer.GetSchema()!.ColumnDefinitions.Count );
            writer.WriteSchema();
            await writer.WriteSchemaAsync();
            writer.Write( Sample() );
            await writer.WriteAsync( Sample() );
            writer.ColumnError -= onColumn;
            writer.RecordError -= onRecord;

            Assert.AreEqual( 0, columnErrors + recordErrors );
            Assert.AreEqual( 3, text.ToString().Split( '\n', StringSplitOptions.RemoveEmptyEntries ).Length, "One header, written once however often it is asked for, and two records." );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( () => writer.WriteAsync( Sample(), new CancellationToken( true ) ) );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( () => writer.WriteSchemaAsync( new CancellationToken( true ) ) );
        }

        [TestMethod]
        public void TestDynamic_WrongPropertyType_Throws()
        {
            var mapper = DelimitedTypeMapper.DefineDynamic( typeof( Everything ) );

            Assert.ThrowsExactly<ArgumentException>( () => mapper.Int32Property( "Text" ), "Text is a string, not an int." );
            Assert.ThrowsExactly<ArgumentException>( () => mapper.StringProperty( "NoSuchMember" ) );
        }

        [TestMethod]
        public void TestDynamic_UseFactory_MakesTheEntitiesItReturns()
        {
            var mapper = DelimitedTypeMapper.DefineDynamic( typeof( Nested ) );
            var made = 0;
            mapper.UseFactory( typeof( Nested ), () => { made++; return new Nested { Name = "preset" }; } );
            mapper.Int32Property( "Id" );

            var read = mapper.Read( new StringReader( "1\n2\n" ) ).Cast<Nested>().ToList();

            Assert.AreEqual( 2, made );
            Assert.IsTrue( read.All( n => n.Name == "preset" ) );
        }

        private static async IAsyncEnumerable<object> Stream( params object[] items )
        {
            foreach (var item in items)
            {
                await Task.Yield();
                yield return item;
            }
        }
    }
}
