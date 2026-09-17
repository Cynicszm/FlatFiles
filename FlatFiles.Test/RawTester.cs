using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    [TestClass]
    public sealed class RawTester
    {
        [TestMethod]
        public void TestReadWrite_Comments()
        {
            var output = new StringWriter();
            var writer = new DelimitedWriter( output );
            writer.Write( [ "a", "b", "c" ] );
            writer.WriteRaw( "# Hello, world!!!", true );
            writer.Write( [ "d", "e", "f" ] );

            var input = new StringReader( output.ToString() );
            var reader = new DelimitedReader( input );
            reader.RecordRead += ( _, e ) =>
            {
                e.IsSkipped = e.Values.Length > 0 && e.Values[0].StartsWith( "#" );
            };
            Assert.IsTrue( reader.Read() );
            CollectionAssert.AreEqual( new[] { "a", "b", "c" }, reader.GetValues() );
            Assert.IsTrue( reader.Read() );
            CollectionAssert.AreEqual( new[] { "d", "e", "f" }, reader.GetValues() );
            Assert.IsFalse( reader.Read() );
        }

        [TestMethod]
        public async Task TestReadWriteAsync_Comments()
        {
            var output = new StringWriter();
            var writer = new DelimitedWriter( output );
            await writer.WriteAsync( [ "a", "b", "c" ] );
            await writer.WriteRawAsync( "# Hello, world!!!", true );
            await writer.WriteAsync( [ "d", "e", "f" ] );

            var input = new StringReader( output.ToString() );
            var reader = new DelimitedReader( input );
            reader.RecordRead += ( _, e ) =>
            {
                e.IsSkipped = e.Values.Length > 0 && e.Values[0].StartsWith( "#" );
            };
            Assert.IsTrue( await reader.ReadAsync() );
            CollectionAssert.AreEqual( new[] { "a", "b", "c" }, reader.GetValues() );
            Assert.IsTrue( await reader.ReadAsync() );
            CollectionAssert.AreEqual( new[] { "d", "e", "f" }, reader.GetValues() );
            Assert.IsFalse( await reader.ReadAsync() );
        }
    }
}
