using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    [TestClass]
    public class ComplexColumnTester
    {
        [TestMethod]
        public void ShouldRoundTrip()
        {
            const string message = @"Tom,Hanselman,2016-06-0426         Walking Ice,Ace
";
            var stringReader = new StringReader( message );
            var outerSchema = new DelimitedSchema();
            outerSchema.AddColumn( new StringColumn( "FirstName" ) );
            outerSchema.AddColumn( new StringColumn( "LastName" ) );

            var innerSchema = new FixedLengthSchema();
            innerSchema.AddColumn( new DateTimeColumn( "StartDate" ) { InputFormat = "yyyy-MM-dd", OutputFormat = "yyyy-MM-dd" }, 10 );
            innerSchema.AddColumn( new Int32Column( "Age" ), 2 );
            innerSchema.AddColumn( new StringColumn( "StageName" ), new Window( 20 ) { Alignment = FixedAlignment.RightAligned } );
            outerSchema.AddColumn( new FixedLengthComplexColumn( "PlayerStats", innerSchema ) );
            outerSchema.AddColumn( new StringColumn( "Nickname" ) );

            var reader = new DelimitedReader( stringReader, outerSchema );
            Assert.IsTrue( reader.Read(), "A record should have been read." );
            var values = reader.GetValues();
            Assert.AreEqual( "Tom", values[0] );
            Assert.AreEqual( "Hanselman", values[1] );
            Assert.IsInstanceOfType( values[2], typeof( object[] ) );
            var playerValues = (object[]) values[2]!;
            Assert.AreEqual( new DateTime( 2016, 06, 04 ), playerValues[0] );
            Assert.AreEqual( 26, playerValues[1] );
            Assert.AreEqual( "Walking Ice", playerValues[2] );
            Assert.AreEqual( "Ace", values[3] );

            var stringWriter = new StringWriter();
            var writer = new DelimitedWriter( stringWriter, outerSchema );
            writer.Write( values );

            var output = stringWriter.GetStringBuilder().ToString();
            Assert.AreEqual( message, output );
        }

        [TestMethod]
        public void ShouldRoundTrip_TypeMappers()
        {
            const string message = @"Tom,Hanselman,2016-06-0426         Walking Ice,Ace
";
            var stringReader = new StringReader( message );

            var nestedMapper = FixedLengthTypeMapper.Define<Stats>();
            nestedMapper.Property( s => s.StartDate, 10 ).InputFormat( "yyyy-MM-dd" ).OutputFormat( "yyyy-MM-dd" );
            nestedMapper.Property( s => s.Age, 2 );
            nestedMapper.Property( s => s.StageName, new Window( 20 ) { Alignment = FixedAlignment.RightAligned } );

            var mapper = DelimitedTypeMapper.Define<Player>();
            mapper.Property( p => p.FirstName );
            mapper.Property( p => p.LastName );
            mapper.ComplexProperty( p => p.Statistics, nestedMapper );
            mapper.Property( p => p.NickName );

            Player[] players = [.. mapper.Read( stringReader )];
            Assert.AreEqual( 1, players.Length );
            var player = players.Single();
                
            Assert.AreEqual( "Tom", player.FirstName );
            Assert.AreEqual( "Hanselman", player.LastName );
            var statistics = player.Statistics;
            Assert.IsNotNull( statistics );
            Assert.AreEqual( new DateTime( 2016, 06, 04 ), statistics.StartDate );
            Assert.AreEqual( 26, statistics.Age );
            Assert.AreEqual( "Walking Ice", statistics.StageName );
            Assert.AreEqual( "Ace", player.NickName );

            var stringWriter = new StringWriter();
            mapper.Write( stringWriter, [ player ] );
            
            var output = stringWriter.ToString();
            Assert.AreEqual( message, output );
        }

        public class Player
        {
            public string FirstName { get; set; } = string.Empty;

            public string LastName { get; set; } = string.Empty;

            public Stats Statistics { get; set; } = null!;
            
            public string NickName { get; set; } = string.Empty;
        }

        public class Stats
        {
            public DateTime StartDate { get; set; }

            public int Age { get; set; }

            public string StageName { get; set; } = string.Empty;
        }

        [TestMethod]
        public void TestRead_FixedLengthColumn_InsideFixedLength_TrailingWhitespace_NoTrimmingPerformed()
        {
            var petMapper = FixedLengthTypeMapper.Define<Pet>();
            petMapper.Property( x => x.Name, new Window( 10 ) );
            petMapper.Property( x => x.UniversalPetIdentifier, new Window( 5 ) );
            petMapper.Ignored( new Window( 2 ) );

            var personMapper = FixedLengthTypeMapper.Define<Person>();
            personMapper.Property( x => x.Name, new Window( 10 ) );
            personMapper.Ignored( new Window( 1 ) );
            personMapper.ComplexProperty( x => x.Pet1, petMapper, new Window( 17 ) );
            personMapper.ComplexProperty( x => x.Pet2, petMapper, new Window( 17 ) );

            const string line = "John       Doggy     dog01  Rain      cat01  ";

            var reader = personMapper.GetReader( new StringReader( line ) );
            List<Person> people = [.. reader.ReadAll()];

            Assert.AreEqual( 1, people.Count );
            var person = people[0];
            Assert.AreEqual( "John", person.Name );
            var pet1 = person.Pet1;
            Assert.AreEqual( "Doggy", pet1.Name );
            Assert.AreEqual( "dog01", pet1.UniversalPetIdentifier );
            var pet2 = person.Pet2;
            Assert.AreEqual( "Rain", pet2.Name );
            Assert.AreEqual( "cat01", pet2.UniversalPetIdentifier );
        }

        [TestMethod]
        public void TestRead_FixedLengthColumn_InsideDelimited_TrailingWhitespace_NoTrimmingPerformed()
        {
            var petMapper = FixedLengthTypeMapper.Define<Pet>();
            petMapper.Property( x => x.Name, new Window( 10 ) );
            petMapper.Property( x => x.UniversalPetIdentifier, new Window( 5 ) );
            petMapper.Ignored( new Window( 2 ) );

            var personMapper = DelimitedTypeMapper.Define<Person>();
            personMapper.Property( x => x.Name );
            personMapper.ComplexProperty( x => x.Pet1, petMapper );
            personMapper.ComplexProperty( x => x.Pet2, petMapper );

            const string line = "John,Doggy     dog01  ,Rain      cat01  ";

            var reader = personMapper.GetReader( new StringReader( line ) );
            List<Person> people = [.. reader.ReadAll()];

            Assert.AreEqual( 1, people.Count );
            var person = people[0];
            Assert.AreEqual( "John", person.Name );
            var pet1 = person.Pet1;
            Assert.AreEqual( "Doggy", pet1.Name );
            Assert.AreEqual( "dog01", pet1.UniversalPetIdentifier );
            var pet2 = person.Pet2;
            Assert.AreEqual( "Rain", pet2.Name );
            Assert.AreEqual( "cat01", pet2.UniversalPetIdentifier );
        }

        internal class Person
        {
            public string Name { get; set; } = string.Empty;

            public Pet Pet1 { get; set; } = null!;

            public Pet Pet2 { get; set; } = null!;
        }

        internal class Pet
        {
            public string Name { get; set; } = string.Empty;

            public string UniversalPetIdentifier { get; set; } = string.Empty;
        }
    }
}
