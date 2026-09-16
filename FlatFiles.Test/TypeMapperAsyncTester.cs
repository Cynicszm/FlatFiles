using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FlatFiles.TypeMapping;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the asynchronous read and write paths of both type mappers, and the dynamic mapper
    ///     factories that take an entity factory.
    /// </summary>
    [TestClass]
    public class TypeMapperAsyncTester
    {
        public sealed class Person
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private static async IAsyncEnumerable<Person> People()
        {
            await Task.Yield();
            yield return new Person { Id = 1, Name = "Bob" };
            yield return new Person { Id = 2, Name = "Jane" };
        }

        private static async Task<List<Person>> Collect( IAsyncEnumerable<Person> people )
        {
            List<Person> results = [];
            await foreach (var person in people)
            {
                results.Add( person );
            }
            return results;
        }

        [TestMethod]
        public async Task TestDelimitedMapper_WriteAsyncThenReadAsync_RoundTrips()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Person() );
            mapper.Property( x => x.Id ).ColumnName( "id" );
            mapper.Property( x => x.Name ).ColumnName( "name" );
            var writer = new StringWriter();

            await mapper.WriteAsync( writer, People() );
            var people = await Collect( mapper.ReadAsync( new StringReader( writer.ToString() ) ) );

            Assert.AreEqual( 2, people.Count, "Both people should have come back." );
            Assert.AreEqual( "Bob", people[0].Name );
            Assert.AreEqual( 2, people[1].Id );
        }

        [TestMethod]
        public async Task TestFixedLengthMapper_WriteAsyncThenReadAsync_RoundTrips()
        {
            var mapper = FixedLengthTypeMapper.Define( () => new Person() );
            mapper.Property( x => x.Id, new Window( 5 ) ).ColumnName( "id" );
            mapper.Property( x => x.Name, new Window( 10 ) ).ColumnName( "name" );
            var writer = new StringWriter();

            await mapper.WriteAsync( writer, People() );
            var people = await Collect( mapper.ReadAsync( new StringReader( writer.ToString() ) ) );

            Assert.AreEqual( 2, people.Count, "Both people should have come back." );
            Assert.AreEqual( "Bob", people[0].Name );
            Assert.AreEqual( 2, people[1].Id );
        }

        [TestMethod]
        public void TestDefineDynamic_WithFactory_CreatesAMapperForTheRuntimeType()
        {
            var delimited = DelimitedTypeMapper.DefineDynamic( typeof( Person ), () => new Person() );
            var fixedLength = FixedLengthTypeMapper.DefineDynamic( typeof( Person ), () => new Person() );

            Assert.IsNotNull( delimited, "The delimited dynamic mapper was not created." );
            Assert.IsNotNull( fixedLength, "The fixed-length dynamic mapper was not created." );
        }
    }
}
