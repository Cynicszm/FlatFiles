using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    [TestClass]
    public class DelimitedWriterTester
    {
        [TestMethod]
        public void ShouldNotWriteSchemaIfNoSchemaProvided()
        {
            var stringWriter = new StringWriter();
            var writer = new DelimitedWriter( stringWriter, new DelimitedOptions { IsFirstRecordSchema = true } );
            writer.Write( [ "a" ] );

            var output = stringWriter.ToString();
            var expected = "a" + Environment.NewLine;
            Assert.AreEqual( expected, output );
        }

        [TestMethod]
        public void ShouldWriteSchemaIfExplicit()
        {
            var stringWriter = new StringWriter();
            // Explicitly indicate that the first record is NOT the schema
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "Col1" ) );
            var writer = new DelimitedWriter( stringWriter, schema, new DelimitedOptions
            {
                IsFirstRecordSchema = false
            } );
            writer.WriteSchema();  // Explicitly write the schema
            writer.Write( [ "a" ] );

            var stringReader = new StringReader( stringWriter.ToString() );
            var reader = new DelimitedReader( stringReader, new DelimitedOptions { IsFirstRecordSchema = true } );
            var parsedSchema = reader.GetSchema();
            Assert.AreEqual( schema.ColumnDefinitions.Count, parsedSchema!.ColumnDefinitions.Count );
            Assert.AreEqual( schema.ColumnDefinitions[0].ColumnName, parsedSchema.ColumnDefinitions[0].ColumnName );

            Assert.IsTrue( reader.Read(), "The record was not retrieved after the schema." );
            Assert.IsFalse( reader.Read(), "Encountered more than the expected number of records." );
        }

        [TestMethod]
        public void ShouldNotWriteSchemaAfterFirstRecordWritten()
        {
            var stringWriter = new StringWriter();
            // Explicitly indicate that the first record is NOT the schema
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "Col1" ) );
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = false
            };
            var writer = new DelimitedWriter( stringWriter, schema, options );
            writer.Write( [ "a" ] );
            writer.WriteSchema();  // Explicitly write the schema

            var stringReader = new StringReader( stringWriter.ToString() );
            var reader = new DelimitedReader( stringReader, schema, options );
            Assert.IsTrue( reader.Read(), "The record was not retrieved." );
            Assert.IsFalse( reader.Read(), "Encountered more than the expected number of records." );
        }
    }
}
