using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the array a reader hands out. A caller of GetValues gets a copy, because it may keep the array or
    ///     write to it; a type mapper is given the reader's own, because it reads each value once and lets it go.
    ///     The copy is what makes the first safe, so these say plainly that it is still there.
    /// </summary>
    [TestClass]
    public class ReaderValueOwnershipTester
    {
        private const string Data = "1,one\r\n2,two\r\n3,three\r\n";

        private static DelimitedSchema Schema()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new Int32Column( "id" ) );
            schema.AddColumn( new StringColumn( "name" ) );
            return schema;
        }

        [TestMethod]
        public void TestGetValues_CalledTwice_HandsOutSeparateArrays()
        {
            var reader = new DelimitedReader( new StringReader( Data ), Schema() );

            Assert.IsTrue( reader.Read() );
            var first = reader.GetValues();
            var second = reader.GetValues();

            Assert.AreNotSame( first, second, "Each caller gets an array of its own." );
            CollectionAssert.AreEqual( first, second );
        }

        [TestMethod]
        public void TestGetValues_WrittenTo_DoesNotDisturbTheReader()
        {
            var reader = new DelimitedReader( new StringReader( Data ), Schema() );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();
            values[0] = 99;
            values[1] = "wrecked";

            CollectionAssert.AreEqual( new object[] { 1, "one" }, reader.GetValues(),
                "Writing to the array a caller was given must not reach the reader's own." );
            Assert.IsTrue( reader.Read() );
            CollectionAssert.AreEqual( new object[] { 2, "two" }, reader.GetValues() );
        }

        [TestMethod]
        public void TestTypedRead_ReadsEveryRecord_WithoutTheCopy()
        {
            var mapper = DelimitedTypeMapper.Define<Row>();
            mapper.Property( x => x.Id );
            mapper.Property( x => x.Name );

            Row[] rows = [.. mapper.Read( new StringReader( Data ) )];

            Assert.HasCount( 3, rows );
            Assert.AreEqual( 1, rows[0].Id );
            Assert.AreEqual( "one", rows[0].Name );
            Assert.AreEqual( 3, rows[2].Id );
            Assert.AreEqual( "three", rows[2].Name );
        }

        [TestMethod]
        public void TestTypedRead_EntitiesOutliveTheirRecords()
        {
            var mapper = DelimitedTypeMapper.Define<Row>();
            mapper.Property( x => x.Id );
            mapper.Property( x => x.Name );

            // Materialising afterwards rather than during: the entities must not share anything with the array
            // the reader reused underneath them.
            var rows = mapper.Read( new StringReader( Data ) ).ToArray();

            CollectionAssert.AreEqual( new[] { 1, 2, 3 }, rows.Select( r => r.Id ).ToArray() );
            CollectionAssert.AreEqual( new[] { "one", "two", "three" }, rows.Select( r => r.Name ).ToArray() );
        }

        [TestMethod]
        public void TestTypedRead_FixedLength_ReadsEveryRecord()
        {
            var mapper = FixedLengthTypeMapper.Define<Row>();
            mapper.Property( x => x.Id, 2 );
            mapper.Property( x => x.Name, 6 );

            Row[] rows = [.. mapper.Read( new StringReader( " 1one   \r\n 2two   \r\n" ) )];

            Assert.HasCount( 2, rows );
            Assert.AreEqual( 1, rows[0].Id );
            Assert.AreEqual( "one", rows[0].Name );
            Assert.AreEqual( 2, rows[1].Id );
            Assert.AreEqual( "two", rows[1].Name );
        }

        [TestMethod]
        public void TestRecordParsed_HandlerKeepsTheValues_TheyDoNotChangeUnderIt()
        {
            List<object[]> kept = [];
            var reader = new DelimitedReader( new StringReader( Data ), Schema() );
            reader.RecordParsed += ( _, e ) => kept.Add( e.Values );

            while (reader.Read())
            {
            }

            // The reader fills one array for every record it reads. A handler that is given the values may keep
            // them, so it is handed an array of its own; if it were not, all three of these would read alike.
            Assert.HasCount( 3, kept );
            CollectionAssert.AreEqual( new object[] { 1, "one" }, kept[0] );
            CollectionAssert.AreEqual( new object[] { 2, "two" }, kept[1] );
            CollectionAssert.AreEqual( new object[] { 3, "three" }, kept[2] );
            Assert.AreNotSame( kept[0], kept[1] );
        }

        [TestMethod]
        public void TestRecordParsed_FixedLengthHandlerKeepsTheValues_TheyDoNotChangeUnderIt()
        {
            List<object[]> kept = [];
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "id" ), new Window( 2 ) );
            schema.AddColumn( new StringColumn( "name" ), new Window( 6 ) );
            var reader = new FixedLengthReader( new StringReader( " 1one   \r\n 2two   \r\n" ), schema );
            reader.RecordParsed += ( _, e ) => kept.Add( e.Values );

            while (reader.Read())
            {
            }

            Assert.HasCount( 2, kept );
            CollectionAssert.AreEqual( new object[] { 1, "one" }, kept[0] );
            CollectionAssert.AreEqual( new object[] { 2, "two" }, kept[1] );
            Assert.AreNotSame( kept[0], kept[1] );
        }

        [TestMethod]
        public void TestRead_SchemaSelectorChangesTheColumnCount_EachRecordStillReadsRight()
        {
            var two = new DelimitedSchema();
            two.AddColumn( new StringColumn( "type" ) );
            two.AddColumn( new Int32Column( "count" ) );
            var three = new DelimitedSchema();
            three.AddColumn( new StringColumn( "type" ) );
            three.AddColumn( new StringColumn( "name" ) );
            three.AddColumn( new Int32Column( "count" ) );
            var selector = new DelimitedSchemaSelector();
            selector.When( v => v[0] == "A" ).Use( two );
            selector.When( v => v[0] == "B" ).Use( three );
            var reader = new DelimitedReader( new StringReader( "A,1\r\nB,bee,2\r\nA,3\r\n" ), selector );

            // The array the reader fills has to grow and shrink with the schema the selector picks.
            Assert.IsTrue( reader.Read() );
            CollectionAssert.AreEqual( new object[] { "A", 1 }, reader.GetValues() );
            Assert.IsTrue( reader.Read() );
            CollectionAssert.AreEqual( new object[] { "B", "bee", 2 }, reader.GetValues() );
            Assert.IsTrue( reader.Read() );
            CollectionAssert.AreEqual( new object[] { "A", 3 }, reader.GetValues() );
        }

        public class Row
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }
    }
}
