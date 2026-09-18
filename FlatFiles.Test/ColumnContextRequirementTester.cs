using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests that a column is given a context only when something attached to it can look at one, and that nothing
    ///     observable changes when it is not: hooks still see a context, the options' format provider still reaches the
    ///     column, and a column error still carries the column's indices.
    /// </summary>
    [TestClass]
    public class ColumnContextRequirementTester
    {
        [TestMethod]
        public void TestIsColumnContextRequired_PlainLibraryColumns_False()
        {
            Assert.IsFalse( new Int32Column( "a" ).IsColumnContextRequired );
            Assert.IsFalse( new StringColumn( "a" ).IsColumnContextRequired );
            Assert.IsFalse( new DateTimeColumn( "a" ) { InputFormat = "yyyyMMdd" }.IsColumnContextRequired );
            Assert.IsFalse( new Int32Column( "a" ) { NullFormatter = NullFormatter.ForValue( "NULL" ) }.IsColumnContextRequired, "The library's null formatters never read the context." );
            Assert.IsFalse( new Int32Column( "a" ) { IsNullable = false, DefaultValue = DefaultValue.Use( 0 ) }.IsColumnContextRequired, "A fixed default value never reads the context." );
        }

        [TestMethod]
        public void TestIsColumnContextRequired_WhenSomethingCanLookAtTheContext_True()
        {
            Assert.IsTrue( new Int32Column( "a" ) { OnParsing = ( _, v ) => v }.IsColumnContextRequired );
            Assert.IsTrue( new Int32Column( "a" ) { OnParsed = ( _, v ) => v }.IsColumnContextRequired );
            Assert.IsTrue( new Int32Column( "a" ) { OnFormatting = ( _, v ) => v }.IsColumnContextRequired );
            Assert.IsTrue( new Int32Column( "a" ) { OnFormatted = ( _, v ) => v }.IsColumnContextRequired );
            Assert.IsTrue( new Int32Column( "a" ) { NullFormatter = new CountingNullFormatter() }.IsColumnContextRequired, "A null formatter from outside the library may read the context." );
            Assert.IsTrue( new Int32Column( "a" ) { DefaultValue = DefaultValue.Use( ctx => ctx?.PhysicalIndex ) }.IsColumnContextRequired, "A default value built from a delegate may read the context." );
            Assert.IsTrue( new ContextCapturingColumn( "a" ).IsColumnContextRequired, "A subclass declared outside the library may read the context." );
            Assert.IsTrue( new RecordNumberColumn( "a" ).IsColumnContextRequired, "A metadata column reads the record from the context." );
        }

        [TestMethod]
        public void TestRead_Hook_ReceivesAContextWithTheColumnsIndices()
        {
            IColumnContext seen = null;
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            schema.AddColumn( new Int32Column( "b" ) { OnParsed = ( ctx, v ) => { seen = ctx; return v; } } );
            var reader = new DelimitedReader( new StringReader( "x,5\r\n" ), schema );

            Assert.IsTrue( reader.Read() );

            Assert.IsNotNull( seen );
            Assert.AreEqual( 1, seen.PhysicalIndex );
            Assert.AreEqual( 1, seen.LogicalIndex );
            Assert.AreEqual( 1, seen.RecordContext.PhysicalRecordNumber );
        }

        [TestMethod]
        public void TestRead_SubclassFromOutsideTheLibrary_ReceivesAContext()
        {
            var column = new ContextCapturingColumn( "a" );
            var schema = new DelimitedSchema();
            schema.AddColumn( column );
            var reader = new DelimitedReader( new StringReader( "hello\r\n" ), schema );

            Assert.IsTrue( reader.Read() );

            Assert.IsNotNull( column.Seen );
            Assert.AreEqual( 0, column.Seen.PhysicalIndex );
        }

        [TestMethod]
        public void TestRead_PlainColumnParseError_ColumnErrorHandlerStillReceivesTheContext()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            schema.AddColumn( new Int32Column( "b" ) );
            var reader = new DelimitedReader( new StringReader( "x,notanumber\r\n" ), schema );
            ColumnProcessingException captured = null;
            reader.ColumnError += ( _, e ) =>
            {
                captured = (ColumnProcessingException) e.Exception;
                e.IsHandled = true;
                e.Substitution = -1;
            };

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();

            Assert.AreEqual( -1, values[1] );
            Assert.IsNotNull( captured );
            Assert.AreEqual( "notanumber", captured.ColumnValue );
            Assert.AreEqual( 1, captured.ColumnContext.PhysicalIndex );
            Assert.AreEqual( 1, captured.ColumnContext.LogicalIndex );
            Assert.AreEqual( "b", captured.ColumnContext.ColumnDefinition.ColumnName );
            Assert.AreEqual( 1, captured.ColumnContext.RecordContext.PhysicalRecordNumber );
        }

        [TestMethod]
        public void TestRead_PlainColumnParseErrorWithoutAHandler_ThrowsWithTheContext()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new Int32Column( "a" ) );
            var reader = new DelimitedReader( new StringReader( "notanumber\r\n" ), schema );

            var exception = Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );

            var columnException = (ColumnProcessingException) exception.InnerException;
            Assert.IsNotNull( columnException?.ColumnContext );
            Assert.AreEqual( 0, columnException.ColumnContext.PhysicalIndex );
        }

        [TestMethod]
        public void TestRead_OptionsFormatProvider_StillReachesAPlainColumn()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new DecimalColumn( "a" ) );
            var options = new DelimitedOptions { Separator = ";", FormatProvider = CultureInfo.GetCultureInfo( "de-DE" ) };
            var reader = new DelimitedReader( new StringReader( "1.234,5\r\n" ), schema, options );

            Assert.IsTrue( reader.Read() );

            Assert.AreEqual( 1234.5m, reader.GetValues()[0] );
        }

        [TestMethod]
        public void TestWrite_OptionsFormatProvider_StillReachesAPlainColumn()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new DecimalColumn( "a" ) );
            var options = new DelimitedOptions { Separator = ";", FormatProvider = CultureInfo.GetCultureInfo( "de-DE" ), RecordSeparator = "\n" };
            var stringWriter = new StringWriter();
            var writer = new DelimitedWriter( stringWriter, schema, options );

            writer.Write( [ 1234.5m ] );

            Assert.AreEqual( "1234,5\n", stringWriter.ToString() );
        }

        [TestMethod]
        public void TestWrite_PlainColumnFormatError_ColumnErrorHandlerStillReceivesTheContext()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            schema.AddColumn( new Int32Column( "b" ) );
            var stringWriter = new StringWriter();
            var writer = new DelimitedWriter( stringWriter, schema, new DelimitedOptions { RecordSeparator = "\n" } );
            ColumnProcessingException captured = null;
            writer.ColumnError += ( _, e ) =>
            {
                captured = (ColumnProcessingException) e.Exception;
                e.IsHandled = true;
                e.Substitution = "?";
            };

            writer.Write( [ "x", "not an int" ] );

            Assert.AreEqual( "x,?\n", stringWriter.ToString() );
            Assert.IsNotNull( captured );
            Assert.AreEqual( 1, captured.ColumnContext.PhysicalIndex );
            Assert.AreEqual( "b", captured.ColumnContext.ColumnDefinition.ColumnName );
        }

        [TestMethod]
        public void TestWrite_Hook_ReceivesAContext()
        {
            IColumnContext seen = null;
            var schema = new DelimitedSchema();
            schema.AddColumn( new Int32Column( "a" ) { OnFormatted = ( ctx, v ) => { seen = ctx; return v; } } );
            var stringWriter = new StringWriter();
            var writer = new DelimitedWriter( stringWriter, schema, new DelimitedOptions { RecordSeparator = "\n" } );

            writer.Write( [ 5 ] );

            Assert.AreEqual( "5\n", stringWriter.ToString() );
            Assert.IsNotNull( seen );
            Assert.AreEqual( 0, seen.PhysicalIndex );
        }

        [TestMethod]
        public void TestRead_ContextDisabled_StillOverridesEverything()
        {
            IColumnContext seen = new ColumnContextStub();
            var schema = new DelimitedSchema();
            schema.AddColumn( new Int32Column( "a" ) { OnParsed = ( ctx, v ) => { seen = ctx; return v; } } );
            var reader = new DelimitedReader( new StringReader( "5\r\n" ), schema, new DelimitedOptions { IsColumnContextDisabled = true } );

            Assert.IsTrue( reader.Read() );

            Assert.IsNull( seen, "With the option set, even a hook is given no context, as before." );
        }

        /// <summary>
        ///     A column from outside the library, which may read the context in its own parsing.
        /// </summary>
        private sealed class ContextCapturingColumn( string columnName ) : ColumnDefinition<string>( columnName )
        {
            public IColumnContext Seen { get; private set; }

            protected override string OnParse( IColumnContext context, string value )
            {
                Seen = context;
                return value;
            }

            protected override string OnFormat( IColumnContext context, string value )
            {
                return value;
            }
        }

        private sealed class CountingNullFormatter : INullFormatter
        {
            public bool IsNullValue( IColumnContext context, string value ) => string.IsNullOrEmpty( value );

            public string FormatNull( IColumnContext context ) => string.Empty;
        }

        private sealed class ColumnContextStub : IColumnContext
        {
            public IRecordContext RecordContext => throw new NotSupportedException();

            public IColumnDefinition ColumnDefinition => throw new NotSupportedException();

            public int PhysicalIndex => 0;

            public int LogicalIndex => 0;
        }
    }
}
