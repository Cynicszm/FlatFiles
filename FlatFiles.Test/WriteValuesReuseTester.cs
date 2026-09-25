using System.IO;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests that a writer filling one array for the whole write does not carry anything from one record into
    ///     the next. A mapping need not write every slot - a custom writer may leave one alone - and a value left
    ///     behind would then be written as though it belonged to the record that followed.
    /// </summary>
    [TestClass]
    public class WriteValuesReuseTester
    {
        [TestMethod]
        public void TestACustomWriterThatSkipsASlot_LeavesNothingBehind()
        {
            var mapper = DelimitedTypeMapper.Define<Thing>();
            mapper.Property( x => x.Id );
            mapper.CustomMapping( new StringColumn( "Note" ) )
                .WithWriter( ( Thing entity, object?[] values ) =>
                {
                    // Only the first record says anything; the second leaves the slot untouched.
                    if (entity.Id == 1)
                    {
                        values[1] = "first";
                    }
                } );

            var writer = new StringWriter();
            mapper.Write( writer, [new Thing { Id = 1 }, new Thing { Id = 2 }], new DelimitedOptions { RecordSeparator = "\r\n" } );

            Assert.AreEqual( "1,first\r\n2,\r\n", writer.ToString() );
        }

        [TestMethod]
        public void TestEveryRecordIsWrittenFromItsOwnValues()
        {
            var mapper = DelimitedTypeMapper.Define<Thing>();
            mapper.Property( x => x.Id );
            mapper.Property( x => x.Name );

            var writer = new StringWriter();
            mapper.Write( writer, [new Thing { Id = 1, Name = "Bob" }, new Thing { Id = 2, Name = "Susan" }, new Thing { Id = 3, Name = "Ann" }],
                new DelimitedOptions { RecordSeparator = "\r\n" } );

            Assert.AreEqual( "1,Bob\r\n2,Susan\r\n3,Ann\r\n", writer.ToString() );
        }

        [TestMethod]
        public void TestAnInjectorWritingSchemasOfDifferentWidths_ResizesBetweenThem()
        {
            var injector = new DelimitedTypeMapperInjector();
            var narrow = DelimitedTypeMapper.Define<Thing>();
            narrow.Property( x => x.Id );
            var wide = DelimitedTypeMapper.Define<Wide>();
            wide.Property( x => x.Id );
            wide.Property( x => x.Name );
            wide.Property( x => x.Extra );
            injector.When<Thing>().Use( narrow );
            injector.When<Wide>().Use( wide );

            var writer = new StringWriter();
            var typed = injector.GetWriter( writer, new DelimitedOptions { RecordSeparator = "\r\n" } );
            typed.Write( new Wide { Id = 1, Name = "Bob", Extra = "x" } );
            typed.Write( new Thing { Id = 2 } );
            typed.Write( new Wide { Id = 3, Name = "Ann", Extra = "y" } );

            Assert.AreEqual( "1,Bob,x\r\n2\r\n3,Ann,y\r\n", writer.ToString() );
        }

        public sealed class Thing
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;
        }

        public sealed class Wide
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;

            public string Extra { get; set; } = string.Empty;
        }
    }
}
