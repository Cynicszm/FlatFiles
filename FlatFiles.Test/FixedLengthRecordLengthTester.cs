using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests how the fixed-length reader treats a record whose length does not match the schema's windows. A short
    ///     record is always refused; a long one is refused only when the option asks for it, because by default the
    ///     characters after the last window are silently ignored.
    /// </summary>
    [TestClass]
    public class FixedLengthRecordLengthTester
    {
        /// <summary>
        ///     Two correct 19-character records: Id (4), Name (10), Qty (5).
        /// </summary>
        private const string Records = "A001Widget    00042\r\nB002Gadget    00007\r\n";

        private static FixedLengthSchema Schema( int quantityWidth )
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "Id" ), new Window( 4 ) );
            schema.AddColumn( new StringColumn( "Name" ), new Window( 10 ) );
            schema.AddColumn( new Int32Column( "Qty" ), new Window( quantityWidth ) { Alignment = FixedAlignment.RightAligned } );
            return schema;
        }

        private static FixedLengthReader Reader( int quantityWidth, FixedLengthOptions options )
        {
            return new FixedLengthReader( new StringReader( Records ), Schema( quantityWidth ), options );
        }

        private static List<int> Quantities( FixedLengthReader reader )
        {
            var quantities = new List<int>();
            while (reader.Read())
            {
                quantities.Add( (int) reader.GetValues()[2] );
            }
            return quantities;
        }

        [TestMethod]
        public void TestRead_LongRecordByDefault_ReadsEachColumnFromItsDeclaredOffsetAndIgnoresTheRest()
        {
            // With the quantity declared one character too narrow, every amount is silently divided by ten.
            var reader = Reader( 4, new FixedLengthOptions() );

            CollectionAssert.AreEqual( new[] { 4, 0 }, Quantities( reader ) );
        }

        [TestMethod]
        public void TestRead_LongRecordWhenRejected_ThrowsWithTheRecordContext()
        {
            var reader = Reader( 4, new FixedLengthOptions { IsLongRecordRejected = true } );

            var exception = Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );

            Assert.AreEqual( 1, exception.RecordContext.PhysicalRecordNumber );
            Assert.AreEqual( "A001Widget    00042", exception.RecordContext.Record );
            StringAssert.Contains( exception.Message, "longer than" );
            StringAssert.Contains( exception.Message, "Record 1" );
        }

        [TestMethod]
        public void TestRead_LongRecordWhenRejected_IsSkippedWhenTheErrorIsHandled()
        {
            var reader = Reader( 4, new FixedLengthOptions { IsLongRecordRejected = true } );
            var rejected = new List<int>();
            reader.RecordError += ( sender, e ) =>
            {
                rejected.Add( ((RecordProcessingException) e.Exception).RecordContext.PhysicalRecordNumber );
                e.IsHandled = true;
            };

            var quantities = Quantities( reader );

            Assert.AreEqual( 0, quantities.Count, "Both records are longer than the schema, so neither should be returned." );
            CollectionAssert.AreEqual( new[] { 1, 2 }, rejected );
        }

        [TestMethod]
        public void TestRead_ShortRecord_IsRefusedWhetherOrNotLongRecordsAre()
        {
            foreach (var rejectLong in new[] { false, true })
            {
                var reader = Reader( 6, new FixedLengthOptions { IsLongRecordRejected = rejectLong } );

                var exception = Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read(), $"IsLongRecordRejected = {rejectLong}" );

                Assert.AreEqual( 1, exception.RecordContext.PhysicalRecordNumber );
                Assert.AreEqual( "A001Widget    00042", exception.RecordContext.Record );
            }
        }

        [TestMethod]
        public void TestRead_RecordExactlyTheSchemaWidthWhenRejected_ReadsNormally()
        {
            var reader = Reader( 5, new FixedLengthOptions { IsLongRecordRejected = true } );

            CollectionAssert.AreEqual( new[] { 42, 7 }, Quantities( reader ) );
        }

        [TestMethod]
        public void TestRead_HeaderRecordLongerThanTheSchema_IsSkippedNotRejected()
        {
            var text = "This header is much longer than the nineteen characters of a record\r\n" + Records;
            var options = new FixedLengthOptions { IsLongRecordRejected = true, IsFirstRecordHeader = true };
            var reader = new FixedLengthReader( new StringReader( text ), Schema( 5 ), options );

            CollectionAssert.AreEqual( new[] { 42, 7 }, Quantities( reader ) );
        }

        [TestMethod]
        public async Task TestReadAsync_LongRecordWhenRejected_Throws()
        {
            var reader = Reader( 4, new FixedLengthOptions { IsLongRecordRejected = true } );

            var exception = await Assert.ThrowsExactlyAsync<RecordProcessingException>( async () => await reader.ReadAsync() );

            Assert.AreEqual( 1, exception.RecordContext.PhysicalRecordNumber );
        }

        [TestMethod]
        public void TestTypeMapperRead_LongRecordWhenRejected_Throws()
        {
            var mapper = FixedLengthTypeMapper.Define<Item>();
            mapper.Property( item => item.Id, new Window( 4 ) );
            mapper.Property( item => item.Name, new Window( 10 ) );
            mapper.Property( item => item.Quantity, new Window( 4 ) { Alignment = FixedAlignment.RightAligned } );
            var options = new FixedLengthOptions { IsLongRecordRejected = true };

            Assert.ThrowsExactly<RecordProcessingException>( () => mapper.Read( new StringReader( Records ), options ).ToList() );
        }

        [TestMethod]
        public void TestClone_CarriesTheOption()
        {
            var options = new FixedLengthOptions { IsLongRecordRejected = true };

            Assert.IsTrue( options.Clone().IsLongRecordRejected );
        }

        public sealed class Item
        {
            public string Id { get; set; }

            public string Name { get; set; }

            public int Quantity { get; set; }
        }
    }
}
