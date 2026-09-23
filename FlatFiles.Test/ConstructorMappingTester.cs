using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests reading onto a type that cannot be built empty and filled afterwards. Where a type has no
    ///     parameterless constructor, the constructor it does have is given the parsed values, matched to its
    ///     parameters by name. That is the only way into a property with no setter, and the only way to let a
    ///     type check what it is given rather than be contradicted after the fact.
    /// </summary>
    [TestClass]
    public class ConstructorMappingTester
    {
        private const string Record = "Alice,3\r\n";

        [TestMethod]
        public void TestRead_PositionalRecord_IsBuiltByItsConstructor()
        {
            var mapper = DelimitedTypeMapper.Define<Positional>();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Count );

            var read = mapper.Read( new StringReader( Record ) ).Single();

            Assert.AreEqual( "Alice", read.Name );
            Assert.AreEqual( 3, read.Count );
        }

        [TestMethod]
        public void TestRead_GetOnlyProperties_AreFilled()
        {
            var mapper = DelimitedTypeMapper.Define<GetOnly>();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Count );

            var read = mapper.Read( new StringReader( Record ) ).Single();

            Assert.AreEqual( "Alice", read.Name );
            Assert.AreEqual( 3, read.Count );
        }

        [TestMethod]
        public void TestRead_ConstructorThatValidates_SeesTheParsedValues()
        {
            var mapper = DelimitedTypeMapper.Define<Validating>();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Count );

            var read = mapper.Read( new StringReader( Record ) ).Single();

            Assert.AreEqual( "ALICE", read.Name, "The constructor was given the value and kept its own version of it." );
        }

        [TestMethod]
        public void TestRead_ConstructorThatRejects_ThrowsWhatTheConstructorThrew()
        {
            var mapper = DelimitedTypeMapper.Define<Validating>();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Count );

            // The record carrying a name the constructor refuses produces no entity, and the constructor's own
            // exception is what reaches the caller. That is how the library already treats a failure while an
            // entity is being assembled - a custom reader that throws does the same - rather than turning it into
            // a RecordProcessingException a RecordError handler could skip. Worth knowing, because a validating
            // constructor is the reason to use this at all.
            // An empty field parses to null rather than to an empty string, so it is the null guard that fires.
            Assert.ThrowsExactly<ArgumentNullException>( () => mapper.Read( new StringReader( ",3\r\n" ) ).ToList() );
        }

        [TestMethod]
        public void TestRead_ParametersOutOfOrder_TakeTheirOwnValues()
        {
            var mapper = DelimitedTypeMapper.Define<Reversed>();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Count );

            var read = mapper.Read( new StringReader( Record ) ).Single();

            Assert.AreEqual( "Alice", read.Name, "A parameter is matched by name, not by position." );
            Assert.AreEqual( 3, read.Count );
        }

        [TestMethod]
        public void TestRead_ParameterNameDifferingInCase_StillMatches()
        {
            var mapper = DelimitedTypeMapper.Define<LowerCaseParameters>();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Count );

            var read = mapper.Read( new StringReader( Record ) ).Single();

            Assert.AreEqual( "Alice", read.Name );
            Assert.AreEqual( 3, read.Count );
        }

        [TestMethod]
        public void TestRead_GreediestConstructorWins()
        {
            var mapper = DelimitedTypeMapper.Define<TwoConstructors>();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Count );

            var read = mapper.Read( new StringReader( Record ) ).Single();

            Assert.AreEqual( "Alice", read.Name );
            Assert.AreEqual( 3, read.Count, "The constructor taking both was preferred to the one taking a name." );
        }

        [TestMethod]
        public void TestRead_MemberTheConstructorDoesNotTake_IsSetAfterwards()
        {
            var mapper = DelimitedTypeMapper.Define<PartlyConstructed>();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Count );

            var read = mapper.Read( new StringReader( Record ) ).Single();

            Assert.AreEqual( "Alice", read.Name, "Given to the constructor." );
            Assert.AreEqual( 3, read.Count, "Assigned once the entity existed." );
        }

        [TestMethod]
        public void TestRead_NoConstructorMatches_SaysWhatItTried()
        {
            var mapper = DelimitedTypeMapper.Define<Unmatched>();
            mapper.Property( x => x.Name );

            var failure = Assert.ThrowsExactly<FlatFileException>( () => mapper.Read( new StringReader( "Alice\r\n" ) ).ToList() );

            StringAssert.Contains( failure.Message, "Unmatched", "The message names the type." );
            StringAssert.Contains( failure.Message, "Name", "The message names what was mapped." );
        }

        [TestMethod]
        public void TestRead_ParameterOfTheWrongType_IsNotAMatch()
        {
            var mapper = DelimitedTypeMapper.Define<WrongParameterType>();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Count );

            // The names line up and the types do not, which is no match at all: passing an int where a string is
            // asked for would fail at the call rather than at the mapping, and far later.
            var failure = Assert.ThrowsExactly<FlatFileException>( () => mapper.Read( new StringReader( Record ) ).ToList() );

            StringAssert.Contains( failure.Message, "WrongParameterType", "The message names the type." );
        }

        [TestMethod]
        public void TestRead_NoPublicConstructorAtAll_SaysSo()
        {
            var mapper = DelimitedTypeMapper.Define<NoPublicConstructor>();
            mapper.Property( x => x.Name );

            var failure = Assert.ThrowsExactly<FlatFileException>( () => mapper.Read( new StringReader( "Alice\r\n" ) ).ToList() );

            StringAssert.Contains( failure.Message, "no public constructor", "The message says there was nothing to try." );
        }

        [TestMethod]
        public void TestRead_FactorySupplied_IsUsedInsteadOfTheConstructor()
        {
            var mapper = DelimitedTypeMapper.Define( () => new PartlyConstructed( "from the factory" ) );
            mapper.Property( x => x.Count );

            var read = mapper.Read( new StringReader( "3\r\n" ) ).Single();

            Assert.AreEqual( "from the factory", read.Name, "The factory said how to build it, so no constructor was matched." );
            Assert.AreEqual( 3, read.Count, "What the factory did not set is assigned afterwards, as it always was." );
        }

        [TestMethod]
        public void TestRead_ParameterlessConstructor_IsStillUsed()
        {
            var mapper = DelimitedTypeMapper.Define<Settable>();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Count );

            var read = mapper.Read( new StringReader( Record ) ).Single();

            Assert.AreEqual( "Alice", read.Name );
            Assert.AreEqual( 3, read.Count );
        }

        [TestMethod]
        public void TestRead_NonPublicParameterlessConstructor_IsUsed()
        {
            var mapper = DelimitedTypeMapper.Define<PrivateConstructor>();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Count );

            var read = mapper.Read( new StringReader( Record ) ).Single();

            Assert.AreEqual( "Alice", read.Name );
            Assert.AreEqual( 3, read.Count );
        }

        [TestMethod]
        public void TestRead_OptimisationOff_BuildsTheSameEntity()
        {
            var mapper = DelimitedTypeMapper.Define<Positional>();
            mapper.OptimiseMapping( false );
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Count );

            var read = mapper.Read( new StringReader( Record ) ).Single();

            Assert.AreEqual( "Alice", read.Name );
            Assert.AreEqual( 3, read.Count );
        }

        [TestMethod]
        public void TestRead_NullableMemberFeedingANullableParameter_TakesNull()
        {
            var mapper = DelimitedTypeMapper.Define<WithNullable>();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Count );

            var read = mapper.Read( new StringReader( "Alice,\r\n" ) ).Single();

            Assert.AreEqual( "Alice", read.Name );
            Assert.IsNull( read.Count );
        }

        [TestMethod]
        public void TestRead_FixedLength_IsBuiltByItsConstructor()
        {
            var mapper = FixedLengthTypeMapper.Define<Positional>();
            mapper.Property( x => x.Name, 10 );
            mapper.Property( x => x.Count, 4 );

            var read = mapper.Read( new StringReader( "Alice     3   \r\n" ) ).Single();

            Assert.AreEqual( "Alice", read.Name );
            Assert.AreEqual( 3, read.Count );
        }

        [TestMethod]
        public void TestWrite_PositionalRecord_WritesItsValues()
        {
            var mapper = DelimitedTypeMapper.Define<Positional>();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Count );

            var writer = new StringWriter();
            mapper.Write( writer, [new Positional( "Alice", 3 )] );

            Assert.AreEqual( "Alice,3\r\n", writer.ToString() );
        }

        [TestMethod]
        public void TestRead_ManyRecords_BuildsEachOne()
        {
            var mapper = DelimitedTypeMapper.Define<Positional>();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Count );

            List<Positional> read = [.. mapper.Read( new StringReader( "Alice,3\r\nBob,4\r\nCarol,5\r\n" ) )];

            Assert.AreEqual( 3, read.Count );
            Assert.AreEqual( "Bob", read[1].Name );
            Assert.AreEqual( 5, read[2].Count );
        }

        internal record Positional( string Name, int Count );

        internal class GetOnly( string name, int count )
        {
            public string Name { get; } = name;

            public int Count { get; } = count;
        }

        internal class Validating
        {
            public Validating( string name, int count )
            {
                ArgumentException.ThrowIfNullOrEmpty( name );
                Name = name.ToUpperInvariant();
                Count = count;
            }

            public string Name { get; }

            public int Count { get; }
        }

        internal class Reversed( int count, string name )
        {
            public string Name { get; } = name;

            public int Count { get; } = count;
        }

        internal class LowerCaseParameters( string name, int count )
        {
            public string Name { get; } = name;

            public int Count { get; } = count;
        }

        internal class TwoConstructors
        {
            public TwoConstructors( string name )
            {
                Name = name;
                Count = -1;
            }

            public TwoConstructors( string name, int count )
            {
                Name = name;
                Count = count;
            }

            public string Name { get; }

            public int Count { get; }
        }

        internal class PartlyConstructed( string name )
        {
            public string Name { get; } = name;

            public int Count { get; set; }
        }

        internal class Unmatched( int somethingElse )
        {
            public string Name { get; set; } = somethingElse.ToString();
        }

        internal class WrongParameterType( string name, string count )
        {
            public string Name { get; } = name;

            public int Count { get; } = int.Parse( count, System.Globalization.CultureInfo.InvariantCulture );
        }

        internal class NoPublicConstructor
        {
            private NoPublicConstructor( string name )
            {
                Name = name;
            }

            public string Name { get; }
        }

        internal class Settable
        {
            public string Name { get; set; }

            public int Count { get; set; }
        }

        internal class PrivateConstructor
        {
            private PrivateConstructor()
            {
            }

            public string Name { get; set; }

            public int Count { get; set; }
        }

        internal class WithNullable( string name, int? count )
        {
            public string Name { get; } = name;

            public int? Count { get; } = count;
        }
    }
}
