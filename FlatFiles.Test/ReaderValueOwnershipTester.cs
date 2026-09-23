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

        public class Row
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }
    }
}
