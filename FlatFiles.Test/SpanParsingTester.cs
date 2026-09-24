using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests that a fixed-length record is parsed from the characters of the record itself, without a string per
    ///     value, and that nothing observable changes because of it: every column type reads the same value, the hooks
    ///     and the obsolete preprocessor still receive a string, a column written against an earlier version still
    ///     sees every value through whichever overload it implemented, and the raw values are still there for a
    ///     handler or an error that asks for them. <see cref="DelimitedSpanParsingTester" /> covers the same ground
    ///     for a delimited record.
    /// </summary>
    [TestClass]
    public class SpanParsingTester
    {
        /// <summary>
        ///     One record of every column type, each value padded to its window.
        /// </summary>
        private const string EveryTypeRecord =
            "A001" +                                 // Id, 4
            "142   " +                               // Count, 6
            "1234.56   " +                           // Amount, 10
            "True " +                                // Flag, 5
            "B" +                                    // Grade, 1
            "2026-09-22" +                           // When, 10
            "2026-09-23" +                           // Day, 10
            "13:45:56" +                             // Time, 8
            "0f8fad5b-d9cb-469f-a165-70867728950e" + // Key, 36
            "01:02:03" +                             // Span, 8
            "Green " +                               // Colour, 6
            "AB   " +                                // Bytes, 5
            "cd   " +                                // Chars, 5
            "\r\n";

        private static FixedLengthSchema EveryTypeSchema()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "Id" ), new Window( 4 ) );
            schema.AddColumn( new Int32Column( "Count" ), new Window( 6 ) );
            schema.AddColumn( new DecimalColumn( "Amount" ), new Window( 10 ) );
            schema.AddColumn( new BooleanColumn( "Flag" ), new Window( 5 ) );
            schema.AddColumn( new CharColumn( "Grade" ), new Window( 1 ) );
            schema.AddColumn( new DateTimeColumn( "When" ), new Window( 10 ) );
            schema.AddColumn( new DateOnlyColumn( "Day" ), new Window( 10 ) );
            schema.AddColumn( new TimeOnlyColumn( "Time" ), new Window( 8 ) );
            schema.AddColumn( new GuidColumn( "Key" ), new Window( 36 ) );
            schema.AddColumn( new TimeSpanColumn( "Span" ), new Window( 8 ) );
            schema.AddColumn( new EnumColumn<Colour>( "Colour" ), new Window( 6 ) );
            schema.AddColumn( new ByteArrayColumn( "Bytes" ), new Window( 5 ) );
            schema.AddColumn( new CharArrayColumn( "Chars" ), new Window( 5 ) );
            return schema;
        }

        [TestMethod]
        public void TestRead_EveryColumnType_ParsesTheSameValues()
        {
            var reader = new FixedLengthReader( new StringReader( EveryTypeRecord ), EveryTypeSchema() );

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
        public void TestRead_ColumnsWithAnInputFormat_ParseExactly()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new DateTimeColumn( "a" ) { InputFormat = "yyyyMMdd" }, new Window( 8 ) );
            schema.AddColumn( new DateOnlyColumn( "b" ) { InputFormat = "yyyyMMdd" }, new Window( 8 ) );
            schema.AddColumn( new TimeOnlyColumn( "c" ) { InputFormat = "HHmmss" }, new Window( 6 ) );
            schema.AddColumn( new TimeSpanColumn( "d" ) { InputFormat = @"hh\:mm\:ss" }, new Window( 8 ) );
            schema.AddColumn( new GuidColumn( "e" ) { InputFormat = "N" }, new Window( 32 ) );
            var reader = new FixedLengthReader( new StringReader( "2026092220260923134556" + "01:02:03" + "0f8fad5bd9cb469fa16570867728950e\r\n" ), schema );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();

            Assert.AreEqual( new DateTime( 2026, 9, 22 ), values[0] );
            Assert.AreEqual( new DateOnly( 2026, 9, 23 ), values[1] );
            Assert.AreEqual( new TimeOnly( 13, 45, 56 ), values[2] );
            Assert.AreEqual( new TimeSpan( 1, 2, 3 ), values[3] );
            Assert.AreEqual( Guid.Parse( "0f8fad5b-d9cb-469f-a165-70867728950e" ), values[4] );
        }

        [TestMethod]
        public void TestRead_BooleanColumn_ReadsBothWordsAndRefusesAnythingElse()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new BooleanColumn( "a" ), new Window( 5 ) );
            schema.AddColumn( new BooleanColumn( "b" ), new Window( 5 ) );
            var reader = new FixedLengthReader( new StringReader( "TRUE falseno   \r\n" ), schema );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();

            Assert.AreEqual( true, values[0], "The words are matched without regard to case." );
            Assert.AreEqual( false, values[1] );

            var refusing = new FixedLengthSchema();
            refusing.AddColumn( new BooleanColumn( "a" ), new Window( 5 ) );
            var second = new FixedLengthReader( new StringReader( "maybe\r\n" ), refusing );

            Assert.ThrowsExactly<RecordProcessingException>( () => second.Read() );
        }

        [TestMethod]
        public void TestRead_BooleanColumnWithNoWords_RefusesEveryValue()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new BooleanColumn( "a" ) { TrueString = null, FalseString = null }, new Window( 4 ) );
            var reader = new FixedLengthReader( new StringReader( "True\r\n" ), schema );

            Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );
        }

        [TestMethod]
        public void TestRead_CharColumn_TakesTheFirstCharacterOnlyWhenTrailingIsAllowed()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new CharColumn( "a" ) { AllowTrailing = true }, new Window( 3 ) );
            var reader = new FixedLengthReader( new StringReader( "xyz\r\n" ), schema );

            Assert.IsTrue( reader.Read() );

            Assert.AreEqual( 'x', reader.GetValues()[0] );

            var refusing = new FixedLengthSchema();
            refusing.AddColumn( new CharColumn( "a" ), new Window( 3 ) );
            var second = new FixedLengthReader( new StringReader( "xyz\r\n" ), refusing );

            Assert.ThrowsExactly<RecordProcessingException>( () => second.Read() );
        }

        [TestMethod]
        public void TestRead_EnumColumn_ReadsANameOrItsNumber()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new EnumColumn<Colour>( "a" ), new Window( 6 ) );
            schema.AddColumn( new EnumColumn<Colour>( "b" ), new Window( 6 ) );
            var reader = new FixedLengthReader( new StringReader( "red   2     \r\n" ), schema );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();

            Assert.AreEqual( Colour.Red, values[0] );
            Assert.AreEqual( Colour.Green, values[1] );
        }

        [TestMethod]
        public void TestRead_ConversionColumn_ParsesThroughTheColumnItWraps()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( TimeSpanColumn.FromSeconds( new DoubleColumn( "a" ) ), new Window( 5 ) );
            var reader = new FixedLengthReader( new StringReader( "90   \r\n" ), schema );

            Assert.IsTrue( reader.Read() );

            Assert.AreEqual( TimeSpan.FromSeconds( 90 ), reader.GetValues()[0] );
        }

        [TestMethod]
        public void TestRead_RightAlignedWindow_TrimsTheLeadingFill()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "a" ), new Window( 6 ) { Alignment = FixedAlignment.RightAligned, FillCharacter = '0' } );
            schema.AddColumn( new StringColumn( "b" ), new Window( 4 ) );
            schema.AddColumn( new StringColumn( "c" ), new Window( 3 ) { Alignment = FixedAlignment.RightAligned, FillCharacter = '0' } );
            var reader = new FixedLengthReader( new StringReader( "000abcdefg000\r\n" ), schema );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();

            Assert.AreEqual( "abc", values[0] );
            Assert.AreEqual( "defg", values[1] );
            Assert.IsNull( values[2], "A window of nothing but fill is empty, which the null formatter reads as null." );
        }

        [TestMethod]
        public void TestRead_NullFormatterForAValue_ReadsItAsNull()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "a" ) { NullFormatter = NullFormatter.ForValue( "NULL" ) }, new Window( 4 ) );
            schema.AddColumn( new Int32Column( "b" ) { NullFormatter = NullFormatter.ForValue( "NULL" ) }, new Window( 4 ) );
            var reader = new FixedLengthReader( new StringReader( "NULL   7\r\n" ), schema );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();

            Assert.IsNull( values[0] );
            Assert.AreEqual( 7, values[1] );
        }

        [TestMethod]
        public void TestRead_NullFormatterForANullValue_MatchesNothingInTheFile()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "a" ) { NullFormatter = NullFormatter.ForValue( null ) }, new Window( 4 ) );
            var reader = new FixedLengthReader( new StringReader( "    \r\n" ), schema );

            Assert.IsTrue( reader.Read() );

            Assert.AreEqual( string.Empty, reader.GetValues()[0], "No text in a file is a null reference, so nothing matches." );
        }

        [TestMethod]
        public void TestRead_NullFormatterFromOutsideTheLibrary_IsStillAsked()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "a" ) { NullFormatter = new StringOnlyNullFormatter() }, new Window( 4 ) );
            var reader = new FixedLengthReader( new StringReader( "none\r\n" ), schema );

            Assert.IsTrue( reader.Read() );

            Assert.IsNull( reader.GetValues()[0] );
        }

        [TestMethod]
        public void TestRead_ParsingHooks_StillReceiveTheValueAsAString()
        {
            string parsing = null!;
            object parsed = null!;
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "a" )
            {
                OnParsing = ( _, v ) => { parsing = v; return v; },
                OnParsed = ( _, v ) => { parsed = v!; return v; }
            }, new Window( 4 ) );
            var reader = new FixedLengthReader( new StringReader( "0042\r\n" ), schema );

            Assert.IsTrue( reader.Read() );

            Assert.AreEqual( "0042", parsing );
            Assert.AreEqual( 42, parsed );
            Assert.AreEqual( 42, reader.GetValues()[0] );
        }

        [TestMethod]
        public void TestRead_ColumnWithOnlyTheStringOverload_StillParsesEveryValue()
        {
            var column = new StringOnlyColumn( "a" );
            var schema = new FixedLengthSchema();
            schema.AddColumn( column, new Window( 4 ) );
            var reader = new FixedLengthReader( new StringReader( "abcd\r\n" ), schema );

            Assert.IsTrue( reader.Read() );

            Assert.AreEqual( "ABCD", reader.GetValues()[0] );
            Assert.AreEqual( 1, column.StringCalls );
        }

        [TestMethod]
        public void TestRead_ColumnThatReplacedTheStringParse_StillReceivesEveryValueThroughIt()
        {
            var column = new StringParseOverridingColumn( "a" );
            var schema = new FixedLengthSchema();
            schema.AddColumn( column, new Window( 4 ) );
            var reader = new FixedLengthReader( new StringReader( "abcd\r\n" ), schema );

            Assert.IsTrue( reader.Read() );

            Assert.AreEqual( "abcd!", reader.GetValues()[0] );
            Assert.AreEqual( 1, column.Calls );
        }

        [TestMethod]
        public void TestRead_ColumnThatParsesASpan_ReceivesTheCharactersOfTheRecord()
        {
            var column = new SpanCountingColumn( "a" );
            var schema = new FixedLengthSchema();
            schema.AddColumn( column, new Window( 4 ) );
            var reader = new FixedLengthReader( new StringReader( "abcd\r\n" ), schema );

            Assert.IsTrue( reader.Read() );

            Assert.AreEqual( "abcd", reader.GetValues()[0] );
            Assert.AreEqual( 1, column.SpanCalls );
            Assert.AreEqual( 0, column.StringCalls );
        }

        [TestMethod]
        public void TestRead_EnumColumnWithItsOwnParser_TheParserStillReceivesAString()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new EnumColumn<Colour>( "a" ) { Parser = v => v == "G" ? Colour.Green : Colour.Red }, new Window( 1 ) );
            var reader = new FixedLengthReader( new StringReader( "G\r\n" ), schema );

            Assert.IsTrue( reader.Read() );

            Assert.AreEqual( Colour.Green, reader.GetValues()[0] );
        }

        [TestMethod]
        public void TestRead_IgnoredColumn_HooksStillRun()
        {
            string parsing = null!;
            var schema = new FixedLengthSchema();
            schema.AddColumn( new IgnoredColumn { OnParsing = ( _, v ) => { parsing = v; return v; } }, new Window( 4 ) );
            schema.AddColumn( new Int32Column( "b" ), new Window( 4 ) );
            var reader = new FixedLengthReader( new StringReader( "skip0042\r\n" ), schema );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();

            Assert.HasCount( 1, values );
            Assert.AreEqual( 42, values[0] );
            Assert.AreEqual( "skip", parsing );
        }

        [TestMethod]
        public void TestRead_PartitionedHandlerReplacesAValue_TheReplacementIsParsed()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "a" ), new Window( 4 ) );
            schema.AddColumn( new Int32Column( "b" ), new Window( 4 ) );
            var reader = new FixedLengthReader( new StringReader( "A0010007\r\n" ), schema );
            reader.RecordPartitioned += ( _, e ) => e.Values[1] = "42";

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();

            Assert.AreEqual( "A001", values[0] );
            Assert.AreEqual( 42, values[1] );
        }

        [TestMethod]
        public void TestRead_HookAsksForTheRawValues_TheyAreCopiedOutOfTheRecord()
        {
            string[] seen = null!;
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "a" ), new Window( 4 ) );
            schema.AddColumn( new Int32Column( "b" ) { OnParsed = ( ctx, v ) => { seen = ctx!.RecordContext.Values!; return v; } }, new Window( 4 ) );
            var reader = new FixedLengthReader( new StringReader( "A0010007\r\n" ), schema );

            Assert.IsTrue( reader.Read() );

            CollectionAssert.AreEqual( new[] { "A001", "0007" }, seen );
        }

        [TestMethod]
        public void TestRead_RaggedRightShortRecord_TheRawValuesStillCopyOut()
        {
            string[] seen = null!;
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "a" ), new Window( 4 ) );
            schema.AddColumn( new StringColumn( "b" ), new Window( 4 ) );
            schema.AddColumn( new StringColumn( "c" ) { OnParsed = ( ctx, v ) => { seen = ctx!.RecordContext.Values!; return v; } }, new Window( 4 ) );
            var reader = new FixedLengthReader( new StringReader( "A001\r\n" ), schema, new FixedLengthOptions { IsRaggedRight = true } );

            Assert.IsTrue( reader.Read() );

            CollectionAssert.AreEqual( new[] { "A001", string.Empty, string.Empty }, seen );
        }

        [TestMethod]
        public void TestRead_HookWritesToTheRawValues_TheWriteDoesNotReachTheNextColumn()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "a" ) { OnParsed = ( context, v ) => { context!.RecordContext!.Values![1] = "99"; return v; } }, new Window( 4 ) );
            schema.AddColumn( new Int32Column( "b" ), new Window( 4 ) );
            var reader = new FixedLengthReader( new StringReader( "A0010007\r\n" ), schema );

            Assert.IsTrue( reader.Read() );

            Assert.AreEqual( 7, reader.GetValues()[1],
                "The array a hook reads is a copy of the record's values, not the one the columns are parsed from. A record partitioned handler is what replaces a value before parsing." );
        }

        [TestMethod]
        public void TestRead_ContextKeptPastItsRecord_ReportsNoValuesButKeepsItsRecord()
        {
            List<IRecordContext> contexts = [];
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "a" ), new Window( 4 ) );
            schema.AddColumn( new StringColumn( "b" ), new Window( 4 ) );
            var reader = new FixedLengthReader( new StringReader( "A001one \r\nB002two \r\n" ), schema );
            reader.RecordParsed += ( _, e ) => contexts.Add( e.RecordContext );

            while (reader.Read())
            {
            }

            Assert.HasCount( 2, contexts );
            Assert.IsNull( contexts[0].Values, "The ranges the values sat in belong to the reader, which has moved on." );
            Assert.IsNull( contexts[1].Values );
            Assert.AreEqual( "A001one ", contexts[0].Record, "The record's own text belongs to the context and stays." );
            Assert.AreEqual( "B002two ", contexts[1].Record );
        }

        [TestMethod]
        public void TestRead_ContextReadWhileItsRecordIsCurrent_KeepsWhatItWasGiven()
        {
            List<IRecordContext> contexts = [];
            List<string[]> readWhileCurrent = [];
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "a" ), new Window( 4 ) );
            schema.AddColumn( new StringColumn( "b" ), new Window( 4 ) );
            var reader = new FixedLengthReader( new StringReader( "A001one \r\nB002two \r\n" ), schema );
            reader.RecordParsed += ( _, e ) =>
            {
                contexts.Add( e.RecordContext );
                readWhileCurrent.Add( e.RecordContext.Values! );
            };

            while (reader.Read())
            {
            }

            CollectionAssert.AreEqual( new[] { "A001", "one" }, readWhileCurrent[0] );
            CollectionAssert.AreEqual( new[] { "B002", "two" }, readWhileCurrent[1] );
            // Once copied out, the array belongs to the context and outlives the record it came from.
            CollectionAssert.AreEqual( new[] { "A001", "one" }, contexts[0].Values );
            CollectionAssert.AreEqual( new[] { "B002", "two" }, contexts[1].Values );
        }

        [TestMethod]
        public void TestRead_ParseError_CarriesTheValueAndThePartitionedRecord()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "a" ), new Window( 4 ) );
            schema.AddColumn( new Int32Column( "b" ), new Window( 4 ) );
            var reader = new FixedLengthReader( new StringReader( "A001nope\r\n" ), schema );
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
            Assert.AreEqual( 1, captured!.ColumnContext!.PhysicalIndex );
            CollectionAssert.AreEqual( new[] { "A001", "nope" }, captured.ColumnContext.RecordContext.Values );
        }

        [TestMethod]
        public void TestRead_ParseErrorWithTheColumnContextDisabled_StillNamesTheValue()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "a" ), new Window( 4 ) );
            var reader = new FixedLengthReader( new StringReader( "nope\r\n" ), schema, new FixedLengthOptions { IsColumnContextDisabled = true } );

            var exception = Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );

            var columnException = (ColumnProcessingException) exception.InnerException!;
            Assert.IsNotNull( columnException );
            Assert.AreEqual( "nope", columnException.ColumnValue );
            Assert.IsNull( columnException.ColumnContext );
        }

        [TestMethod]
        public void TestRead_ParseErrorWithAFormatProviderOnTheOptions_StillCarriesTheContext()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "a" ), new Window( 4 ) );
            var reader = new FixedLengthReader( new StringReader( "nope\r\n" ), schema, new FixedLengthOptions { FormatProvider = CultureInfo.InvariantCulture } );

            var exception = Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );

            var columnException = (ColumnProcessingException) exception.InnerException!;
            Assert.IsNotNull( columnException?.ColumnContext );
            Assert.AreEqual( "nope", columnException.ColumnValue );
        }

        [TestMethod]
        public void TestParse_ColumnImplementedFromTheInterface_CopiesTheSpanForIt()
        {
            IColumnDefinition column = new MinimalColumn();

            Assert.AreEqual( "42", column.Parse( null, "42".AsSpan() ) );
        }

        [TestMethod]
        public void TestIsNullValue_FormatterImplementedFromTheInterface_CopiesTheSpanForIt()
        {
            INullFormatter formatter = new StringOnlyNullFormatter();

            Assert.IsTrue( formatter.IsNullValue( null, "none".AsSpan() ) );
            Assert.IsFalse( formatter.IsNullValue( null, "7".AsSpan() ) );
        }

        private enum Colour
        {
            Red = 1,
            Green = 2
        }

        /// <summary>
        ///     A column from outside the library that implements only the string overload, as every column written
        ///     before the span overload existed does.
        /// </summary>
        private sealed class StringOnlyColumn( string columnName ) : ColumnDefinition<string>( columnName )
        {
            public int StringCalls { get; private set; }

            protected override string OnParse( IColumnContext? context, string value )
            {
                ++StringCalls;
                return value.ToUpperInvariant();
            }

            protected override string OnFormat( IColumnContext? context, string value )
            {
                return value;
            }
        }

        /// <summary>
        ///     A column from outside the library that replaced Parse itself rather than OnParse, and so has to keep
        ///     receiving every value through it.
        /// </summary>
        private sealed class StringParseOverridingColumn( string columnName ) : ColumnDefinition<string>( columnName )
        {
            public int Calls { get; private set; }

            public override object Parse( IColumnContext? context, string value )
            {
                ++Calls;
                return value + "!";
            }

            protected override string OnParse( IColumnContext? context, string value )
            {
                return value;
            }

            protected override string OnFormat( IColumnContext? context, string value )
            {
                return value;
            }
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

        private sealed class StringOnlyNullFormatter : INullFormatter
        {
            public bool IsNullValue( IColumnContext? context, string? value ) => value == "none";

            public string FormatNull( IColumnContext? context ) => "none";
        }

        /// <summary>
        ///     A column implemented straight from the interface, which takes both of its default members.
        /// </summary>
        private sealed class MinimalColumn : IColumnDefinition
        {
            public string ColumnName => "a";

            public bool IsIgnored => false;

            public bool IsNullable => true;

            public bool IsComplex => false;

            public IDefaultValue DefaultValue { get; set; } = FlatFiles.DefaultValue.Disabled();

            public INullFormatter NullFormatter { get; set; } = FlatFiles.NullFormatter.Default;

            public Func<IColumnContext?, string, string?>? OnParsing { get; set; } = null!;

            public Func<IColumnContext?, object?, object?>? OnParsed { get; set; } = null!;

            public Func<IColumnContext?, object?, object?>? OnFormatting { get; set; } = null!;

            public Func<IColumnContext?, string, string?>? OnFormatted { get; set; } = null!;

            public Type ColumnType => typeof( string );

            public object Parse( IColumnContext? context, string value ) => value;

            public string Format( IColumnContext? context, object? value ) => (string) value!;
        }
    }
}
