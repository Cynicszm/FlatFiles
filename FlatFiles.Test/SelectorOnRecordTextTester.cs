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
    ///     Tests choosing a schema or a mapping by the record's text rather than by its values: that it picks the
    ///     same layouts, that a selector may mix the two, and that everything a reader does around it - handlers,
    ///     errors, skipping - still holds when the records go straight onto their entities.
    /// </summary>
    [TestClass]
    public class SelectorOnRecordTextTester
    {
        private const string Records = "P,Bob,42\r\nO,1001,9.99\r\nP,Sue,37\r\n";

        [TestMethod]
        public void TestSchemaSelector_ChoosesByTheRecordText()
        {
            var selector = new DelimitedSchemaSelector();
            selector.WhenText( record => record.StartsWith( "P," ) ).Use( PersonSchema() );
            selector.WhenText( record => record.StartsWith( "O," ) ).Use( OrderSchema() );
            var reader = new DelimitedReader( new StringReader( Records ), selector, Options() );

            var read = new List<object?>();
            while (reader.Read())
            {
                read.Add( reader.GetValues()[1] );
            }

            CollectionAssert.AreEqual( new object[] { "Bob", 1001, "Sue" }, read );
        }

        [TestMethod]
        public void TestSchemaSelector_TheTwoKindsOfPredicateCanBeMixed()
        {
            var selector = new DelimitedSchemaSelector();
            selector.WhenText( record => record.StartsWith( "P," ) ).Use( PersonSchema() );
            selector.When( values => values[0] == "O" ).Use( OrderSchema() );
            var reader = new DelimitedReader( new StringReader( Records ), selector, Options() );

            var read = new List<object?>();
            while (reader.Read())
            {
                read.Add( reader.GetValues()[1] );
            }

            CollectionAssert.AreEqual( new object[] { "Bob", 1001, "Sue" }, read );
        }

        [TestMethod]
        public void TestSchemaSelector_ADefaultStillTakesWhatNothingElseDoes()
        {
            var selector = new DelimitedSchemaSelector();
            selector.WhenText( record => record.StartsWith( "P," ) ).Use( PersonSchema() );
            selector.WithDefault( OrderSchema() );
            var reader = new DelimitedReader( new StringReader( Records ), selector, Options() );

            var read = new List<object?>();
            while (reader.Read())
            {
                read.Add( reader.GetValues()[1] );
            }

            CollectionAssert.AreEqual( new object[] { "Bob", 1001, "Sue" }, read );
        }

        [TestMethod]
        public void TestSchemaSelector_ARecordNothingMatchesIsStillReported()
        {
            var selector = new DelimitedSchemaSelector();
            selector.WhenText( record => record.StartsWith( "P," ) ).Use( PersonSchema() );
            var reader = new DelimitedReader( new StringReader( Records ), selector, Options() );

            Assert.IsTrue( reader.Read() );
            Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );
        }

        [TestMethod]
        public void TestSchemaSelector_OnMatchStillFires()
        {
            var matched = 0;
            var selector = new DelimitedSchemaSelector();
            selector.WhenText( record => record.StartsWith( "P," ) ).Use( PersonSchema() ).OnMatch( () => ++matched );
            selector.WithDefault( OrderSchema() );
            var reader = new DelimitedReader( new StringReader( Records ), selector, Options() );

            while (reader.Read())
            {
            }

            Assert.AreEqual( 2, matched );
        }

        [TestMethod]
        public void TestSchemaSelector_ANullPredicateIsRefused()
        {
            var selector = new DelimitedSchemaSelector();

            Assert.ThrowsExactly<ArgumentNullException>( () => selector.WhenText( null! ) );
        }

        [TestMethod]
        public void TestMapperSelector_ReadsEachLayoutOntoItsEntity()
        {
            var reader = TextSelector().GetReader( new StringReader( Records ), Options() );

            var read = new List<object>();
            while (reader.Read())
            {
                read.Add( reader.Current );
            }

            Assert.AreEqual( 3, read.Count );
            Assert.AreEqual( "Bob", ( (Person) read[0] ).Name );
            Assert.AreEqual( 42, ( (Person) read[0] ).Age );
            Assert.AreEqual( 1001, ( (Order) read[1] ).Id );
            Assert.AreEqual( 9.99m, ( (Order) read[1] ).Total );
            Assert.AreEqual( "Sue", ( (Person) read[2] ).Name );
        }

        [TestMethod]
        public async Task TestMapperSelector_ReadsEachLayoutAsynchronously()
        {
            var reader = TextSelector().GetReader( new StringReader( Records ), Options() );

            var read = new List<object>();
            while (await reader.ReadAsync())
            {
                read.Add( reader.Current );
            }

            Assert.AreEqual( 3, read.Count );
            Assert.AreEqual( "Bob", ( (Person) read[0] ).Name );
            Assert.AreEqual( 1001, ( (Order) read[1] ).Id );
        }

        [TestMethod]
        public void TestMapperSelector_TheTwoKindsOfPredicateCanBeMixed()
        {
            var selector = new DelimitedTypeMapperSelector();
            selector.WhenText( record => record.StartsWith( "P," ) ).Use( PersonMapper() );
            selector.When( values => values[0] == "O" ).Use( OrderMapper() );
            var reader = selector.GetReader( new StringReader( Records ), Options() );

            var read = new List<object>();
            while (reader.Read())
            {
                read.Add( reader.Current );
            }

            Assert.AreEqual( 3, read.Count );
            Assert.AreEqual( "Bob", ( (Person) read[0] ).Name );
            Assert.AreEqual( 1001, ( (Order) read[1] ).Id );
        }

        [TestMethod]
        public void TestMapperSelector_ADefaultMapperStillTakesWhatNothingElseDoes()
        {
            var selector = new DelimitedTypeMapperSelector();
            selector.WhenText( record => record.StartsWith( "P," ) ).Use( PersonMapper() );
            selector.WithDefault( OrderMapper() );
            var reader = selector.GetReader( new StringReader( Records ), Options() );

            var read = new List<object>();
            while (reader.Read())
            {
                read.Add( reader.Current );
            }

            Assert.AreEqual( 3, read.Count );
            Assert.AreEqual( 1001, ( (Order) read[1] ).Id );
        }

        [TestMethod]
        public void TestMapperSelector_ARecordNothingMatchesIsStillReported()
        {
            var selector = new DelimitedTypeMapperSelector();
            selector.WhenText( record => record.StartsWith( "P," ) ).Use( PersonMapper() );
            var reader = selector.GetReader( new StringReader( Records ), Options() );

            Assert.IsTrue( reader.Read() );
            Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );
        }

        [TestMethod]
        public void TestMapperSelector_AHandlerStillSeesTheValues()
        {
            // A record handler is given the values as an array it may write into, so attaching one puts the reader
            // back on the path that copies them out, text predicates or not.
            var reader = TextSelector().GetReader( new StringReader( Records ), Options() );
            var seen = new List<string>();
            reader.RecordRead += ( _, e ) => seen.Add( e.Values[1] );

            while (reader.Read())
            {
            }

            CollectionAssert.AreEqual( new[] { "Bob", "1001", "Sue" }, seen );
        }

        [TestMethod]
        public void TestMapperSelector_AParsedHandlerStillSeesTheRecord()
        {
            var reader = TextSelector().GetReader( new StringReader( Records ), Options() );
            var seen = 0;
            reader.RecordParsed += ( _, _ ) => ++seen;

            while (reader.Read())
            {
            }

            Assert.AreEqual( 3, seen );
        }

        [TestMethod]
        public void TestMapperSelector_ABadValueIsStillAColumnError()
        {
            const string broken = "P,Bob,not a number\r\nP,Sue,37\r\n";
            var reader = TextSelector().GetReader( new StringReader( broken ), Options() );
            var errors = 0;
            reader.ColumnError += ( _, e ) =>
            {
                ++errors;
                e.IsHandled = true;
                e.Substitution = 0;
            };

            var read = new List<object>();
            while (reader.Read())
            {
                read.Add( reader.Current );
            }

            Assert.AreEqual( 1, errors );
            Assert.AreEqual( 0, ( (Person) read[0] ).Age );
            Assert.AreEqual( 37, ( (Person) read[1] ).Age );
        }

        [TestMethod]
        public void TestMapperSelector_SkippingStillWorks()
        {
            var reader = TextSelector().GetReader( new StringReader( Records ), Options() );

            Assert.IsTrue( reader.Skip() );
            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( 1001, ( (Order) reader.Current ).Id );
        }

        private static DelimitedTypeMapperSelector TextSelector()
        {
            var selector = new DelimitedTypeMapperSelector();
            selector.WhenText( record => record.StartsWith( "P," ) ).Use( PersonMapper() );
            selector.WhenText( record => record.StartsWith( "O," ) ).Use( OrderMapper() );
            return selector;
        }

        private static IDelimitedTypeMapper<Person> PersonMapper()
        {
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Ignored();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Age );
            return mapper;
        }

        private static IDelimitedTypeMapper<Order> OrderMapper()
        {
            var mapper = DelimitedTypeMapper.Define<Order>();
            mapper.Ignored();
            mapper.Property( x => x.Id );
            mapper.Property( x => x.Total );
            return mapper;
        }

        private static DelimitedOptions Options()
        {
            return new DelimitedOptions { RecordSeparator = "\r\n" };
        }

        private static DelimitedSchema PersonSchema()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "Kind" ) );
            schema.AddColumn( new StringColumn( "Name" ) );
            schema.AddColumn( new Int32Column( "Age" ) );
            return schema;
        }

        private static DelimitedSchema OrderSchema()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "Kind" ) );
            schema.AddColumn( new Int32Column( "Id" ) );
            schema.AddColumn( new DecimalColumn( "Total" ) );
            return schema;
        }

        public class Person
        {
            public string Name { get; set; } = string.Empty;

            public int Age { get; set; }
        }

        public class Order
        {
            public int Id { get; set; }

            public decimal Total { get; set; }
        }
    }
}
