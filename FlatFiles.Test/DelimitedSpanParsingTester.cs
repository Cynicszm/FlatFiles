using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests that a delimited record's values are copied into one string and parsed from it, rather than copied
    ///     into a string each, and that nothing observable changes because of it: every column type reads the same
    ///     value, quoting and escaping are unchanged, a schema selector and a record read handler still receive the
    ///     values as an array, and a record context still carries them after the reader has moved on.
    /// </summary>
    [TestClass]
    public class DelimitedSpanParsingTester
    {
        private const string EveryTypeRecord =
            "A001,142,1234.56,True,B,2026-09-22,2026-09-23,13:45:56,"
            + "0f8fad5b-d9cb-469f-a165-70867728950e,01:02:03,Green,AB,cd\r\n";

        private static DelimitedSchema EveryTypeSchema()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "Id" ) );
            schema.AddColumn( new Int32Column( "Count" ) );
            schema.AddColumn( new DecimalColumn( "Amount" ) );
            schema.AddColumn( new BooleanColumn( "Flag" ) );
            schema.AddColumn( new CharColumn( "Grade" ) );
            schema.AddColumn( new DateTimeColumn( "When" ) );
            schema.AddColumn( new DateOnlyColumn( "Day" ) );
            schema.AddColumn( new TimeOnlyColumn( "Time" ) );
            schema.AddColumn( new GuidColumn( "Key" ) );
            schema.AddColumn( new TimeSpanColumn( "Span" ) );
            schema.AddColumn( new EnumColumn<Colour>( "Colour" ) );
            schema.AddColumn( new ByteArrayColumn( "Bytes" ) );
            schema.AddColumn( new CharArrayColumn( "Chars" ) );
            return schema;
        }

        [TestMethod]
        public void TestRead_EveryColumnType_ParsesTheSameValues()
        {
            var reader = new DelimitedReader( new StringReader( EveryTypeRecord ), EveryTypeSchema() );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();

            Assert.AreEqual( "A001", values[0] );
            Assert.AreEqual( 142, values[1] );
            Assert.AreEqual( 1234.56m, values[2] );
            Assert.AreEqual( true, values[3] );
            Assert.AreEqual( 'B', values[4] );
            Assert.AreEqual( new DateTime( 2026, 9, 22 ), values[5] );
            Assert.AreEqual( new DateOnly( 2026, 9, 23 ), values[6] );
            Assert.AreEqual( new TimeOnly( 13, 45, 56 ), values[7] );
            Assert.AreEqual( Guid.Parse( "0f8fad5b-d9cb-469f-a165-70867728950e" ), values[8] );
            Assert.AreEqual( new TimeSpan( 1, 2, 3 ), values[9] );
            Assert.AreEqual( Colour.Green, values[10] );
            Assert.AreEqual( "AB", Encoding.UTF8.GetString( (byte[]) values[11]! ) );
            CollectionAssert.AreEqual( new[] { 'c', 'd' }, (char[]) values[12]! );
        }

        [TestMethod]
        public void TestRead_ColumnThatParsesASpan_ReceivesTheCharactersOfTheRecord()
        {
            var column = new SpanCountingColumn( "a" );
            var schema = new DelimitedSchema();
            schema.AddColumn( column );
            var reader = new DelimitedReader( new StringReader( "abcd\r\n" ), schema );

            Assert.IsTrue( reader.Read() );

            Assert.AreEqual( "abcd", reader.GetValues()[0] );
            Assert.AreEqual( 1, column.SpanCalls );
            Assert.AreEqual( 0, column.StringCalls );
        }

        [TestMethod]
        public void TestRead_QuotedValues_AreUnescapedAsBefore()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            schema.AddColumn( new StringColumn( "b" ) );
            schema.AddColumn( new StringColumn( "c" ) );
            var reader = new DelimitedReader( new StringReader( "\"one, two\",\"say \"\"hi\"\"\",plain\r\n" ), schema );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();

            Assert.AreEqual( "one, two", values[0] );
            Assert.AreEqual( "say \"hi\"", values[1] );
            Assert.AreEqual( "plain", values[2] );
        }

        [TestMethod]
        public void TestRead_QuotedValuesAcrossRecords_StayApart()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            schema.AddColumn( new StringColumn( "b" ) );
            var reader = new DelimitedReader( new StringReader( "\"one\",\"two\"\r\n\"three\",\"four\"\r\n" ), schema );

            Assert.IsTrue( reader.Read() );
            CollectionAssert.AreEqual( new object[] { "one", "two" }, reader.GetValues() );
            Assert.IsTrue( reader.Read() );
            CollectionAssert.AreEqual( new object[] { "three", "four" }, reader.GetValues() );
            Assert.IsFalse( reader.Read() );
        }

        [TestMethod]
        public void TestRead_SeveralValuesWithDoubledQuotes_StayApart()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            schema.AddColumn( new StringColumn( "b" ) );
            schema.AddColumn( new StringColumn( "c" ) );
            var reader = new DelimitedReader( new StringReader( "\"a\"\"b\",plain,\"c\"\"d\"\"e\"\r\n" ), schema );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();

            Assert.AreEqual( "a\"b", values[0] );
            Assert.AreEqual( "plain", values[1], "A value the record can hold is still read straight from it." );
            Assert.AreEqual( "c\"d\"e", values[2] );
        }

        [TestMethod]
        public void TestRead_RebuiltValuesAsStrings_AreStillTakenFromTheRightPlace()
        {
            string[] seen = null!;
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            schema.AddColumn( new StringColumn( "b" ) );
            schema.AddColumn( new StringColumn( "c" ) );
            var reader = new DelimitedReader( new StringReader( "plain,\"a\"\"b\",last\r\n" ), schema );
            // A handler for the record read is given every value as a string, whichever text it was read from.
            reader.RecordRead += ( _, e ) => seen = e.Values;

            Assert.IsTrue( reader.Read() );

            CollectionAssert.AreEqual( new[] { "plain", "a\"b", "last" }, seen );
        }

        [TestMethod]
        public void TestRead_QuotedValueWithPreservedTrailingWhiteSpace_KeepsIt()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) { Trim = false } );
            schema.AddColumn( new StringColumn( "b" ) { Trim = false } );
            var reader = new DelimitedReader( new StringReader( "\"abc\"  ,d\r\n" ), schema, new DelimitedOptions { PreserveWhiteSpace = true } );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();

            Assert.AreEqual( "abc  ", values[0], "The whitespace is not next to the value in the record, so the value is rebuilt." );
            Assert.AreEqual( "d", values[1] );
        }

        [TestMethod]
        public void TestRead_QuotedValueWithDiscardedTrailingWhiteSpace_DropsIt()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) { Trim = false } );
            schema.AddColumn( new StringColumn( "b" ) { Trim = false } );
            var reader = new DelimitedReader( new StringReader( "\"abc\"  ,d\r\n" ), schema );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();

            Assert.AreEqual( "abc", values[0] );
            Assert.AreEqual( "d", values[1] );
        }

        [TestMethod]
        public void TestRead_RebuiltValueReadDuringItsRecord_IsKeptAfterwards()
        {
            List<IRecordContext> contexts = [];
            List<string[]> readWhileCurrent = [];
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            schema.AddColumn( new StringColumn( "b" ) );
            var reader = new DelimitedReader( new StringReader( "\"a\"\"b\",one\r\n\"c\"\"d\",two\r\n" ), schema );
            reader.RecordParsed += ( _, e ) =>
            {
                contexts.Add( e.RecordContext );
                readWhileCurrent.Add( e.RecordContext.Values! );
            };

            while (reader.Read())
            {
            }

            // Asked for while its record was current, a rebuilt value is copied out and the copy is the context's
            // to keep, whatever the parser does with its buffers afterwards.
            CollectionAssert.AreEqual( new[] { "a\"b", "one" }, readWhileCurrent[0] );
            CollectionAssert.AreEqual( new[] { "c\"d", "two" }, readWhileCurrent[1] );
            CollectionAssert.AreEqual( new[] { "a\"b", "one" }, contexts[0].Values );
            CollectionAssert.AreEqual( new[] { "c\"d", "two" }, contexts[1].Values );
        }

        [TestMethod]
        public void TestRead_EmptyValues_StayEmpty()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            schema.AddColumn( new StringColumn( "b" ) );
            schema.AddColumn( new StringColumn( "c" ) );
            var reader = new DelimitedReader( new StringReader( ",b,\r\n" ), schema );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();

            Assert.IsNull( values[0] );
            Assert.AreEqual( "b", values[1] );
            Assert.IsNull( values[2] );
        }

        [TestMethod]
        public void TestRead_PreservedWhiteSpace_IsKeptWithTheValue()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) { Trim = false } );
            schema.AddColumn( new StringColumn( "b" ) { Trim = false } );
            var reader = new DelimitedReader( new StringReader( "  a  ,  b  \r\n" ), schema, new DelimitedOptions { PreserveWhiteSpace = true } );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();

            Assert.AreEqual( "  a  ", values[0] );
            Assert.AreEqual( "  b  ", values[1] );
        }

        [TestMethod]
        public void TestRead_PreservedRecordText_IsTheRecordNotTheValues()
        {
            string seen = null!;
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            schema.AddColumn( new StringColumn( "b" ) { OnParsed = ( ctx, v ) => { seen = ctx!.RecordContext.Record!; return v; } } );
            var reader = new DelimitedReader( new StringReader( "\"one, two\",three\r\n" ), schema, new DelimitedOptions { PreserveRecordText = true } );

            Assert.IsTrue( reader.Read() );

            Assert.AreEqual( "\"one, two\",three", seen, "The record keeps its own text, quotes and all." );
        }

        [TestMethod]
        public void TestRead_HookAsksForTheRawValues_TheyAreCopiedOutOfTheRecord()
        {
            string[] seen = null!;
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            schema.AddColumn( new Int32Column( "b" ) { OnParsed = ( ctx, v ) => { seen = ctx!.RecordContext.Values!; return v; } } );
            var reader = new DelimitedReader( new StringReader( "one,42\r\n" ), schema );

            Assert.IsTrue( reader.Read() );

            CollectionAssert.AreEqual( new[] { "one", "42" }, seen );
        }

        [TestMethod]
        public void TestRead_ContextKeptPastItsRecord_ReportsNoValues()
        {
            List<IRecordContext> contexts = [];
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            schema.AddColumn( new StringColumn( "b" ) );
            var reader = new DelimitedReader( new StringReader( "one,two\r\nthree,four\r\nfive,six\r\n" ), schema );
            reader.RecordParsed += ( _, e ) => contexts.Add( e.RecordContext );

            while (reader.Read())
            {
            }

            // Nothing asked for the values while each record was current, and the characters they would have been
            // copied from are long gone, so the contexts say so rather than inventing an answer.
            Assert.HasCount( 3, contexts );
            Assert.IsNull( contexts[0].Values );
            Assert.IsNull( contexts[1].Values );
            Assert.IsNull( contexts[2].Values );
        }

        [TestMethod]
        public void TestRead_ContextReadWhileItsRecordIsCurrent_KeepsWhatItWasGiven()
        {
            List<IRecordContext> contexts = [];
            List<string[]> readWhileCurrent = [];
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            schema.AddColumn( new StringColumn( "b" ) );
            var reader = new DelimitedReader( new StringReader( "one,two\r\nthree,four\r\n" ), schema );
            reader.RecordParsed += ( _, e ) =>
            {
                contexts.Add( e.RecordContext );
                readWhileCurrent.Add( e.RecordContext.Values! );
            };

            while (reader.Read())
            {
            }

            CollectionAssert.AreEqual( new[] { "one", "two" }, readWhileCurrent[0] );
            CollectionAssert.AreEqual( new[] { "three", "four" }, readWhileCurrent[1] );
            // Once copied out, the array belongs to the context and outlives the record it came from.
            CollectionAssert.AreEqual( new[] { "one", "two" }, contexts[0].Values );
            CollectionAssert.AreEqual( new[] { "three", "four" }, contexts[1].Values );
        }

        [TestMethod]
        public void TestRead_HookWritesToTheRawValues_TheWriteDoesNotReachTheNextColumn()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) { OnParsed = ( context, v ) => { context!.RecordContext!.Values![1] = "99"; return v; } } );
            schema.AddColumn( new Int32Column( "b" ) );
            var reader = new DelimitedReader( new StringReader( "x,7\r\n" ), schema );

            Assert.IsTrue( reader.Read() );

            Assert.AreEqual( 7, reader.GetValues()[1],
                "The array a hook reads is a copy of the record's values, not the one the columns are parsed from. A record read handler is what replaces a value before parsing." );
        }

        [TestMethod]
        public void TestRead_RecordReadHandlerReplacesAValue_TheReplacementIsParsed()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            schema.AddColumn( new Int32Column( "b" ) );
            var reader = new DelimitedReader( new StringReader( "one,7\r\n" ), schema );
            reader.RecordRead += ( _, e ) => e.Values[1] = "42";

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();

            Assert.AreEqual( "one", values[0] );
            Assert.AreEqual( 42, values[1] );
        }

        [TestMethod]
        public void TestRead_RecordReadHandlerSkips_TheRecordIsSkipped()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            var reader = new DelimitedReader( new StringReader( "skip\r\nkeep\r\n" ), schema );
            reader.RecordRead += ( _, e ) => e.IsSkipped = e.Values[0] == "skip";

            Assert.IsTrue( reader.Read() );

            Assert.AreEqual( "keep", reader.GetValues()[0] );
            Assert.IsFalse( reader.Read() );
        }

        [TestMethod]
        public void TestRead_SchemaSelector_IsGivenTheValuesAsStrings()
        {
            var first = new DelimitedSchema();
            first.AddColumn( new StringColumn( "type" ) );
            first.AddColumn( new Int32Column( "count" ) );
            var second = new DelimitedSchema();
            second.AddColumn( new StringColumn( "type" ) );
            second.AddColumn( new StringColumn( "name" ) );
            var selector = new DelimitedSchemaSelector();
            selector.When( v => v[0] == "A" ).Use( first );
            selector.When( v => v[0] == "B" ).Use( second );
            var reader = new DelimitedReader( new StringReader( "A,42\r\nB,hello\r\n" ), selector );

            Assert.IsTrue( reader.Read() );
            CollectionAssert.AreEqual( new object[] { "A", 42 }, reader.GetValues() );
            Assert.IsTrue( reader.Read() );
            CollectionAssert.AreEqual( new object[] { "B", "hello" }, reader.GetValues() );
        }

        [TestMethod]
        public void TestRead_HeaderRow_StillNamesTheColumns()
        {
            var reader = new DelimitedReader( new StringReader( "one,two\r\na,b\r\n" ), new DelimitedOptions { IsFirstRecordSchema = true } );

            var schema = reader.GetSchema();

            Assert.IsNotNull( schema );
            Assert.AreEqual( "one", schema.ColumnDefinitions[0].ColumnName );
            Assert.AreEqual( "two", schema.ColumnDefinitions[1].ColumnName );
            Assert.IsTrue( reader.Read() );
            CollectionAssert.AreEqual( new object[] { "a", "b" }, reader.GetValues() );
        }

        [TestMethod]
        public void TestRead_ParseError_CarriesTheValueAndTheWholeRecord()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            schema.AddColumn( new Int32Column( "b" ) );
            var reader = new DelimitedReader( new StringReader( "one,nope\r\n" ), schema );
            ColumnProcessingException captured = null!;
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
            Assert.AreEqual( "nope", captured.ColumnValue );
            CollectionAssert.AreEqual( new[] { "one", "nope" }, captured!.ColumnContext!.RecordContext.Values );
        }

        [TestMethod]
        public void TestRead_TooFewColumns_IsStillReported()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            schema.AddColumn( new StringColumn( "b" ) );
            var reader = new DelimitedReader( new StringReader( "only\r\n" ), schema );

            Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );
        }

        [TestMethod]
        public void TestRead_MetadataColumn_StillSeesTheRecordNumber()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new RecordNumberColumn( "n" ) { IncludeSkippedRecords = false } );
            schema.AddColumn( new StringColumn( "a" ) );
            var reader = new DelimitedReader( new StringReader( "one\r\ntwo\r\n" ), schema );

            Assert.IsTrue( reader.Read() );
            CollectionAssert.AreEqual( new object[] { 1, "one" }, reader.GetValues() );
            Assert.IsTrue( reader.Read() );
            CollectionAssert.AreEqual( new object[] { 2, "two" }, reader.GetValues() );
        }

        [TestMethod]
        public void TestRead_NullFormatterForAValue_ReadsItAsNull()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new Int32Column( "a" ) { NullFormatter = NullFormatter.ForValue( "NULL" ) } );
            schema.AddColumn( new Int32Column( "b" ) { NullFormatter = NullFormatter.ForValue( "NULL" ) } );
            var reader = new DelimitedReader( new StringReader( "NULL,7\r\n" ), schema );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();

            Assert.IsNull( values[0] );
            Assert.AreEqual( 7, values[1] );
        }

        [TestMethod]
        public void TestRead_ParsingHooks_StillReceiveTheValueAsAString()
        {
            string parsing = null!;
            var schema = new DelimitedSchema();
            schema.AddColumn( new Int32Column( "a" ) { OnParsing = ( _, v ) => { parsing = v; return v; } } );
            var reader = new DelimitedReader( new StringReader( "42\r\n" ), schema );

            Assert.IsTrue( reader.Read() );

            Assert.AreEqual( "42", parsing );
            Assert.AreEqual( 42, reader.GetValues()[0] );
        }

        [TestMethod]
        public void TestRead_FormatProviderOnTheOptions_StillReachesTheColumn()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new DecimalColumn( "a" ) );
            var reader = new DelimitedReader( new StringReader( "1234,56\r\n" ), schema, new DelimitedOptions
            {
                Separator = ";",
                FormatProvider = CultureInfo.GetCultureInfo( "de-DE" )
            } );

            Assert.IsTrue( reader.Read() );

            Assert.AreEqual( 1234.56m, reader.GetValues()[0] );
        }

        private enum Colour
        {
            Red = 1,
            Green = 2
        }

        /// <summary>
        ///     A column from outside the library that reads its value from the characters of the record.
        /// </summary>
        private sealed class SpanCountingColumn( string columnName ) : ColumnDefinition<string>( columnName )
        {
            public int SpanCalls { get; private set; }

            public int StringCalls { get; private set; }

            protected override string OnParse( IColumnContext? context, string value )
            {
                ++StringCalls;
                return value;
            }

            protected override string OnParse( IColumnContext? context, ReadOnlySpan<char> value )
            {
                ++SpanCalls;
                return value.ToString();
            }

            protected override string OnFormat( IColumnContext? context, string value )
            {
                return value;
            }
        }
    }
}
