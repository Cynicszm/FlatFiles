using System.IO;
using System.Linq;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests mapping an entity that is a value type. Writing one reads its members and works; reading onto one
    ///     can only work where its constructor takes every mapped value, because a deserialiser is handed a copy.
    /// </summary>
    [TestClass]
    public class ValueTypeEntityTester
    {
        [TestMethod]
        public void TestWrite_AStructWithProperties()
        {
            // The emitted writer used to call a property on a value sitting on the stack, which is not valid IL:
            // the runtime failed to compile the method and took the process down with it.
            var mapper = DelimitedTypeMapper.Define( () => new Boxed() );
            mapper.Property( x => x.Id );
            mapper.Property( x => x.Name );

            var writer = new StringWriter();
            mapper.Write( writer, [new Boxed { Id = 1, Name = "Bob" }, new Boxed { Id = 2, Name = "Ann" }],
                new DelimitedOptions { RecordSeparator = "\r\n" } );

            Assert.AreEqual( "1,Bob\r\n2,Ann\r\n", writer.ToString() );
        }

        [TestMethod]
        public void TestWrite_AStructWithFields()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Fielded() );
            mapper.Property( x => x.Id );
            mapper.Property( x => x.Name );

            var writer = new StringWriter();
            mapper.Write( writer, [new Fielded { Id = 7, Name = "Sue" }], new DelimitedOptions { RecordSeparator = "\r\n" } );

            Assert.AreEqual( "7,Sue\r\n", writer.ToString() );
        }

        [TestMethod]
        public void TestWrite_AReadOnlyRecordStruct()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Point( 0, 0 ) );
            mapper.Property( x => x.X );
            mapper.Property( x => x.Y );

            var writer = new StringWriter();
            mapper.Write( writer, [new Point( 3, 4 )], new DelimitedOptions { RecordSeparator = "\r\n" } );

            Assert.AreEqual( "3,4\r\n", writer.ToString() );
        }

        [TestMethod]
        public void TestRead_AStructThatWouldHaveToBeFilledIsRefused()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Boxed() );
            mapper.Property( x => x.Id );
            mapper.Property( x => x.Name );

            var exception = Assert.ThrowsExactly<FlatFileException>(
                () => mapper.Read( new StringReader( "1,Bob\r\n" ), new DelimitedOptions { RecordSeparator = "\r\n" } ).ToList() );

            StringAssert.Contains( exception.Message, "value type" );
        }

        [TestMethod]
        public void TestRead_AReadOnlyRecordStructIsBuiltByItsConstructor()
        {
            // Nothing is set after the entity is built, so there is no copy to lose anything to.
            var mapper = DelimitedTypeMapper.Define<Point>();
            mapper.Property( x => x.X );
            mapper.Property( x => x.Y );

            var points = mapper.Read( new StringReader( "3,4\r\n" ), new DelimitedOptions { RecordSeparator = "\r\n" } ).ToList();

            Assert.AreEqual( 1, points.Count );
            Assert.AreEqual( 3, points[0].X );
            Assert.AreEqual( 4, points[0].Y );
        }

        public struct Boxed
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        public struct Fielded
        {
            public int Id;

            public string Name;
        }

        public readonly record struct Point( int X, int Y );
    }
}
