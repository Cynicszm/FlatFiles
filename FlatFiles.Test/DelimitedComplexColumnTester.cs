using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests a delimited column whose value is itself a delimited record with its own separator.
    /// </summary>
    [TestClass]
    public class DelimitedComplexColumnTester
    {
        private static DelimitedSchema OuterSchema()
        {
            var inner = new DelimitedSchema();
            inner.AddColumn( new Int32Column( "a" ) );
            inner.AddColumn( new StringColumn( "b" ) );

            var outer = new DelimitedSchema();
            outer.AddColumn( new Int32Column( "id" ) );
            outer.AddColumn( new DelimitedComplexColumn( "nested", inner, new DelimitedOptions { Separator = ";" } ) );
            return outer;
        }

        [TestMethod]
        public void TestRead_NestedValues_AreParsedIntoAnArray()
        {
            var reader = new DelimitedReader( new StringReader( "1,2;x\r\n" ), OuterSchema() );

            Assert.IsTrue( reader.Read(), "The record should have been read." );
            var values = reader.GetValues();
            Assert.AreEqual( 1, values[0] );
            var nested = (object[]) values[1]!;
            Assert.AreEqual( 2, nested[0] );
            Assert.AreEqual( "x", nested[1] );
            Assert.IsFalse( reader.Read(), "Only one record was written." );
        }

        [TestMethod]
        public void TestWrite_NestedArray_IsFormattedWithItsOwnSeparator()
        {
            var writer = new StringWriter();
            var flatWriter = new DelimitedWriter( writer, OuterSchema() );

            flatWriter.Write( [ 1, new object[] { 2, "x" } ] );

            Assert.AreEqual( "1,2;x", writer.ToString().TrimEnd( '\r', '\n' ) );
        }
    }
}
