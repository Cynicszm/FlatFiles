using System;
using System.Collections.Generic;
using System.IO;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    [TestClass]
    public class FixedLengthRuntimeTypeTester
    {
        [TestMethod]
        public void TestAnonymousTypeDefinition()
        {
            var mapper = FixedLengthTypeMapper.Define( () => new
            {
                Name = (string) null!
            } );
            mapper.Property( x => x.Name, 10 ).ColumnName( "Name" );
            var writer = new StringWriter();
            mapper.Write( writer,
            [
                new { Name = "John" }, new { Name = "Sam" }
            ] );
            var result = writer.ToString();
            var expected = $"John      {Environment.NewLine}Sam       {Environment.NewLine}";
            Assert.AreEqual( expected, result );
        }

        [TestMethod]
        public void TestRuntimeTypeDefinition()
        {
            var mapper = FixedLengthTypeMapper.DefineDynamic( typeof( Person ) );
            mapper.StringProperty( "Name", 10 ).ColumnName( "Name" );
            mapper.Int32Property( "IQ", 3 ).ColumnName( "IQ" );
            mapper.DateTimeProperty( "BirthDate", 10 ).ColumnName( "BirthDate" ).InputFormat( "yyyyMMdd" ).OutputFormat( "yyyyMMdd" );
            mapper.DecimalProperty( "TopSpeed", 10 ).ColumnName( "TopSpeed" );

            Person[] people = [
                new() { Name = "John", IQ = null, BirthDate = new DateTime( 1954, 10, 29 ), TopSpeed = 3.4m },
                new() { Name = "Susan", IQ = 132, BirthDate = new DateTime( 1984, 3, 15 ), TopSpeed = 10.1m }
            ];

            var writer = new StringWriter();
            mapper.Write( writer, people );
            var result = writer.ToString();

            var reader = new StringReader( result );
            object[] parsed = [.. mapper.Read( reader )];
            Assert.AreEqual( 2, parsed.Length );
            Assert.IsInstanceOfType( parsed[0], typeof( Person ) );
            Assert.IsInstanceOfType( parsed[1], typeof( Person ) );
            AssertEqual( people[0], (Person) parsed[0] );
            AssertEqual( people[1], (Person) parsed[1] );
        }

        [TestMethod]
        public void TestRuntimeTypeDefinition_ReaderWriter()
        {
            var mapper = FixedLengthTypeMapper.DefineDynamic( typeof( Person ) );
            mapper.StringProperty( "Name", 10 ).ColumnName( "Name" );
            mapper.Int32Property( "IQ", 3 ).ColumnName( "IQ" );
            mapper.DateTimeProperty( "BirthDate", 10 ).ColumnName( "BirthDate" ).InputFormat( "yyyyMMdd" ).OutputFormat( "yyyyMMdd" );
            mapper.DecimalProperty( "TopSpeed", 10 ).ColumnName( "TopSpeed" );

            Person[] people = [
                new() { Name = "John", IQ = null, BirthDate = new DateTime( 1954, 10, 29 ), TopSpeed = 3.4m },
                new() { Name = "Susan", IQ = 132, BirthDate = new DateTime( 1984, 3, 15 ), TopSpeed = 10.1m }
            ];

            var writer = new StringWriter();
            var entityWriter = mapper.GetWriter( writer );
            foreach (var person in people)
            {
                entityWriter.Write( person );
            }
            var result = writer.ToString();

            var reader = new StringReader( result );
            var entityReader = mapper.GetReader( reader );
            List<object> parsed = [];
            while (entityReader.Read())
            {
                parsed.Add( entityReader.Current );
            }
            Assert.AreEqual( 2, parsed.Count );
            Assert.IsInstanceOfType( parsed[0], typeof( Person ) );
            Assert.IsInstanceOfType( parsed[1], typeof( Person ) );
            AssertEqual( people[0], (Person) parsed[0] );
            AssertEqual( people[1], (Person) parsed[1] );
        }

        private static void AssertEqual( Person person1, Person person2 )
        {
            Assert.AreEqual( person1.Name, person2.Name );
            Assert.AreEqual( person1.IQ, person2.IQ );
            Assert.AreEqual( person1.BirthDate, person2.BirthDate );
            Assert.AreEqual( person1.TopSpeed, person2.TopSpeed );
        }

        public class Person
        {
            public string Name { get; set; } = string.Empty;

            public int? IQ { get; set; }

            public DateTime BirthDate { get; set; }

            public decimal TopSpeed { get; set; }
        }
    }
}
