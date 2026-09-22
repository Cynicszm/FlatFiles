using System.Globalization;
using System.IO;
using System.Linq;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    [TestClass]
    public class OnParsingTester
    {
        [TestMethod]
        public void ShouldStripNonNumericCharacters()
        {
            const string input = @"=""12345.67"",=""$123""";

            var mapper = DelimitedTypeMapper.Define<Numbers>();
            mapper.Property( x => x.Value ).ColumnName( "value" ).OnParsing( ( _, x ) => x.Trim( '"', '=' ) ).NumberStyles( NumberStyles.AllowDecimalPoint );
            mapper.Property( x => x.Money ).ColumnName( "money" ).OnParsing( ( _, x ) => x.Trim( '"', '=' ) ).NumberStyles( NumberStyles.Currency );

            var reader = new StringReader( input );
            Numbers[] results = [.. mapper.Read( reader )];

            Assert.AreEqual( 1, results.Length );
            var result = results.Single();
            Assert.AreEqual( 12345.67m, result.Value );
            Assert.AreEqual( 123m, result.Money );
        }

        public class Numbers
        {
            public decimal Value { get; set; }

            public decimal Money { get; set; }
        }
    }
}
