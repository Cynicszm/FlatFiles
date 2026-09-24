using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests reading onto an entity without any value becoming an object on the way. A type mapper takes that
    ///     path only where every column and member it maps allows it, and has to fall back to the array of parsed
    ///     values otherwise, so these cover both: that the path reads what the slower one reads, and that everything
    ///     which sends a mapping back to the slower one still behaves.
    /// </summary>
    [TestClass]
    public class TypedParseTester
    {
        private const string Record = "Alice,3,7,1.50\r\nBob,4,,2.25\r\n";

        private static IDelimitedTypeMapper<Person> Mapper()
        {
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Count );
            mapper.Property( x => x.Level );
            mapper.Property( x => x.Amount );
            return mapper;
        }

        private static IFixedLengthTypeMapper<Person> FixedMapper()
        {
            var mapper = FixedLengthTypeMapper.Define<Person>();
            mapper.Property( x => x.Name, 10 );
            mapper.Property( x => x.Count, 4 );
            mapper.Property( x => x.Level, 4 );
            mapper.Property( x => x.Amount, 8 );
            return mapper;
        }

        private static string Describe( Person person )
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}/{2}/{3}",
                person.Name,
                person.Count,
                person.Level.HasValue ? person.Level.Value.ToString( CultureInfo.InvariantCulture ) : "null",
                person.Amount );
        }

        [TestMethod]
        public void TestRead_EveryMemberTakesItsValue()
        {
            var people = Mapper().Read( new StringReader( Record ) ).ToArray();

            Assert.AreEqual( 2, people.Length );
            Assert.AreEqual( "Alice/3/7/1.50", Describe( people[0] ) );
            Assert.AreEqual( "Bob/4/null/2.25", Describe( people[1] ) );
        }

        [TestMethod]
        public void TestRead_EmptyValueForANullableMember_IsNull()
        {
            var people = Mapper().Read( new StringReader( "Alice,3,,1.50\r\n" ) ).ToArray();

            Assert.IsNull( people[0].Level );
        }

        [TestMethod]
        public void TestRead_NullFormatter_NullsAReferenceMember()
        {
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( x => x.Name ).NullFormatter( NullFormatter.ForValue( "NULL" ) );
            mapper.Property( x => x.Count );
            mapper.Property( x => x.Level );
            mapper.Property( x => x.Amount );

            var people = mapper.Read( new StringReader( "NULL,3,7,1.50\r\n" ) ).ToArray();

            Assert.IsNull( people[0].Name );
            Assert.AreEqual( 3, people[0].Count );
        }

        [TestMethod]
        public void TestRead_DefaultValue_SubstitutesForAnEmptyValue()
        {
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Count ).DefaultValue( DefaultValue.Use( 99 ) );
            mapper.Property( x => x.Level );
            mapper.Property( x => x.Amount );

            var people = mapper.Read( new StringReader( "Alice,,7,1.50\r\n" ) ).ToArray();

            Assert.AreEqual( 99, people[0].Count );
        }

        [TestMethod]
        public void TestRead_TrimmedValue_LosesItsPadding()
        {
            var people = Mapper().Read( new StringReader( "  Alice  ,  3  ,7,1.50\r\n" ) ).ToArray();

            Assert.AreEqual( "Alice", people[0].Name );
            Assert.AreEqual( 3, people[0].Count );
        }

        [TestMethod]
        public void TestRead_ColumnErrorHandler_SubstitutesForABadValue()
        {
            var reader = Mapper().GetReader( new StringReader( "Alice,nonsense,7,1.50\r\n" ) );
            reader.ColumnError += ( sender, e ) =>
            {
                e.Substitution = 42;
                e.IsHandled = true;
            };

            var people = Read( reader );

            Assert.AreEqual( "Alice/42/7/1.50", Describe( people[0] ) );
        }

        [TestMethod]
        public void TestRead_BadValueWithNoHandler_RaisesARecordProcessingException()
        {
            var reader = Mapper().GetReader( new StringReader( "Alice,nonsense,7,1.50\r\n" ) );

            Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );
        }

        [TestMethod]
        public void TestRead_RecordErrorHandler_SkipsTheBadRecord()
        {
            var reader = Mapper().GetReader( new StringReader( "Alice,nonsense,7,1.50\r\nBob,4,8,2.25\r\n" ) );
            reader.RecordError += ( sender, e ) => e.IsHandled = true;

            var people = Read( reader );

            Assert.AreEqual( 1, people.Count );
            Assert.AreEqual( "Bob/4/8/2.25", Describe( people[0] ) );
        }

        [TestMethod]
        public void TestRead_IgnoredColumn_DoesNotShiftTheRest()
        {
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( x => x.Name );
            mapper.Ignored();
            mapper.Property( x => x.Count );
            mapper.Property( x => x.Level );
            mapper.Property( x => x.Amount );

            var people = mapper.Read( new StringReader( "Alice,skip,3,7,1.50\r\n" ) ).ToArray();

            Assert.AreEqual( "Alice/3/7/1.50", Describe( people[0] ) );
        }

        [TestMethod]
        public void TestRead_ColumnContextDisabled_ReadsTheSameValues()
        {
            var options = new DelimitedOptions { IsColumnContextDisabled = true };

            var people = Mapper().Read( new StringReader( Record ), options ).ToArray();

            Assert.AreEqual( "Alice/3/7/1.50", Describe( people[0] ) );
            Assert.AreEqual( "Bob/4/null/2.25", Describe( people[1] ) );
        }

        [TestMethod]
        public void TestRead_TheSameMapperTwice_ReadsBoth()
        {
            var mapper = Mapper();

            var first = mapper.Read( new StringReader( "Alice,3,7,1.50\r\n" ) ).ToArray();
            var second = mapper.Read( new StringReader( "Bob,4,8,2.25\r\n" ) ).ToArray();

            Assert.AreEqual( "Alice/3/7/1.50", Describe( first[0] ) );
            Assert.AreEqual( "Bob/4/8/2.25", Describe( second[0] ) );
        }

        [TestMethod]
        public async Task TestReadAsync_EveryMemberTakesItsValue()
        {
            var reader = Mapper().GetReader( new StringReader( Record ) );

            var people = new List<Person>();
            while (await reader.ReadAsync())
            {
                people.Add( reader.Current );
            }

            Assert.AreEqual( 2, people.Count );
            Assert.AreEqual( "Alice/3/7/1.50", Describe( people[0] ) );
            Assert.AreEqual( "Bob/4/null/2.25", Describe( people[1] ) );
        }

        [TestMethod]
        public void TestRead_OnParsedHook_StillRuns()
        {
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( x => x.Name ).OnParsed( ( context, value ) => value + "!" );
            mapper.Property( x => x.Count );
            mapper.Property( x => x.Level );
            mapper.Property( x => x.Amount );

            var people = mapper.Read( new StringReader( "Alice,3,7,1.50\r\n" ) ).ToArray();

            Assert.AreEqual( "Alice!", people[0].Name );
        }

        [TestMethod]
        public void TestRead_MetadataColumn_StillNumbersTheRecords()
        {
            var mapper = DelimitedTypeMapper.Define<Numbered>();
            mapper.Property( x => x.Name );
            mapper.CustomMapping( new RecordNumberColumn( "Row" ) { IncludeSchema = false } ).WithReader( x => x.Row );

            var rows = mapper.Read( new StringReader( "Alice\r\nBob\r\n" ) ).ToArray();

            Assert.AreEqual( 1, rows[0].Row );
            Assert.AreEqual( 2, rows[1].Row );
            Assert.AreEqual( "Bob", rows[1].Name );
        }

        [TestMethod]
        public void TestRead_RecordParsedHandler_SeesTheParsedValues()
        {
            var reader = Mapper().GetReader( new StringReader( "Alice,3,7,1.50\r\n" ) );
            object?[] seen = null!;
            reader.RecordParsed += ( sender, e ) => seen = e.Values!;

            var people = Read( reader );

            Assert.AreEqual( "Alice/3/7/1.50", Describe( people[0] ) );
            Assert.IsNotNull( seen );
            Assert.AreEqual( "Alice", seen[0] );
            Assert.AreEqual( 3, seen[1] );
        }

        [TestMethod]
        public void TestRead_RecordReadHandler_ReplacesAValue()
        {
            var reader = Mapper().GetReader( new StringReader( "Alice,3,7,1.50\r\n" ) );
            reader.RecordRead += ( sender, e ) => e.Values[0] = "Replaced";

            var people = Read( reader );

            Assert.AreEqual( "Replaced", people[0].Name );
            Assert.AreEqual( 3, people[0].Count );
        }

        [TestMethod]
        public void TestRead_NonPublicSetter_StillReadsThroughTheGeneratedCode()
        {
            var mapper = DelimitedTypeMapper.Define<Restricted>();
            mapper.Property( x => x.Count );

            var reader = mapper.GetReader( new StringReader( "3\r\n" ) );

            // The generated code cannot reach the setter either; what matters is that the two paths agree.
            Assert.ThrowsExactly<System.MethodAccessException>( () => reader.Read() );
        }

        [TestMethod]
        public void TestRead_ColumnErrorHandler_SubstitutesOntoANullableMember()
        {
            var reader = Mapper().GetReader( new StringReader( "Alice,3,nonsense,1.50\r\n" ) );
            reader.ColumnError += ( sender, e ) =>
            {
                e.Substitution = 8;
                e.IsHandled = true;
            };

            var people = Read( reader );

            Assert.AreEqual( "Alice/3/8/1.50", Describe( people[0] ) );
        }

        [TestMethod]
        public void TestRead_ColumnErrorHandler_SubstitutesNullOntoANullableMember()
        {
            var reader = Mapper().GetReader( new StringReader( "Alice,3,nonsense,1.50\r\n" ) );
            reader.ColumnError += ( sender, e ) =>
            {
                e.Substitution = null;
                e.IsHandled = true;
            };

            var people = Read( reader );

            Assert.IsNull( people[0].Level );
        }

        [TestMethod]
        public void TestRead_EmptyValueForANonNullableMember_RaisesARecordProcessingException()
        {
            var reader = Mapper().GetReader( new StringReader( "Alice,,7,1.50\r\n" ) );

            Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );
        }

        [TestMethod]
        public void TestFixedLengthRead_EveryMemberTakesItsValue()
        {
            var people = FixedMapper().Read( new StringReader( "Alice     3   7       1.50\r\nBob       4           2.25\r\n" ) ).ToArray();

            Assert.AreEqual( 2, people.Length );
            Assert.AreEqual( "Alice/3/7/1.50", Describe( people[0] ) );
            Assert.AreEqual( "Bob/4/null/2.25", Describe( people[1] ) );
        }

        [TestMethod]
        public void TestFixedLengthRead_IgnoredColumn_DoesNotShiftTheRest()
        {
            var mapper = FixedLengthTypeMapper.Define<Person>();
            mapper.Property( x => x.Name, 10 );
            mapper.Ignored( 5 );
            mapper.Property( x => x.Count, 4 );
            mapper.Property( x => x.Level, 4 );
            mapper.Property( x => x.Amount, 8 );

            var people = mapper.Read( new StringReader( "Alice     skip 3   7       1.50\r\n" ) ).ToArray();

            Assert.AreEqual( "Alice/3/7/1.50", Describe( people[0] ) );
        }

        [TestMethod]
        public void TestFixedLengthRead_RecordErrorHandler_SkipsTheBadRecord()
        {
            var reader = FixedMapper().GetReader( new StringReader( "Alice     xx  7       1.50\r\nBob       4   8       2.25\r\n" ) );
            reader.RecordError += ( sender, e ) => e.IsHandled = true;

            var people = Read( reader );

            Assert.AreEqual( 1, people.Count );
            Assert.AreEqual( "Bob/4/8/2.25", Describe( people[0] ) );
        }

        [TestMethod]
        public void TestFixedLengthRead_RecordParsedHandler_SeesTheParsedValues()
        {
            var reader = FixedMapper().GetReader( new StringReader( "Alice     3   7       1.50\r\n" ) );
            object?[] seen = null!;
            reader.RecordParsed += ( sender, e ) => seen = e.Values;

            var people = Read( reader );

            Assert.AreEqual( "Alice/3/7/1.50", Describe( people[0] ) );
            Assert.IsNotNull( seen );
            Assert.AreEqual( "Alice", seen[0] );
        }

        private static List<Person> Read( ITypedReader<Person> reader )
        {
            var people = new List<Person>();
            while (reader.Read())
            {
                people.Add( reader.Current );
            }
            return people;
        }

        internal class Person
        {
            public string Name { get; set; } = string.Empty;

            public int Count { get; set; }

            public int? Level { get; set; }

            public decimal Amount { get; set; }
        }

        internal class Numbered
        {
            public string Name { get; set; } = string.Empty;

            public int Row { get; set; }
        }

        internal class Restricted
        {
            public int Count { get; private set; }
        }
    }
}
