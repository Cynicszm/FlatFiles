using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests that formatting a column into a buffer produces exactly the text that formatting it to a string does,
    ///     for every built-in column type and for the hooks and fallbacks around them.
    /// </summary>
    [TestClass]
    public class ColumnBufferFormattingTester
    {
        private static readonly Guid SampleGuid = Guid.Parse( "6f9619ff-8b86-d011-b42d-00c04fc964ff" );

        private static string ToBuffer( IColumnDefinition column, object value )
        {
            // A tiny initial size forces the span-formatting columns to ask for more room and try again.
            var buffer = new ArrayBufferWriter<char>( 4 );
            column.Format( null, value, buffer );
            return buffer.WrittenSpan.ToString();
        }

        private static void AssertSameAsString( IColumnDefinition column, object value )
        {
            Assert.AreEqual( column.Format( null, value ), ToBuffer( column, value ), $"{column.GetType().Name} formatted {value} differently." );
        }

        [TestMethod]
        public void TestFormat_EveryBuiltInColumn_MatchesStringFormatting()
        {
            var german = CultureInfo.GetCultureInfo( "de-DE" );
            var when = new DateTime( 2026, 9, 16, 14, 30, 0 );
            AssertSameAsString( new ByteColumn( "c" ), (byte) 200 );
            AssertSameAsString( new SByteColumn( "c" ), (sbyte) -100 );
            AssertSameAsString( new Int16Column( "c" ), (short) -12345 );
            AssertSameAsString( new Int32Column( "c" ) { OutputFormat = "N0", FormatProvider = german }, 1234567 );
            AssertSameAsString( new Int64Column( "c" ), long.MinValue );
            AssertSameAsString( new UInt16Column( "c" ), ushort.MaxValue );
            AssertSameAsString( new UInt32Column( "c" ), uint.MaxValue );
            AssertSameAsString( new UInt64Column( "c" ), ulong.MaxValue );
            AssertSameAsString( new SingleColumn( "c" ) { OutputFormat = "F3" }, 3.14159f );
            AssertSameAsString( new DoubleColumn( "c" ) { FormatProvider = german }, 1234.5678 );
            AssertSameAsString( new DecimalColumn( "c" ) { OutputFormat = "C2", FormatProvider = german }, 1234.5m );
            AssertSameAsString( new DateTimeColumn( "c" ), when );
            AssertSameAsString( new DateTimeColumn( "c" ) { OutputFormat = "'The date is' dddd, dd MMMM yyyy 'at' HH:mm:ss.fffffff 'precisely'" }, when );
            AssertSameAsString( new DateTimeOffsetColumn( "c" ) { OutputFormat = "o" }, new DateTimeOffset( when, TimeSpan.FromHours( 1 ) ) );
            AssertSameAsString( new TimeSpanColumn( "c" ), new TimeSpan( 1, 2, 3, 4, 5 ) );
            AssertSameAsString( new TimeSpanColumn( "c" ) { OutputFormat = @"hh\:mm" }, new TimeSpan( 1, 2, 3 ) );
            AssertSameAsString( new GuidColumn( "c" ), SampleGuid );
            AssertSameAsString( new GuidColumn( "c" ) { OutputFormat = "B" }, SampleGuid );
            AssertSameAsString( new CharColumn( "c" ), 'x' );
            AssertSameAsString( new CharArrayColumn( "c" ), "chars".ToCharArray() );
            AssertSameAsString( new ByteArrayColumn( "c" ) { Encoding = Encoding.UTF8 }, Encoding.UTF8.GetBytes( "naïve" ) );
            AssertSameAsString( new BooleanColumn( "c" ) { TrueString = "yes", FalseString = "no" }, true );
            AssertSameAsString( new BooleanColumn( "c" ) { TrueString = "yes", FalseString = "no" }, false );
            AssertSameAsString( new StringColumn( "c" ), "plain text" );
            AssertSameAsString( new StringColumn( "c" ), new string( 'x', 5000 ) );
        }

        [TestMethod]
        public void TestFormat_NullValue_WritesTheNullFormatterText()
        {
            var column = new Int32Column( "c" ) { NullFormatter = NullFormatter.ForValue( "NULL" ) };

            Assert.AreEqual( "NULL", ToBuffer( column, null ) );
        }

        [TestMethod]
        public void TestFormat_OnFormattingHook_IsAppliedBeforeFormatting()
        {
            var column = new Int32Column( "c" ) { OnFormatting = ( _, value ) => (int) value * 2 };

            Assert.AreEqual( "84", ToBuffer( column, 42 ) );
        }

        [TestMethod]
        public void TestFormat_OnFormattedHook_SeesTheWholeString()
        {
            var column = new StringColumn( "c" ) { OnFormatted = ( _, value ) => value.ToUpperInvariant() };

            Assert.AreEqual( "SHOUT", ToBuffer( column, "shout" ) );
        }

        [TestMethod]
        public void TestFormat_CustomColumnWithoutBufferOverride_FallsBackToItsStringFormat()
        {
            Assert.AreEqual( "ff", ToBuffer( new HexColumn(), 255 ) );
        }

        [TestMethod]
        public void TestFormat_ExternalInterfaceImplementation_UsesTheInterfaceDefault()
        {
            Assert.AreEqual( "constant", ToBuffer( new ConstantColumn(), "ignored" ) );
        }

        [TestMethod]
        public void TestFormat_BufferOverloadWithoutDestination_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>( () => new Int32Column( "c" ).Format( null, 1, null ) );
        }

        [TestMethod]
        public void TestWrite_RecordNumberColumn_FormatsTheRecordNumberIntoTheBuffer()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new RecordNumberColumn( "n" ) );
            schema.AddColumn( new StringColumn( "s" ) );
            var output = new StringWriter();
            var writer = new DelimitedWriter( output, schema, new DelimitedOptions { RecordSeparator = "\n" } );

            // A metadata column still takes a slot in the values; whatever is passed for it is ignored.
            writer.Write( [ null, "a" ] );
            writer.Write( [ null, "b" ] );

            Assert.AreEqual( "1,a\n2,b\n", output.ToString() );
        }

        [TestMethod]
        public void TestWrite_ColumnFailsAfterWritingPartialText_TheSubstitutionReplacesIt()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new Int32Column( "a" ) );
            schema.AddColumn( new FailingColumn() );
            schema.AddColumn( new Int32Column( "b" ) );
            var output = new StringWriter();
            var writer = new DelimitedWriter( output, schema, new DelimitedOptions { RecordSeparator = "\n" } );
            writer.ColumnError += ( _, e ) =>
            {
                e.Substitution = "sub";
                e.IsHandled = true;
            };

            writer.Write( [ 1, 2, 3 ] );

            Assert.AreEqual( "1,sub,3\n", output.ToString() );
        }

        [TestMethod]
        public void TestWrite_ColumnFailsWithoutAHandler_ThrowsWithTheColumnFailureAsTheCause()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new FailingColumn() );
            var writer = new DelimitedWriter( new StringWriter(), schema );

            var exception = Assert.ThrowsExactly<RecordProcessingException>( () => writer.Write( [ 1 ] ) );

            Assert.IsInstanceOfType<ColumnProcessingException>( exception.InnerException );
        }

        /// <summary>
        ///     A custom column that only overrides the string formatting members, as every column written before the
        ///     buffer overload existed does.
        /// </summary>
        private sealed class HexColumn : ColumnDefinition<int>
        {
            public HexColumn()
                : base( "hex" )
            {
            }

            protected override int OnParse( IColumnContext context, string value )
            {
                return Convert.ToInt32( value, 16 );
            }

            protected override string OnFormat( IColumnContext context, int value )
            {
                return value.ToString( "x", CultureInfo.InvariantCulture );
            }
        }

        /// <summary>
        ///     A column that writes part of its value and then fails, so the writer has to discard what it wrote.
        /// </summary>
        private sealed class FailingColumn : ColumnDefinition<int>
        {
            public FailingColumn()
                : base( "fail" )
            {
            }

            protected override int OnParse( IColumnContext context, string value )
            {
                return 0;
            }

            protected override string OnFormat( IColumnContext context, int value )
            {
                throw new InvalidOperationException( "The value cannot be formatted." );
            }

            protected override void OnFormat( IColumnContext context, int value, IBufferWriter<char> destination )
            {
                destination.Write( "partial".AsSpan() );
                throw new InvalidOperationException( "The value cannot be formatted." );
            }
        }

        /// <summary>
        ///     Implements the column interface directly, as a caller outside the library might, so the buffer overload
        ///     it never heard of has to come from the interface default.
        /// </summary>
        private sealed class ConstantColumn : IColumnDefinition
        {
            public string ColumnName => "constant";

            public bool IsIgnored => false;

            public bool IsNullable => false;

            public bool IsComplex => false;

            public IDefaultValue DefaultValue { get; set; } = FlatFiles.DefaultValue.Disabled();

            public INullFormatter NullFormatter { get; set; } = FlatFiles.NullFormatter.Default;

            [Obsolete( "This property has been superseded by the OnParsing delegate." )]
            public Func<string, string> Preprocessor { get; set; }

            public Func<IColumnContext, string, string> OnParsing { get; set; }

            public Func<IColumnContext, object, object> OnParsed { get; set; }

            public Func<IColumnContext, object, object> OnFormatting { get; set; }

            public Func<IColumnContext, string, string> OnFormatted { get; set; }

            public Type ColumnType => typeof( string );

            public object Parse( IColumnContext context, string value )
            {
                return "constant";
            }

            public string Format( IColumnContext context, object value )
            {
                return "constant";
            }
        }
    }
}
