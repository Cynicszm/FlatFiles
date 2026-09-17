using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Round-trips the four common delimited formats - comma, tab, pipe and semicolon separated - through the writer,
    ///     the reader and the type mapper, and pins the quoting each separator implies: a value is quoted when it contains
    ///     that file's separator, not when it contains another format's.
    /// </summary>
    [TestClass]
    public class SeparatorFormatTester
    {
        private const string Comma = ",";
        private const string Tab = "\t";
        private const string Pipe = "|";
        private const string Semicolon = ";";

        private static readonly string[] Separators = [ Comma, Tab, Pipe, Semicolon ];

        private static DelimitedSchema Schema()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new Int32Column( "Id" ) );
            schema.AddColumn( new StringColumn( "Name" ) );
            schema.AddColumn( new DecimalColumn( "Amount" ) );
            schema.AddColumn( new BooleanColumn( "Active" ) );
            return schema;
        }

        private static DelimitedOptions Options( string separator )
        {
            return new DelimitedOptions { Separator = separator, IsFirstRecordSchema = true, RecordSeparator = "\r\n" };
        }

        private static string Write( string separator, params object[][] records )
        {
            var output = new StringWriter();
            var writer = new DelimitedWriter( output, Schema(), Options( separator ) );
            foreach (var record in records)
            {
                writer.Write( record );
            }
            return output.ToString();
        }

        private static List<object[]> Read( string separator, string text )
        {
            var reader = new DelimitedReader( new StringReader( text ), Schema(), Options( separator ) );
            List<object[]> records = [];
            while (reader.Read())
            {
                records.Add( reader.GetValues() );
            }
            return records;
        }

        [TestMethod]
        public void TestRoundTrip_EveryFormat_ReadsBackWhatWasWritten()
        {
            object[] first = [ 1, "Bob", 12.5m, true ];
            object[] second = [ 2, "Jane", 0m, false ];
            foreach (var separator in Separators)
            {
                var text = Write( separator, first, second );

                var records = Read( separator, text );

                Assert.AreEqual( 2, records.Count, $"Separator {separator}" );
                CollectionAssert.AreEqual( first, records[0], $"Separator {separator}" );
                CollectionAssert.AreEqual( second, records[1], $"Separator {separator}" );
            }
        }

        [TestMethod]
        public void TestWrite_HeaderAndValues_UseTheConfiguredSeparator()
        {
            Assert.AreEqual( "Id,Name,Amount,Active\r\n1,Bob,12.5,True\r\n", Write( Comma, [ 1, "Bob", 12.5m, true ] ) );
            Assert.AreEqual( "Id\tName\tAmount\tActive\r\n1\tBob\t12.5\tTrue\r\n", Write( Tab, [ 1, "Bob", 12.5m, true ] ) );
            Assert.AreEqual( "Id|Name|Amount|Active\r\n1|Bob|12.5|True\r\n", Write( Pipe, [ 1, "Bob", 12.5m, true ] ) );
            Assert.AreEqual( "Id;Name;Amount;Active\r\n1;Bob;12.5;True\r\n", Write( Semicolon, [ 1, "Bob", 12.5m, true ] ) );
        }

        [TestMethod]
        public void TestWrite_ValueContainingTheFileSeparator_IsQuoted()
        {
            Assert.AreEqual( "Id,Name,Amount,Active\r\n1,\"Smith, Bob\",1,True\r\n", Write( Comma, [ 1, "Smith, Bob", 1m, true ] ) );
            Assert.AreEqual( "Id\tName\tAmount\tActive\r\n1\t\"Smith\tBob\"\t1\tTrue\r\n", Write( Tab, [ 1, "Smith\tBob", 1m, true ] ) );
            Assert.AreEqual( "Id|Name|Amount|Active\r\n1|\"Smith|Bob\"|1|True\r\n", Write( Pipe, [ 1, "Smith|Bob", 1m, true ] ) );
            Assert.AreEqual( "Id;Name;Amount;Active\r\n1;\"Smith;Bob\";1;True\r\n", Write( Semicolon, [ 1, "Smith;Bob", 1m, true ] ) );
        }

        [TestMethod]
        public void TestWrite_ValueContainingAnotherFormatsSeparator_IsNotQuoted()
        {
            // A comma is just a character in a tab, pipe or semicolon separated file, and a tab or pipe is just a character in a CSV.
            Assert.AreEqual( "Id\tName\tAmount\tActive\r\n1\tSmith, Bob\t1\tTrue\r\n", Write( Tab, [ 1, "Smith, Bob", 1m, true ] ) );
            Assert.AreEqual( "Id|Name|Amount|Active\r\n1|Smith, Bob|1|True\r\n", Write( Pipe, [ 1, "Smith, Bob", 1m, true ] ) );
            Assert.AreEqual( "Id;Name;Amount;Active\r\n1;Smith, Bob;1;True\r\n", Write( Semicolon, [ 1, "Smith, Bob", 1m, true ] ) );
            Assert.AreEqual( "Id,Name,Amount,Active\r\n1,Smith|Bob,1,True\r\n", Write( Comma, [ 1, "Smith|Bob", 1m, true ] ) );
        }

        [TestMethod]
        public void TestRead_QuotedValueContainingTheSeparator_IsOneValue()
        {
            foreach (var separator in Separators)
            {
                var text = $"Id{separator}Name{separator}Amount{separator}Active\r\n1{separator}\"Smith{separator} Bob\"{separator}1{separator}True\r\n";

                var records = Read( separator, text );

                Assert.AreEqual( 1, records.Count, $"Separator {separator}" );
                Assert.AreEqual( $"Smith{separator} Bob", records[0][1], $"Separator {separator}" );
            }
        }

        [TestMethod]
        public void TestRead_EmptyValues_AreNullsInEveryFormat()
        {
            foreach (var separator in Separators)
            {
                var text = $"Id{separator}Name{separator}Amount{separator}Active\r\n1{separator}{separator}{separator}\r\n";

                var records = Read( separator, text );

                Assert.AreEqual( 1, records[0][0], $"Separator {separator}" );
                Assert.IsNull( records[0][1], $"Separator {separator}: empty string column" );
                Assert.IsNull( records[0][2], $"Separator {separator}: empty decimal column" );
                Assert.IsNull( records[0][3], $"Separator {separator}: empty boolean column" );
            }
        }

        [TestMethod]
        public void TestSemicolon_WithACommaDecimalCulture_NeedsNoQuoting()
        {
            // The usual reason for a semicolon file: the values themselves contain commas as decimal separators.
            var german = CultureInfo.GetCultureInfo( "de-DE" );
            var options = new DelimitedOptions { Separator = Semicolon, RecordSeparator = "\r\n", FormatProvider = german };
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "Name" ) );
            schema.AddColumn( new DecimalColumn( "Amount" ) );
            var output = new StringWriter();
            new DelimitedWriter( output, schema, options ).Write( [ "Bob", 1234.5m ] );

            Assert.AreEqual( "Bob;1234,5\r\n", output.ToString() );
            var reader = new DelimitedReader( new StringReader( output.ToString() ), schema, options );
            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( 1234.5m, reader.GetValues()[1] );
        }

        [TestMethod]
        public void TestTypeMapper_EveryFormat_RoundTripsEntities()
        {
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( p => p.Id ).ColumnName( "Id" );
            mapper.Property( p => p.Name ).ColumnName( "Name" );
            mapper.Property( p => p.Amount ).ColumnName( "Amount" );
            mapper.Property( p => p.Active ).ColumnName( "Active" );
            Person[] people =
            [
                new Person { Id = 1, Name = "Smith, Bob", Amount = 12.5m, Active = true },
                new Person { Id = 2, Name = "Tab\there", Amount = null, Active = false },
                new Person { Id = 3, Name = "Pipe|here; semi", Amount = -3m, Active = true }
            ];
            foreach (var separator in Separators)
            {
                var options = Options( separator );
                var output = new StringWriter();
                mapper.Write( output, people, options );

                Person[] read = [.. mapper.Read( new StringReader( output.ToString() ), options )];

                Assert.AreEqual( people.Length, read.Length, $"Separator {separator}" );
                for (int index = 0; index != people.Length; ++index)
                {
                    Assert.AreEqual( people[index].Id, read[index].Id, $"Separator {separator}" );
                    Assert.AreEqual( people[index].Name, read[index].Name, $"Separator {separator}" );
                    Assert.AreEqual( people[index].Amount, read[index].Amount, $"Separator {separator}" );
                    Assert.AreEqual( people[index].Active, read[index].Active, $"Separator {separator}" );
                }
            }
        }

        [TestMethod]
        public void TestRead_FileWrittenWithOneSeparatorReadWithAnother_IsOneColumnPerLine()
        {
            var csv = Write( Comma, [ 1, "Bob", 12.5m, true ] );
            var reader = new DelimitedReader( new StringReader( csv ), new DelimitedOptions { Separator = Tab, IsFirstRecordSchema = true } );

            Assert.IsTrue( reader.Read() );

            Assert.AreEqual( 1, reader.GetValues().Length, "With no tabs in the text, each line is a single value." );
            Assert.AreEqual( "1,Bob,12.5,True", reader.GetValues()[0] );
        }

        public sealed class Person
        {
            public int Id { get; set; }

            public string Name { get; set; }

            public decimal? Amount { get; set; }

            public bool Active { get; set; }
        }
    }
}
