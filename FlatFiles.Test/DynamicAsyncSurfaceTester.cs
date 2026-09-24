using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the asynchronous surface of the dynamic mappers - the one reached when the entity type is not
    ///     known until run time. It is the same work as the typed surface underneath, but it is reached through
    ///     its own set of explicit interface implementations, and nothing had been through them.
    /// </summary>
    [TestClass]
    public class DynamicAsyncSurfaceTester
    {
        private const string Text = "Alice,3\r\nBob,4\r\n";

        private static IDynamicDelimitedTypeMapper Delimited()
        {
            var mapper = DelimitedTypeMapper.DefineDynamic( typeof( Person ) );
            mapper.StringProperty( "Name" );
            mapper.Int32Property( "Count" );
            return mapper;
        }

        private static IDynamicFixedLengthTypeMapper FixedLength()
        {
            var mapper = FixedLengthTypeMapper.DefineDynamic( typeof( Person ) );
            mapper.StringProperty( "Name", new Window( 10 ) );
            mapper.Int32Property( "Count", new Window( 4 ) );
            return mapper;
        }

        [TestMethod]
        public async Task TestDelimited_ReadAsync_ReadsEveryRecord()
        {
            List<object> read = [];
            await foreach (var entity in Delimited().ReadAsync( new StringReader( Text ) ))
            {
                read.Add( entity );
            }

            Assert.AreEqual( 2, read.Count );
            Assert.AreEqual( "Bob", ( (Person) read[1] ).Name );
        }

        [TestMethod]
        public async Task TestDelimited_WriteAsync_WritesEveryEntity()
        {
            var writer = new StringWriter();

            await Delimited().WriteAsync( writer, [new Person { Name = "Alice", Count = 3 }] );

            Assert.AreEqual( "Alice,3\r\n", writer.ToString() );
        }

        [TestMethod]
        public async Task TestDelimited_WriteAsync_TakesAnAsynchronousSequence()
        {
            var writer = new StringWriter();

            await Delimited().WriteAsync( writer, Sequence() );

            Assert.AreEqual( "Alice,3\r\nBob,4\r\n", writer.ToString() );
        }

        [TestMethod]
        public async Task TestFixedLength_ReadAsync_ReadsEveryRecord()
        {
            List<object> read = [];
            await foreach (var entity in FixedLength().ReadAsync( new StringReader( "Alice     3   \r\nBob       4   \r\n" ) ))
            {
                read.Add( entity );
            }

            Assert.AreEqual( 2, read.Count );
            Assert.AreEqual( 4, ( (Person) read[1] ).Count );
        }

        [TestMethod]
        public async Task TestFixedLength_WriteAsync_WritesEveryEntity()
        {
            var writer = new StringWriter();

            await FixedLength().WriteAsync( writer, [new Person { Name = "Alice", Count = 3 }] );

            Assert.AreEqual( "Alice     3   \r\n", writer.ToString() );
        }

        [TestMethod]
        public async Task TestFixedLength_WriteAsync_TakesAnAsynchronousSequence()
        {
            var writer = new StringWriter();

            await FixedLength().WriteAsync( writer, Sequence() );

            Assert.AreEqual( "Alice     3   \r\nBob       4   \r\n", writer.ToString() );
        }

        private static async IAsyncEnumerable<object> Sequence()
        {
            yield return new Person { Name = "Alice", Count = 3 };
            await Task.Yield();
            yield return new Person { Name = "Bob", Count = 4 };
        }

        internal class Person
        {
            public string Name { get; set; } = string.Empty;

            public int Count { get; set; }
        }
    }
}
