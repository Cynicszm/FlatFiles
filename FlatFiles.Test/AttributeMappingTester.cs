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
    }
}
