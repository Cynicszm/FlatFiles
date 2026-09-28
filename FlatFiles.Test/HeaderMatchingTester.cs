using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests reading a file whose columns are matched to its header by name rather than by position: the file
    ///     that has been reordered since the schema was written, and what each kind of disagreement between the two
    ///     is treated as.
    /// </summary>
    [TestClass]
    public class HeaderMatchingTester
    {
        private const string Reordered = "Town,Id,Name\r\nLeeds,1,Bob\r\nYork,2,Sue\r\n";

        [TestMethod]
        public void TestByPosition_ReadsAReorderedFileIntoTheWrongColumns()
        {
            // What happens today, and the reason the option exists. The header is read and thrown away, so the
            // town lands in Name and nothing says so.
            var reader = new DelimitedReader( new StringReader( Reordered ), Schema(), Options( HeaderMatching.ByPosition ) );

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( "Leeds", reader.GetValues()[0] );
        }

        [TestMethod]
        public void TestByName_ReadsAReorderedFileIntoTheRightColumns()
        {
            var reader = new DelimitedReader( new StringReader( Reordered ), Schema(), Options( HeaderMatching.ByName ) );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();
            Assert.AreEqual( "Bob", values[0], "Name is third in the file and first in the schema." );
            Assert.AreEqual( 1, values[1] );
            Assert.AreEqual( "Leeds", values[2] );
        }

        [TestMethod]
        public void TestByName_KeepsTheValuesInSchemaOrder()
        {
            // The source is permuted, not the destination, so anything indexing by GetOrdinal still works.
            var schema = Schema();
            var reader = new DelimitedReader( new StringReader( Reordered ), schema, Options( HeaderMatching.ByName ) );

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( "Leeds", reader.GetValues()[schema.GetOrdinal( "Town" )] );
        }

        [TestMethod]
        public async Task TestByName_ReadsAReorderedFileAsynchronously()
        {
            var reader = new DelimitedReader( new StringReader( Reordered ), Schema(), Options( HeaderMatching.ByName ) );

            Assert.IsTrue( await reader.ReadAsync() );
            Assert.AreEqual( "Bob", reader.GetValues()[0] );
        }

        [TestMethod]
        public void TestByName_ReadsEveryRecordOfTheFile()
        {
            var reader = new DelimitedReader( new StringReader( Reordered ), Schema(), Options( HeaderMatching.ByName ) );

            var names = new List<object?>();
            while (reader.Read())
            {
                names.Add( reader.GetValues()[0] );
            }

            CollectionAssert.AreEqual( new object[] { "Bob", "Sue" }, names );
        }

        [TestMethod]
        public void TestByName_AColumnTheHeaderDoesNotCarryIsRefused()
        {
            const string text = "Id,Name\r\n1,Bob\r\n";
            var reader = new DelimitedReader( new StringReader( text ), Schema(), Options( HeaderMatching.ByName ) );

            var exception = Assert.ThrowsExactly<FlatFileException>( () => reader.Read() );

            StringAssert.Contains( exception.Message, "Town" );
        }

        [TestMethod]
        public void TestByNameWhereFound_AColumnTheHeaderDoesNotCarryIsReadAsEmpty()
        {
            const string text = "Id,Name\r\n1,Bob\r\n";
            var reader = new DelimitedReader( new StringReader( text ), Schema(), Options( HeaderMatching.ByNameWhereFound ) );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();
            Assert.AreEqual( "Bob", values[0] );
            Assert.IsNull( values[2], "An empty value is what the column's null handling turns into null." );
        }

        [TestMethod]
        public void TestByName_AHeaderColumnTheSchemaDoesNotDeclareIsNotRead()
        {
            const string text = "Name,Note,Id,Town\r\nBob,ignore me,1,Leeds\r\n";
            var reader = new DelimitedReader( new StringReader( text ), Schema(), Options( HeaderMatching.ByName ) );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();
            Assert.AreEqual( 3, values.Length );
            Assert.AreEqual( "Leeds", values[2] );
        }

        [TestMethod]
        public void TestByName_ANameTheHeaderCarriesTwiceIsRefusedOnlyWhenAColumnAsksForIt()
        {
            const string wanted = "Name,Name,Id,Town\r\nBob,Bobby,1,Leeds\r\n";
            var reader = new DelimitedReader( new StringReader( wanted ), Schema(), Options( HeaderMatching.ByName ) );

            var exception = Assert.ThrowsExactly<FlatFileException>( () => reader.Read() );
            StringAssert.Contains( exception.Message, "Name" );

            const string spare = "Name,Note,Note,Id,Town\r\nBob,a,b,1,Leeds\r\n";
            var tolerant = new DelimitedReader( new StringReader( spare ), Schema(), Options( HeaderMatching.ByName ) );

            Assert.IsTrue( tolerant.Read(), "A repeated name the schema never asks for is nothing to do with it." );
            Assert.AreEqual( "Leeds", tolerant.GetValues()[2] );
        }

        [TestMethod]
        public void TestByName_TheComparisonIgnoresCaseByDefault()
        {
            const string text = "TOWN,ID,NAME\r\nLeeds,1,Bob\r\n";
            var reader = new DelimitedReader( new StringReader( text ), Schema(), Options( HeaderMatching.ByName ) );

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( "Bob", reader.GetValues()[0] );
        }

        [TestMethod]
        public void TestByName_TheComparerCanBeChosen()
        {
            const string text = "TOWN,ID,NAME\r\nLeeds,1,Bob\r\n";
            var options = Options( HeaderMatching.ByName );
            options.HeaderComparer = StringComparer.Ordinal;
            var reader = new DelimitedReader( new StringReader( text ), Schema(), options );

            Assert.ThrowsExactly<FlatFileException>( () => reader.Read() );
        }

        [TestMethod]
        public void TestByName_AnIgnoredColumnWithNoNameIsRefused()
        {
            // An ignored column exists to hold a position, and under name matching positions stop mattering.
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "Name" ) );
            schema.AddColumn( new IgnoredColumn() );
            var reader = new DelimitedReader( new StringReader( "Name,Spare\r\nBob,x\r\n" ), schema, Options( HeaderMatching.ByName ) );

            Assert.ThrowsExactly<FlatFileException>( () => reader.Read() );
        }

        [TestMethod]
        public void TestByName_AnIgnoredColumnWithANameIsMatchedAndStillYieldsNothing()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "Name" ) );
            schema.AddColumn( new IgnoredColumn( "Spare" ) );
            var reader = new DelimitedReader( new StringReader( "Spare,Name\r\nx,Bob\r\n" ), schema, Options( HeaderMatching.ByName ) );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();
            Assert.AreEqual( 1, values.Length );
            Assert.AreEqual( "Bob", values[0] );
        }

        [TestMethod]
        public void TestByName_AMetadataColumnNeedsNoHeading()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "Name" ) );
            schema.AddColumn( new RecordNumberColumn( "Row" ) );
            schema.AddColumn( new StringColumn( "Town" ) );
            var reader = new DelimitedReader( new StringReader( "Town,Name\r\nLeeds,Bob\r\n" ), schema, Options( HeaderMatching.ByName ) );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();
            Assert.AreEqual( "Bob", values[0] );
            Assert.AreEqual( 1, values[1] );
            Assert.AreEqual( "Leeds", values[2] );
        }

        [TestMethod]
        public void TestByName_ATypeMapperReadsAReorderedFileOntoItsEntity()
        {
            // The straight-onto-the-entity path has its own parsing loop, so it needs its own test.
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Id );
            mapper.Property( x => x.Town );

            var people = mapper.Read( new StringReader( Reordered ), Options( HeaderMatching.ByName ) ).ToList();

            Assert.AreEqual( 2, people.Count );
            Assert.AreEqual( "Bob", people[0].Name );
            Assert.AreEqual( 1, people[0].Id );
            Assert.AreEqual( "Leeds", people[0].Town );
        }

        [TestMethod]
        public void TestByName_AHandlerIsGivenTheValuesAsTheRecordCarriedThem()
        {
            var reader = new DelimitedReader( new StringReader( Reordered ), Schema(), Options( HeaderMatching.ByName ) );
            string[]? seen = null;
            reader.RecordRead += ( _, e ) => seen = e.Values;

            Assert.IsTrue( reader.Read() );
            Assert.IsNotNull( seen );
            CollectionAssert.AreEqual( new[] { "Leeds", "1", "Bob" }, seen, "A handler sees the record, not the schema's order." );
            Assert.AreEqual( "Bob", reader.GetValues()[0], "And what it leaves is still matched by name." );
        }

        [TestMethod]
        public void TestByName_ARecordShorterThanTheHeaderIsJudgedAgainstTheHeader()
        {
            const string text = "Name,Id,Town,Note\r\nBob,1,Leeds\r\n";
            var reader = new DelimitedReader( new StringReader( text ), Schema(), Options( HeaderMatching.ByName ) );

            Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );
        }

        [TestMethod]
        public void TestByName_AShortRecordCanStillBePadded()
        {
            const string text = "Name,Id,Town,Note\r\nBob,1,Leeds\r\n";
            var options = Options( HeaderMatching.ByName );
            options.ShortRecordHandling = ShortRecordHandling.Pad;
            var reader = new DelimitedReader( new StringReader( text ), Schema(), options );

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( "Leeds", reader.GetValues()[2] );
        }

        [TestMethod]
        public void TestByName_ExtraColumnsInTheFileAreNotALongRecord()
        {
            // The header, not the schema, says how wide a record should be, so a file carrying columns the schema
            // does not declare is not a file of long records.
            const string text = "Name,Note,Id,Town\r\nBob,spare,1,Leeds\r\n";
            var options = Options( HeaderMatching.ByName );
            options.LongRecordHandling = LongRecordHandling.Refuse;
            var reader = new DelimitedReader( new StringReader( text ), Schema(), options );

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( "Leeds", reader.GetValues()[2] );
        }

        [TestMethod]
        public void TestMatching_NeedsAHeaderToMatch()
        {
            var options = new DelimitedOptions { RecordSeparator = "\r\n", HeaderMatching = HeaderMatching.ByName };
            var reader = new DelimitedReader( new StringReader( "Bob,1,Leeds\r\n" ), Schema(), options );

            Assert.ThrowsExactly<InvalidOperationException>( () => reader.Read() );
        }

        [TestMethod]
        public void TestMatching_NeedsASchemaRatherThanASelector()
        {
            var selector = new DelimitedSchemaSelector();
            selector.When( _ => true ).Use( Schema() );
            var reader = new DelimitedReader( new StringReader( Reordered ), selector, Options( HeaderMatching.ByName ) );

            Assert.ThrowsExactly<InvalidOperationException>( () => reader.Read() );
        }

        [TestMethod]
        public void TestMatching_AnEmptyFileIsNotAnError()
        {
            var reader = new DelimitedReader( new StringReader( string.Empty ), Schema(), Options( HeaderMatching.ByName ) );

            Assert.IsFalse( reader.Read() );
        }

        [TestMethod]
        public void TestOptions_TheSettingsSurviveACloneAndAreValidated()
        {
            var options = Options( HeaderMatching.ByNameWhereFound );
            options.HeaderComparer = StringComparer.Ordinal;
            var copy = options.Clone();

            Assert.AreEqual( HeaderMatching.ByNameWhereFound, copy.HeaderMatching );
            Assert.AreSame( StringComparer.Ordinal, copy.HeaderComparer );
            Assert.AreEqual( HeaderMatching.ByPosition, new DelimitedOptions().HeaderMatching );

            Assert.ThrowsExactly<ArgumentException>( () => new DelimitedOptions { HeaderMatching = (HeaderMatching) 7 } );
            Assert.ThrowsExactly<ArgumentNullException>( () => new DelimitedOptions { HeaderComparer = null! } );
        }

        private static DelimitedOptions Options( HeaderMatching matching )
        {
            return new DelimitedOptions
            {
                RecordSeparator = "\r\n",
                IsFirstRecordSchema = true,
                HeaderMatching = matching
            };
        }

        private static DelimitedSchema Schema()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "Name" ) );
            schema.AddColumn( new Int32Column( "Id" ) );
            schema.AddColumn( new StringColumn( "Town" ) );
            return schema;
        }

        public class Person
        {
            public string Name { get; set; } = string.Empty;

            public int Id { get; set; }

            public string Town { get; set; } = string.Empty;
        }
    }
}
