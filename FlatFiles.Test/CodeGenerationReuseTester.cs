using System;
using System.IO;
using System.Linq;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     What a type mapper emits, and how often.
    /// </summary>
    /// <remarks>
    ///     Every call to read or write used to build a new mapper, and with it a new code generator - and a code
    ///     generator defines a dynamic assembly of its own. A caller writing a record at a time built one dynamic
    ///     assembly per record, until the process ran out of mappings for executable memory and the runtime could
    ///     not start another. It failed as a crash with no stack into the library's own code, so nothing pointed
    ///     at the mapper.
    ///     <para>
    ///         <see cref="EmitCodeGenerator.Preparations" /> counted what a single read emitted and caught the
    ///         same mistake one level down at 8.1.0. These count what repeated calls emit, which is where it was
    ///         still being made. It is what these assert on rather than how many dynamic assemblies exist: a
    ///         generator defines its assembly as collectible, so that count falls as well as rises and makes a
    ///         flaky test. One emitted preparation is one too many here however the runtime is feeling.
    ///     </para>
    /// </remarks>
    [TestClass]
    public class CodeGenerationReuseTester
    {
        private const int Calls = 20;

        public class Trade
        {
            public string Key { get; set; } = string.Empty;

            public int Quantity { get; set; }
        }

        private static IFixedLengthTypeMapper<Trade> FixedMapper()
        {
            var mapper = FixedLengthTypeMapper.Define( () => new Trade() );
            mapper.Property( x => x.Key, 10 );
            mapper.Property( x => x.Quantity, 6 );
            return mapper;
        }

        private static IDelimitedTypeMapper<Trade> DelimitedMapper()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Trade() );
            mapper.Property( x => x.Key );
            mapper.Property( x => x.Quantity );
            return mapper;
        }

        [TestMethod]
        public void TestWritingRepeatedly_EmitsOnce()
        {
            var mapper = FixedMapper();
            // The first call is what does the emitting; the count is taken around the rest.
            mapper.Write( new StringWriter(), [new Trade()] );

            var prepared = EmitCodeGenerator.Preparations;
            for (var index = 0; index != Calls; ++index)
            {
                mapper.Write( new StringWriter(), [new Trade { Key = "K", Quantity = index }] );
            }

            Assert.AreEqual( prepared, EmitCodeGenerator.Preparations, "code was emitted per call" );
        }

        [TestMethod]
        public void TestReadingRepeatedly_EmitsOnce()
        {
            var mapper = FixedMapper();
            var record = "K         " + "     1";
            _ = mapper.Read( new StringReader( record ) ).ToArray();

            var prepared = EmitCodeGenerator.Preparations;
            for (var index = 0; index != Calls; ++index)
            {
                _ = mapper.Read( new StringReader( record ) ).ToArray();
            }

            Assert.AreEqual( prepared, EmitCodeGenerator.Preparations, "code was emitted per call" );
        }

        [TestMethod]
        public void TestDelimitedWritingRepeatedly_EmitsOnce()
        {
            var mapper = DelimitedMapper();
            mapper.Write( new StringWriter(), [new Trade()] );

            var prepared = EmitCodeGenerator.Preparations;
            for (var index = 0; index != Calls; ++index)
            {
                mapper.Write( new StringWriter(), [new Trade { Key = "K", Quantity = index }] );
            }

            Assert.AreEqual( prepared, EmitCodeGenerator.Preparations, "code was emitted per call" );
        }

        /// <summary>
        ///     A mapping added after the mapper has been used still takes effect, which is what stops the holding
        ///     above from being a behaviour change.
        /// </summary>
        [TestMethod]
        public void TestAddingAMappingAfterUse_IsPickedUp()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Trade() );
            mapper.Property( x => x.Key );

            var first = new StringWriter();
            mapper.Write( first, [new Trade { Key = "K", Quantity = 7 }] );
            Assert.AreEqual( "K", first.ToString().Trim() );

            mapper.Property( x => x.Quantity );

            var second = new StringWriter();
            mapper.Write( second, [new Trade { Key = "K", Quantity = 7 }] );
            Assert.AreEqual( "K,7", second.ToString().Trim() );
        }

        /// <summary>
        ///     Turning optimisation off after the mapper has been used still takes effect, which is why both
        ///     kinds of generator are kept rather than one.
        /// </summary>
        [TestMethod]
        public void TestTurningOptimisationOffAfterUse_StopsEmitting()
        {
            var mapper = DelimitedMapper();
            mapper.Write( new StringWriter(), [new Trade()] );

            mapper.OptimiseMapping( false );

            var prepared = EmitCodeGenerator.Preparations;
            var written = new StringWriter();
            mapper.Write( written, [new Trade { Key = "K", Quantity = 7 }] );

            Assert.AreEqual( prepared, EmitCodeGenerator.Preparations, "it emitted code after being told not to" );
            Assert.AreEqual( "K,7", written.ToString().Trim() );
        }
    }
}
