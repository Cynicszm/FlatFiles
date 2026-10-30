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
    // Reads a counter the whole process shares, as the other classes that read it do.
    [DoNotParallelize]
    [TestClass]
    public class CodeGenerationReuseTester
    {
        private const int Calls = 20;

        // Ten characters for the key and six for the quantity, which is what the schema below declares.
        private const string Record = "K         " + "     1";

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
        ///     Building a type mapper per call no longer defines a dynamic assembly per call.
        /// </summary>
        /// <remarks>
        ///     Holding the mapper fixed the caller who reuses one. A caller who builds one per file never had a
        ///     mapper to reuse, and went on defining an assembly each time - which is what runs a process out of
        ///     mappings for executable memory. Every type mapper now shares one generator, so what grows is
        ///     types in a single module.
        ///     <para>
        ///         The assertion is an upper bound rather than an equality on purpose: a generator defines its
        ///         assembly as collectible, so the count can fall while this runs. It cannot rise for the reason
        ///         this is about without rising by one per call.
        ///     </para>
        /// </remarks>
        [TestMethod]
        public void TestAMapperPerCall_DoesNotDefineAnAssemblyPerCall()
        {
            // Warm whatever the first mapper of a process emits, so none of it lands inside the count.
            _ = FixedMapper().Read( new StringReader( Record ) ).ToArray();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Count( assembly => assembly.IsDynamic );
            for (var index = 0; index != Calls; ++index)
            {
                _ = FixedMapper().Read( new StringReader( Record ) ).ToArray();
            }

            Assert.IsLessThanOrEqualTo( assemblies,
                AppDomain.CurrentDomain.GetAssemblies().Count( assembly => assembly.IsDynamic ),
                "a dynamic assembly was defined per type mapper" );
        }

        /// <summary>
        ///     The emitted factory is <c>new TEntity()</c> and nothing else, so it depends on the type and not
        ///     on the mapping. Two mappers over one entity emit it once between them.
        /// </summary>
        [TestMethod]
        public void TestTheFactoryIsHeldPerEntityType()
        {
            var first = EmitCodeGenerator.Shared.GetFactory<Trade>();

            var prepared = EmitCodeGenerator.Preparations;
            var second = EmitCodeGenerator.Shared.GetFactory<Trade>();

            Assert.AreSame( first, second, "the factory was emitted again for a type that already had one" );
            Assert.AreEqual( prepared, EmitCodeGenerator.Preparations, "something was emitted for a cache hit" );
            Assert.IsNotNull( second() );
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
