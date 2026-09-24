using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the ragged-right option: every column but the last has a fixed width and the last runs to the end of
    ///     the record, however long or short. A record that ends earlier still is read as far as it goes, a window it
    ///     ends inside takes what is there, a window it never reaches is null, and the writer writes the last column as
    ///     formatted, neither padded nor truncated.
    /// </summary>
    [TestClass]
    public class FixedLengthRaggedRightTester
    {
        /// <summary>
        ///     Id (4), Name (10), Qty (5, right-aligned), Note (6): 25 characters when complete. The second record stops
        ///     after the quantity, the third inside it, the fourth after the id.
        /// </summary>
        private const string Records = "A001Widget       42Note A\r\n"
                                       + "B002Gadget        7\r\n"
                                       + "C003Gizmo         1\r\n"
                                       + "D004\r\n";

        private static FixedLengthOptions Ragged( bool isRaggedRight = true )
        {
            return new FixedLengthOptions { IsRaggedRight = isRaggedRight };
        }

        private static FixedLengthSchema Schema()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "Id" ), new Window( 4 ) );
            schema.AddColumn( new StringColumn( "Name" ), new Window( 10 ) );
            schema.AddColumn( new Int32Column( "Qty" ), new Window( 5 ) { Alignment = FixedAlignment.RightAligned } );
            schema.AddColumn( new StringColumn( "Note" ), new Window( 6 ) );
            return schema;
        }

        private static List<object?[]> ReadAll( FixedLengthReader reader )
        {
            List<object?[]> records = [];
            while (reader.Read())
            {
                records.Add( reader.GetValues() );
            }
            return records;
        }

        [TestMethod]
        public void TestRead_ShortRecords_ReadWhatIsThereAndLeaveTheRestNull()
        {
            var reader = new FixedLengthReader( new StringReader( Records ), Schema(), Ragged() );

            var records = ReadAll( reader );

            Assert.HasCount( 4, records );
            CollectionAssert.AreEqual( new object[] { "A001", "Widget", 42, "Note A" }, records[0] );
            CollectionAssert.AreEqual( new object[] { "B002", "Gadget", 7, null! }, records[1], "A window the record never reaches is null." );
            CollectionAssert.AreEqual( new object[] { "C003", "Gizmo", 1, null! }, records[2], "A window the record ends inside takes the characters that are there." );
            CollectionAssert.AreEqual( new object[] { "D004", null!, null!, null! }, records[3] );
        }

        [TestMethod]
        public void TestRead_ShortRecordWithoutTheOption_IsStillRefused()
        {
            var reader = new FixedLengthReader( new StringReader( Records ), Schema(), Ragged( false ) );

            Assert.IsTrue( reader.Read(), "The complete first record reads normally." );
            var exception = Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );

            Assert.AreEqual( 2, exception.RecordContext.PhysicalRecordNumber );
            Assert.AreEqual( "B002Gadget        7", exception.RecordContext.Record );
        }

        [TestMethod]
        public void TestRead_RecordPartitioned_SeesAnEmptyRawValueForEachMissingWindow()
        {
            var reader = new FixedLengthReader( new StringReader( Records ), Schema(), Ragged() );
            List<string[]> partitions = [];
            reader.RecordPartitioned += ( _, e ) => partitions.Add( e.Values );

            ReadAll( reader );

            Assert.HasCount( 4, partitions );
            CollectionAssert.AreEqual( new[] { "B002", "Gadget", "7", "" }, partitions[1] );
            CollectionAssert.AreEqual( new[] { "D004", "", "", "" }, partitions[3] );
        }

        [TestMethod]
        public void TestRead_LongRecord_LastColumnRunsToTheEndOfTheRecord()
        {
            const string text = "A001Widget       42Note A, which runs past its six characters  \r\n";
            var reader = new FixedLengthReader( new StringReader( text ), Schema(), Ragged() );

            CollectionAssert.AreEqual( new object[] { "A001", "Widget", 42, "Note A, which runs past its six characters" }, ReadAll( reader ).Single(),
                "The last column takes everything to the end of the record, trimmed like any other left-aligned column." );
        }

        [TestMethod]
        public void TestRead_IsLongRecordRejected_HasNoEffectWhenRaggedRight()
        {
            const string text = "A001Widget       42Note A and more\r\n";
            var options = new FixedLengthOptions { IsRaggedRight = true, IsLongRecordRejected = true };
            var reader = new FixedLengthReader( new StringReader( text ), Schema(), options );

            CollectionAssert.AreEqual( new object[] { "A001", "Widget", 42, "Note A and more" }, ReadAll( reader ).Single(),
                "No ragged-right record is too long, because the surplus belongs to the last column." );
        }

        [TestMethod]
        public void TestRead_LastColumnRightAligned_TakesTheRestAndTrimsItsLeadingFill()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "Id" ), new Window( 4 ) );
            schema.AddColumn( new Int64Column( "Amount" ), new Window( 5 ) { Alignment = FixedAlignment.RightAligned } );
            var reader = new FixedLengthReader( new StringReader( "A001      1234567890\r\nB002 7\r\n" ), schema, Ragged() );

            var records = ReadAll( reader );

            CollectionAssert.AreEqual( new object[] { "A001", 1234567890L }, records[0] );
            CollectionAssert.AreEqual( new object[] { "B002", 7L }, records[1] );
        }

        [TestMethod]
        public void TestRead_TrailingWindow_IsNullWhenTheRecordEndsBeforeIt()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "Id" ), new Window( 4 ) );
            schema.AddColumn( new StringColumn( "Rest" ), Window.Trailing );
            var reader = new FixedLengthReader( new StringReader( "A001everything else\r\nB0\r\nC003\r\n" ), schema, Ragged() );

            var records = ReadAll( reader );

            CollectionAssert.AreEqual( new object[] { "A001", "everything else" }, records[0] );
            CollectionAssert.AreEqual( new object[] { "B0", null! }, records[1] );
            CollectionAssert.AreEqual( new object[] { "C003", null! }, records[2] );
        }

        [TestMethod]
        public void TestRead_SelectorOnAPrefix_ReadsRecordTypesOfDifferentLengthsEachRagged()
        {
            var header = new FixedLengthSchema();
            header.AddColumn( new StringColumn( "Type" ), new Window( 1 ) );
            header.AddColumn( new StringColumn( "Batch" ), new Window( 12 ) );
            header.AddColumn( new Int32Column( "Count" ), new Window( 4 ) { Alignment = FixedAlignment.RightAligned } );
            var detail = new FixedLengthSchema();
            detail.AddColumn( new StringColumn( "Type" ), new Window( 1 ) );
            detail.AddColumn( new StringColumn( "Account" ), new Window( 8 ) );
            detail.AddColumn( new DecimalColumn( "Amount" ), new Window( 10 ) { Alignment = FixedAlignment.RightAligned } );
            detail.AddColumn( new StringColumn( "Reference" ), new Window( 12 ) );
            var selector = new FixedLengthSchemaSelector();
            selector.When( record => record.StartsWith( 'H' ) ).Use( header );
            selector.WithDefault( detail );
            const string text = "HSeptember\r\n"
                                + "DACC00001    100.50REF-1\r\n"
                                + "DACC00002     75\r\n"
                                + "DACC00003\r\n";
            var reader = new FixedLengthReader( new StringReader( text ), selector, Ragged() );

            var records = ReadAll( reader );

            Assert.HasCount( 4, records );
            CollectionAssert.AreEqual( new object[] { "H", "September", null! }, records[0] );
            CollectionAssert.AreEqual( new object[] { "D", "ACC00001", 100.50m, "REF-1" }, records[1] );
            CollectionAssert.AreEqual( new object[] { "D", "ACC00002", 75m, null! }, records[2] );
            CollectionAssert.AreEqual( new object[] { "D", "ACC00003", null!, null! }, records[3] );
        }

        [TestMethod]
        public void TestRead_WithoutRecordSeparators_ReadsTheFinalPartialRecord()
        {
            const string text = "A001Widget       42Note AB002Gadget";
            var options = new FixedLengthOptions { IsRaggedRight = true, HasRecordSeparator = false };
            var reader = new FixedLengthReader( new StringReader( text ), Schema(), options );

            var records = ReadAll( reader );

            Assert.HasCount( 2, records );
            CollectionAssert.AreEqual( new object[] { "B002", "Gadget", null!, null! }, records[1] );
        }

        [TestMethod]
        public async Task TestReadAsync_ShortRecords_ReadWhatIsThere()
        {
            var reader = new FixedLengthReader( new StringReader( Records ), Schema(), Ragged() );
            List<object?[]> records = [];
            while (await reader.ReadAsync())
            {
                records.Add( reader.GetValues() );
            }

            Assert.HasCount( 4, records );
            CollectionAssert.AreEqual( new object[] { "D004", null!, null!, null! }, records[3] );
        }

        [TestMethod]
        public void TestTypeMapperRead_ShortRecords_LeaveTheMissingPropertiesUnset()
        {
            var mapper = Mapper();

            var items = mapper.Read( new StringReader( Records ), Ragged() ).ToList();

            Assert.HasCount( 4, items );
            Assert.AreEqual( 42, items[0].Quantity );
            Assert.AreEqual( "Note A", items[0].Note );
            Assert.AreEqual( 7, items[1].Quantity );
            Assert.IsNull( items[1].Note );
            Assert.AreEqual( 1, items[2].Quantity );
            Assert.IsNull( items[3].Name );
            Assert.IsNull( items[3].Quantity );
        }

        [TestMethod]
        public void TestWrite_LastLeftAlignedColumn_IsNotPadded()
        {
            var writer = new StringWriter();
            var recordWriter = new FixedLengthWriter( writer, Schema(), new FixedLengthOptions { IsRaggedRight = true, RecordSeparator = "\r\n" } );

            recordWriter.Write( [ "A001", "Widget", 42, "Note A" ] );
            recordWriter.Write( [ "B002", "Gadget", 7, null ] );
            recordWriter.Write( [ "C003", "Gizmo", 1, "N" ] );

            Assert.AreEqual( "A001Widget       42Note A\r\nB002Gadget        7\r\nC003Gizmo         1N\r\n", writer.ToString() );
        }

        [TestMethod]
        public void TestWrite_LastColumn_IsNeitherPaddedNorTruncated()
        {
            var writer = new StringWriter();
            var recordWriter = new FixedLengthWriter( writer, Schema(), new FixedLengthOptions { IsRaggedRight = true, RecordSeparator = "\n" } );

            recordWriter.Write( [ "A001", "Widget", 42, "A note far longer than six characters" ] );

            Assert.AreEqual( "A001Widget       42A note far longer than six characters\n", writer.ToString() );
        }

        [TestMethod]
        public void TestWrite_LastColumnRightAligned_IsWrittenAsFormatted()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "Id" ), new Window( 4 ) );
            schema.AddColumn( new Int32Column( "Qty" ), new Window( 5 ) { Alignment = FixedAlignment.RightAligned } );
            var writer = new StringWriter();
            var recordWriter = new FixedLengthWriter( writer, schema, new FixedLengthOptions { IsRaggedRight = true, RecordSeparator = "\n" } );

            recordWriter.Write( [ "A001", 42 ] );
            recordWriter.Write( [ "B002", 1234567 ] );

            Assert.AreEqual( "A00142\nB0021234567\n", writer.ToString(), "The last column runs to the end of the record, so alignment within a window does not apply." );
        }

        [TestMethod]
        public void TestWrite_ColumnsBeforeTheLast_AreStillFitted()
        {
            var writer = new StringWriter();
            var recordWriter = new FixedLengthWriter( writer, Schema(), new FixedLengthOptions { IsRaggedRight = true, RecordSeparator = "\n" } );

            recordWriter.Write( [ "A001", "A name longer than ten", 1234567, "N" ] );

            Assert.AreEqual( "A001r than ten34567N\n", writer.ToString(), "Every column but the last is padded or truncated to its window as before." );
        }

        [TestMethod]
        public void TestWrite_Header_LeavesTheLastColumnNameUnpadded()
        {
            var writer = new StringWriter();
            var options = new FixedLengthOptions { IsRaggedRight = true, IsFirstRecordHeader = true, RecordSeparator = "\n" };
            var recordWriter = new FixedLengthWriter( writer, Schema(), options );

            recordWriter.Write( [ "A001", "Widget", 42, "Note A" ] );

            Assert.AreEqual( "Id  Name        QtyNote\nA001Widget       42Note A\n", writer.ToString() );
        }

        [TestMethod]
        public void TestWrite_WithoutTheOption_PadsTheLastColumnAsBefore()
        {
            var writer = new StringWriter();
            var recordWriter = new FixedLengthWriter( writer, Schema(), new FixedLengthOptions { RecordSeparator = "\n" } );

            recordWriter.Write( [ "B002", "Gadget", 7, null ] );

            Assert.AreEqual( "B002Gadget        7      \n", writer.ToString() );
        }

        [TestMethod]
        public void TestRoundTrip_RaggedInRaggedOut_KeepsTheShape()
        {
            // Only the last column varies here, which is the shape the writer produces.
            const string text = "A001Widget       42Note A\r\nB002Gadget        7\r\nC003Gizmo         1A note far longer than six characters\r\n";
            var mapper = Mapper();
            var options = new FixedLengthOptions { IsRaggedRight = true, RecordSeparator = "\r\n" };
            var items = mapper.Read( new StringReader( text ), options ).ToList();
            var writer = new StringWriter();

            mapper.Write( writer, items, options );

            Assert.AreEqual( text, writer.ToString() );
        }

        [TestMethod]
        public void TestClone_CarriesTheOption()
        {
            Assert.IsTrue( Ragged().Clone().IsRaggedRight );
        }

        private static IFixedLengthTypeMapper<Item> Mapper()
        {
            var mapper = FixedLengthTypeMapper.Define<Item>();
            mapper.Property( item => item.Id, new Window( 4 ) );
            mapper.Property( item => item.Name, new Window( 10 ) );
            mapper.Property( item => item.Quantity, new Window( 5 ) { Alignment = FixedAlignment.RightAligned } );
            mapper.Property( item => item.Note, new Window( 6 ) );
            return mapper;
        }

        public sealed class Item
        {
            public string Id { get; set; } = string.Empty;

            public string Name { get; set; } = string.Empty;

            public int? Quantity { get; set; }

            public string Note { get; set; } = string.Empty;
        }
    }
}
