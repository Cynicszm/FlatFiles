using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    [TestClass]
    public class DelimitedTypeMapperSelectorTester
    {
        [TestMethod]
        public void TestSimpleSelection()
        {
            var mapper = DelimitedTypeMapper.Define<Data>();
            mapper.Property( x => x.Amount );

            var selector = new DelimitedTypeMapperSelector();
            selector.When( v => v.Length == 1 ).Use( mapper );

            var stringReader = new StringReader( "100\r\n200\r\n" );
            var reader = selector.GetReader( stringReader );

            List<Data> data = [.. reader.ReadAll().Cast<Data>()];
            Assert.AreEqual( 2, data.Count );
            Assert.AreEqual( 100, data[0].Amount );
            Assert.AreEqual( 200, data[1].Amount );
        }

        public class Data
        {
            public decimal Amount { get; set; }
        }
    }
}
