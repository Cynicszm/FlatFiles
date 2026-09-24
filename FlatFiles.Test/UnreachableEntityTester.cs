using System;
using System.IO;
using System.Linq;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests that a type the emitted code cannot see is still mapped. The generated deserialiser lives in an
    ///     assembly of its own, so a type out of its reach used to be found out by a
    ///     <see cref="System.MethodAccessException" /> thrown from generated code on the first record.
    ///     <para>
    ///         Nothing in this suite noticed, and the reason is worth knowing: this assembly names
    ///         <c>FlatFiles.DynamicAssembly</c> in an <c>InternalsVisibleTo</c>, so its internal types are
    ///         reachable when the same types in any other assembly are not. A private nested type is the case that
    ///         no <c>InternalsVisibleTo</c> can open up, which is what these tests map onto.
    ///     </para>
    /// </summary>
    [TestClass]
    public class UnreachableEntityTester
    {
        [TestMethod]
        public void TestPrivateEntity_Optimised_Reads()
        {
            var mapper = Mapper( true );

            var read = mapper.Read( new StringReader( "1,Bob,12.34\r\n2,Susan,13.88\r\n" ) ).ToList();

            Assert.HasCount( 2, read );
            Assert.AreEqual( 1, read[0].Id );
            Assert.AreEqual( "Susan", read[1].Name );
            Assert.AreEqual( 13.88m, read[1].Amount );
        }

        [TestMethod]
        public void TestPrivateEntity_Optimised_Writes()
        {
            var writer = new StringWriter();

            Mapper( true ).Write( writer, [new Tucked { Id = 1, Name = "Bob", Amount = 12.34m }], new DelimitedOptions { RecordSeparator = "\r\n" } );

            Assert.AreEqual( "1,Bob,12.34\r\n", writer.ToString() );
        }

        [TestMethod]
        public void TestPrivateEntity_Unoptimised_Reads()
        {
            var read = Mapper( false ).Read( new StringReader( "3,Ann,1.50\r\n" ) ).Single();

            Assert.AreEqual( "Ann", read.Name );
        }

        [TestMethod]
        public void TestPrivateEntity_FixedLength_Reads()
        {
            var mapper = FixedLengthTypeMapper.Define<Tucked>();
            mapper.Property( x => x.Id, 3 );
            mapper.Property( x => x.Name, 6 );
            mapper.Property( x => x.Amount, 6 );

            var read = mapper.Read( new StringReader( "4  Ann    12.34\r\n" ) ).Single();

            Assert.AreEqual( 4, read.Id );
            Assert.AreEqual( 12.34m, read.Amount );
        }

        [TestMethod]
        public void TestIsAccessible_AnswersForEachKindOfType()
        {
            Assert.IsTrue( TypeVisibility.IsAccessible( typeof( Visible ) ), "A public type is reachable." );
            Assert.IsTrue( TypeVisibility.IsAccessible( typeof( Visible.AlsoVisible ) ), "A public type nested in one is reachable." );
            // This assembly lets the generated code at its internals, so its internal types stay on the faster path.
            Assert.IsTrue( TypeVisibility.IsAccessible( typeof( Hidden ) ), "An internal type in an assembly that opens up to the generated code is reachable." );
            Assert.IsFalse( TypeVisibility.IsAccessible( typeof( Tucked ) ), "A private nested type is reachable from nowhere else." );
            Assert.IsFalse( TypeVisibility.IsAccessible( typeof( System.Collections.Generic.List<Tucked> ) ), "A generic closed over an unreachable type is unreachable." );
            // An assembly that has never heard of the dynamic assembly keeps its internals to itself.
            var elsewhere = Array.Find( typeof( object ).Assembly.GetTypes(), x => !x.IsPublic && !x.IsNested )!;
            Assert.IsFalse( TypeVisibility.IsAccessible( elsewhere ), $"{elsewhere} is internal to an assembly that does not open up." );
        }

        /// <summary>
        ///     Internal, and reachable all the same, because this assembly names the dynamic assembly in an
        ///     <c>InternalsVisibleTo</c>. Here so that the distinction is tested rather than assumed.
        /// </summary>
        internal sealed class Hidden
        {
            public int Id { get; set; }
        }

        public sealed class Visible
        {
            public int Id { get; set; }

            public sealed class AlsoVisible
            {
                public int Id { get; set; }
            }
        }

        private static IDelimitedTypeMapper<Tucked> Mapper( bool isOptimised )
        {
            var mapper = DelimitedTypeMapper.Define<Tucked>();
            mapper.Property( x => x.Id );
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Amount );
            mapper.OptimiseMapping( isOptimised );
            return mapper;
        }

        private sealed class Tucked
        {
            public int Id { get; set; }

            public string Name { get; set; }

            public decimal Amount { get; set; }
        }
    }
}
