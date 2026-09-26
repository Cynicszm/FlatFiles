using System;
using System.IO;
using System.Linq;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the mappings built from a type's attributes rather than from fluent calls: what the attributes say,
    ///     what they refuse to guess, and that what comes back is an ordinary mapper.
    /// </summary>
    [TestClass]
    public class AttributeMappingTester
    {
        [TestMethod]
        public void TestDelimited_RoundTripsThroughTheAttributes()
        {
            var mapper = DelimitedTypeMapper.DefineFromAttributes<Customer>();
            var customer = new Customer { CustomerId = 7, Name = "Bob", Created = new DateTime( 2026, 9, 26 ) };

            var writer = new StringWriter();
            mapper.Write( writer, [customer], Options );

            Assert.AreEqual( "7,Bob,20260926\r\n", writer.ToString() );

            var read = mapper.Read( new StringReader( writer.ToString() ), Options ).Single();
            Assert.AreEqual( 7, read.CustomerId );
            Assert.AreEqual( "Bob", read.Name );
            Assert.AreEqual( new DateTime( 2026, 9, 26 ), read.Created );
        }

        [TestMethod]
        public void TestDelimited_TheAttributeNamesTheColumns()
        {
            var mapper = DelimitedTypeMapper.DefineFromAttributes<Customer>();

            var names = mapper.GetSchema().ColumnDefinitions.Select( x => x.ColumnName ).ToList();

            CollectionAssert.AreEqual( new[] { "customer_id", "name", "created" }, names );
        }

        [TestMethod]
        public void TestDelimited_TheOrderIsTheAttributesAndNotReflection()
        {
            var mapper = DelimitedTypeMapper.DefineFromAttributes<Reordered>();

            var names = mapper.GetSchema().ColumnDefinitions.Select( x => x.ColumnName ).ToList();

            CollectionAssert.AreEqual( new[] { "Last", "Middle", "First" }, names, "The columns should be in the order the attributes gave, not the order the members are declared in." );
        }

        [TestMethod]
        public void TestDelimited_AMemberWithNoOrderComesAfterTheOnesThatHaveOne()
        {
            var mapper = DelimitedTypeMapper.DefineFromAttributes<PartlyOrdered>();

            var names = mapper.GetSchema().ColumnDefinitions.Select( x => x.ColumnName ).ToList();

            Assert.AreEqual( "Second", names[0] );
            Assert.AreEqual( "Loose", names[^1] );
        }

        [TestMethod]
        public void TestDelimited_TheMapperItReturnsIsAnOrdinaryOne()
        {
            var mapper = DelimitedTypeMapper.DefineFromAttributes<Customer>();
            // Anything the attributes do not say is still fluent, and a later call wins.
            ( (IDynamicDelimitedTypeConfiguration) mapper ).Int32Property( nameof( Customer.CustomerId ) ).ColumnName( "id" );

            var names = mapper.GetSchema().ColumnDefinitions.Select( x => x.ColumnName ).ToList();

            Assert.AreEqual( "id", names[0] );
            Assert.HasCount( 3, names, "Configuring a member again should rename its column rather than add another." );
        }

        [TestMethod]
        public void TestDelimited_NoAttributesAtAll_Throws()
        {
            var exception = Assert.ThrowsExactly<FlatFileException>( () => DelimitedTypeMapper.DefineFromAttributes<Unmarked>() );

            StringAssert.Contains( exception.Message, nameof( Unmarked ) );
        }

        [TestMethod]
        public void TestDelimited_TwoColumnsClaimingTheSameOrder_Throws()
        {
            var exception = Assert.ThrowsExactly<FlatFileException>( () => DelimitedTypeMapper.DefineFromAttributes<Clashing>() );

            StringAssert.Contains( exception.Message, "1" );
        }

        [TestMethod]
        public void TestDelimited_ATypeNoColumnReads_ThrowsNamingTheMember()
        {
            var exception = Assert.ThrowsExactly<FlatFileException>( () => DelimitedTypeMapper.DefineFromAttributes<Unreadable>() );

            StringAssert.Contains( exception.Message, nameof( Unreadable.Nested ) );
        }

        [TestMethod]
        public void TestDelimited_NullableAndEnumMembersMap()
        {
            var mapper = DelimitedTypeMapper.DefineFromAttributes<Mixed>();
            var writer = new StringWriter();

            mapper.Write( writer, [new Mixed { Amount = null, Status = Status.Closed }], Options );

            // An enum column writes its numeric value unless a formatter says otherwise, which the attributes
            // do not - a caller who wants the name sets one fluently, as on any hand-written mapping.
            Assert.AreEqual( ",1\r\n", writer.ToString() );
        }

        [TestMethod]
        public void TestFixedLength_RoundTripsThroughTheAttributes()
        {
            var mapper = FixedLengthTypeMapper.DefineFromAttributes<Record>();
            var record = new Record { Code = "AB", Balance = 12.5m, Booked = new DateTime( 2026, 9, 26 ) };

            var writer = new StringWriter();
            mapper.Write( writer, [record], FixedOptions );

            Assert.AreEqual( "AB          12.5020260926\r\n", writer.ToString() );

            var read = mapper.Read( new StringReader( writer.ToString() ), FixedOptions ).Single();
            Assert.AreEqual( "AB", read.Code );
            Assert.AreEqual( 12.5m, read.Balance );
            Assert.AreEqual( new DateTime( 2026, 9, 26 ), read.Booked );
        }

        [TestMethod]
        public void TestFixedLength_AnIgnoredWindowLeavesItsGap()
        {
            var mapper = FixedLengthTypeMapper.DefineFromAttributes<WithFiller>();

            var writer = new StringWriter();
            mapper.Write( writer, [new WithFiller { Code = "AB", Tail = "Z" }], FixedOptions );

            Assert.AreEqual( "AB     Z\r\n", writer.ToString(), "The five characters between the two columns belong to nothing and should be written as blank." );
        }

        [TestMethod]
        public void TestFixedLength_AMemberWithNoWindow_Throws()
        {
            var exception = Assert.ThrowsExactly<FlatFileException>( () => FixedLengthTypeMapper.DefineFromAttributes<NoWindow>() );

            StringAssert.Contains( exception.Message, nameof( NoWindow.Code ) );
        }

        [TestMethod]
        public void TestFixedLength_AMemberWithNoOrder_Throws()
        {
            var exception = Assert.ThrowsExactly<FlatFileException>( () => FixedLengthTypeMapper.DefineFromAttributes<NoOrder>() );

            StringAssert.Contains( exception.Message, nameof( NoOrder.Code ) );
        }

        [TestMethod]
        public void TestFixedLength_TheWindowsAlignmentIsHonoured()
        {
            var mapper = FixedLengthTypeMapper.DefineFromAttributes<RightAligned>();

            var writer = new StringWriter();
            mapper.Write( writer, [new RightAligned { Code = "AB" }], FixedOptions );

            Assert.AreEqual( "00000000AB\r\n", writer.ToString() );
        }

        [TestMethod]
        public void TestDelimited_EveryTypeTheAttributesSupport_GetsItsColumn()
        {
            // One member of every kind the attributes can map, so that the type a member takes and the column it
            // gets are held together rather than checked one convenient type at a time.
            var mapper = DelimitedTypeMapper.DefineFromAttributes<EveryType>();

            var columns = mapper.GetSchema().ColumnDefinitions.Select( x => x.GetType().Name ).ToList();

            CollectionAssert.AreEqual( ExpectedColumns, columns );
        }

        [TestMethod]
        public void TestFixedLength_EveryTypeTheAttributesSupport_GetsItsColumn()
        {
            var mapper = FixedLengthTypeMapper.DefineFromAttributes<EveryFixedType>();

            var columns = mapper.GetSchema().ColumnDefinitions.Select( x => x.GetType().Name ).ToList();

            CollectionAssert.AreEqual( ExpectedColumns, columns );
        }

        [TestMethod]
        public void TestFixedLength_EveryTypeTheAttributesSupport_RoundTrips()
        {
            var mapper = FixedLengthTypeMapper.DefineFromAttributes<EveryFixedType>();
            var original = new EveryFixedType
            {
                Flag = true,
                Small = 3,
                Signed = -4,
                Letter = 'x',
                Letters = ['a', 'b'],
                Bytes = [1, 2],
                Day = new DateOnly( 2026, 9, 26 ),
                Moment = new DateTime( 2026, 9, 26 ),
                Offset = new DateTimeOffset( new DateTime( 2026, 9, 26 ), TimeSpan.Zero ),
                Amount = 1.5m,
                Wide = 2.5,
                Identifier = Guid.Empty,
                Short = 5,
                Whole = 6,
                Long = 7,
                Narrow = 8.5f,
                Text = "hi",
                Clock = new TimeOnly( 1, 2 ),
                Elapsed = TimeSpan.FromMinutes( 3 ),
                UShort = 9,
                UInt = 10,
                ULong = 11,
                Status = Status.Closed
            };

            var writer = new StringWriter();
            mapper.Write( writer, [original], FixedOptions );
            var read = mapper.Read( new StringReader( writer.ToString() ), FixedOptions ).Single();

            Assert.IsTrue( read.Flag );
            Assert.AreEqual( 'x', read.Letter );
            Assert.AreEqual( new DateOnly( 2026, 9, 26 ), read.Day );
            Assert.AreEqual( 1.5m, read.Amount );
            Assert.AreEqual( "hi", read.Text );
            Assert.AreEqual( Status.Closed, read.Status );
            Assert.AreEqual( TimeSpan.FromMinutes( 3 ), read.Elapsed );
        }

        private static readonly string[] ExpectedColumns =
        [
            "BooleanColumn", "ByteColumn", "SByteColumn", "CharColumn", "CharArrayColumn", "ByteArrayColumn",
            "DateOnlyColumn", "DateTimeColumn", "DateTimeOffsetColumn", "DecimalColumn", "DoubleColumn",
            "GuidColumn", "Int16Column", "Int32Column", "Int64Column", "SingleColumn", "StringColumn",
            "TimeOnlyColumn", "TimeSpanColumn", "UInt16Column", "UInt32Column", "UInt64Column", "EnumColumn`1"
        ];

        private static DelimitedOptions Options => new() { RecordSeparator = "\r\n" };

        private static FixedLengthOptions FixedOptions => new() { RecordSeparator = "\r\n" };

        public class Customer
        {
            [Column( "customer_id", Order = 0 )]
            public int CustomerId { get; set; }

            [Column( "name", Order = 1 )]
            public string Name { get; set; } = string.Empty;

            [Column( "created", Order = 2, Format = "yyyyMMdd" )]
            public DateTime Created { get; set; }
        }

        public class Reordered
        {
            [Column( Order = 2 )]
            public string First { get; set; } = string.Empty;

            [Column( Order = 1 )]
            public string Middle { get; set; } = string.Empty;

            [Column( Order = 0 )]
            public string Last { get; set; } = string.Empty;
        }

        public class PartlyOrdered
        {
            [Column]
            public string Loose { get; set; } = string.Empty;

            [Column( Order = 0 )]
            public string Second { get; set; } = string.Empty;
        }

        public class Unmarked
        {
            public string Name { get; set; } = string.Empty;
        }

        public class Clashing
        {
            [Column( Order = 1 )]
            public string One { get; set; } = string.Empty;

            [Column( Order = 1 )]
            public string Two { get; set; } = string.Empty;
        }

        public class Unreadable
        {
            [Column]
            public Unmarked Nested { get; set; } = new();
        }

        public class Mixed
        {
            [Column( Order = 0 )]
            public decimal? Amount { get; set; }

            [Column( Order = 1 )]
            public Status Status { get; set; }
        }

        public enum Status
        {
            Open,
            Closed
        }

        public class Record
        {
            [Column( Order = 0 )]
            [Window( 12 )]
            public string Code { get; set; } = string.Empty;

            [Column( Order = 1, Format = "0.00" )]
            [Window( 5 )]
            public decimal Balance { get; set; }

            [Column( Order = 2, Format = "yyyyMMdd" )]
            [Window( 8 )]
            public DateTime Booked { get; set; }
        }

        [IgnoredWindow( 1, 5 )]
        public class WithFiller
        {
            [Column( Order = 0 )]
            [Window( 2 )]
            public string Code { get; set; } = string.Empty;

            [Column( Order = 2 )]
            [Window( 1 )]
            public string Tail { get; set; } = string.Empty;
        }

        public class NoWindow
        {
            [Column( Order = 0 )]
            public string Code { get; set; } = string.Empty;
        }

        public class NoOrder
        {
            [Column]
            [Window( 4 )]
            public string Code { get; set; } = string.Empty;
        }

        public class RightAligned
        {
            [Column( Order = 0 )]
            [Window( 10, Alignment = FixedAlignment.RightAligned, FillCharacter = '0' )]
            public string Code { get; set; } = string.Empty;
        }

        public class EveryType
        {
            [Column( Order = 0 )] public bool Flag { get; set; }
            [Column( Order = 1 )] public byte Small { get; set; }
            [Column( Order = 2 )] public sbyte Signed { get; set; }
            [Column( Order = 3 )] public char Letter { get; set; }
            [Column( Order = 4 )] public char[] Letters { get; set; } = [];
            [Column( Order = 5 )] public byte[] Bytes { get; set; } = [];
            [Column( Order = 6 )] public DateOnly Day { get; set; }
            [Column( Order = 7 )] public DateTime Moment { get; set; }
            [Column( Order = 8 )] public DateTimeOffset Offset { get; set; }
            [Column( Order = 9 )] public decimal Amount { get; set; }
            [Column( Order = 10 )] public double Wide { get; set; }
            [Column( Order = 11 )] public Guid Identifier { get; set; }
            [Column( Order = 12 )] public short Short { get; set; }
            [Column( Order = 13 )] public int Whole { get; set; }
            [Column( Order = 14 )] public long Long { get; set; }
            [Column( Order = 15 )] public float Narrow { get; set; }
            [Column( Order = 16 )] public string Text { get; set; } = string.Empty;
            [Column( Order = 17 )] public TimeOnly Clock { get; set; }
            [Column( Order = 18 )] public TimeSpan Elapsed { get; set; }
            [Column( Order = 19 )] public ushort UShort { get; set; }
            [Column( Order = 20 )] public uint UInt { get; set; }
            [Column( Order = 21 )] public ulong ULong { get; set; }
            [Column( Order = 22 )] public Status Status { get; set; }
        }

        public class EveryFixedType
        {
            [Column( Order = 0 ), Window( 6 )] public bool Flag { get; set; }
            [Column( Order = 1 ), Window( 4 )] public byte Small { get; set; }
            [Column( Order = 2 ), Window( 4 )] public sbyte Signed { get; set; }
            [Column( Order = 3 ), Window( 1 )] public char Letter { get; set; }
            [Column( Order = 4 ), Window( 2 )] public char[] Letters { get; set; } = [];
            [Column( Order = 5 ), Window( 2 )] public byte[] Bytes { get; set; } = [];
            [Column( Order = 6, Format = "yyyyMMdd" ), Window( 8 )] public DateOnly Day { get; set; }
            [Column( Order = 7, Format = "yyyyMMdd" ), Window( 8 )] public DateTime Moment { get; set; }
            [Column( Order = 8, Format = "yyyyMMdd" ), Window( 8 )] public DateTimeOffset Offset { get; set; }
            [Column( Order = 9 ), Window( 8 )] public decimal Amount { get; set; }
            [Column( Order = 10 ), Window( 8 )] public double Wide { get; set; }
            [Column( Order = 11, Format = "N" ), Window( 32 )] public Guid Identifier { get; set; }
            [Column( Order = 12 ), Window( 6 )] public short Short { get; set; }
            [Column( Order = 13 ), Window( 6 )] public int Whole { get; set; }
            [Column( Order = 14 ), Window( 6 )] public long Long { get; set; }
            [Column( Order = 15 ), Window( 8 )] public float Narrow { get; set; }
            [Column( Order = 16 ), Window( 6 )] public string Text { get; set; } = string.Empty;
            [Column( Order = 17, Format = "HHmm" ), Window( 4 )] public TimeOnly Clock { get; set; }
            [Column( Order = 18 ), Window( 16 )] public TimeSpan Elapsed { get; set; }
            [Column( Order = 19 ), Window( 6 )] public ushort UShort { get; set; }
            [Column( Order = 20 ), Window( 6 )] public uint UInt { get; set; }
            [Column( Order = 21 ), Window( 6 )] public ulong ULong { get; set; }
            [Column( Order = 22 ), Window( 4 )] public Status Status { get; set; }
        }
    }
}
