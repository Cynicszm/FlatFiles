using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests that an optimised mapper falls back to the reflection code generator where the runtime cannot generate
    ///     code, as under Native AOT, instead of throwing from Reflection.Emit on the first read. The switch is a
    ///     process-wide static, so these tests do not run alongside others.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class CodeGeneratorFallbackTester
    {
        [TestMethod]
        public void TestIsSupported_DefaultsToTheRuntimeFeature()
        {
            Assert.AreEqual( RuntimeFeature.IsDynamicCodeSupported, DynamicCode.IsSupported );
        }

        [TestMethod]
        public void TestDelimitedMapper_WithoutDynamicCode_ReadsAndWritesThroughReflection()
        {
            WithoutDynamicCode( () =>
            {
                var mapper = DelimitedTypeMapper.Define<Item>();
                mapper.Property( item => item.Id );
                mapper.Property( item => item.Name );

                var items = mapper.Read( new StringReader( "1,Bob\r\n2,Jane\r\n" ) ).ToList();
                var writer = new StringWriter();
                mapper.Write( writer, items, new DelimitedOptions { RecordSeparator = "\r\n" } );

                Assert.HasCount( 2, items );
                Assert.AreEqual( "Jane", items[1].Name );
                Assert.AreEqual( "1,Bob\r\n2,Jane\r\n", writer.ToString() );
            } );
        }

        [TestMethod]
        public void TestFixedLengthMapper_WithoutDynamicCode_ReadsAndWritesThroughReflection()
        {
            WithoutDynamicCode( () =>
            {
                var mapper = FixedLengthTypeMapper.Define<Item>();
                mapper.Property( item => item.Id, 3 );
                mapper.Property( item => item.Name, 5 );

                var items = mapper.Read( new StringReader( "1  Bob  \r\n2  Jane \r\n" ) ).ToList();
                var writer = new StringWriter();
                mapper.Write( writer, items, new FixedLengthOptions { RecordSeparator = "\r\n" } );

                Assert.HasCount( 2, items );
                Assert.AreEqual( 2, items[1].Id );
                Assert.AreEqual( "1  Bob  \r\n2  Jane \r\n", writer.ToString() );
            } );
        }

        [TestMethod]
        public void TestDynamicMapper_WithoutDynamicCode_ReadsThroughReflection()
        {
            WithoutDynamicCode( () =>
            {
                var mapper = DelimitedTypeMapper.DefineDynamic( typeof( Item ) );
                mapper.Int32Property( "Id" );
                mapper.StringProperty( "Name" );

                var items = mapper.Read( new StringReader( "7,Ann\r\n" ) ).Cast<Item>().ToList();

                Assert.AreEqual( 7, items.Single().Id );
                Assert.AreEqual( "Ann", items.Single().Name );
            } );
        }

        private static void WithoutDynamicCode( System.Action test )
        {
            var supported = DynamicCode.IsSupported;
            DynamicCode.IsSupported = false;
            try
            {
                test();
            }
            finally
            {
                DynamicCode.IsSupported = supported;
            }
        }

        public sealed class Item
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;
        }
    }
}
