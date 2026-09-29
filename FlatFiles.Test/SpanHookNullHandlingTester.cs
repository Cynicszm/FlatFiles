using System;
using System.IO;
using System.Linq;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests that a span parsing hook is given the same standing on the path that reads straight onto an
    ///     entity as on the one that parses into objects: it sees every value, including a blank one, and what it
    ///     answers is then judged for nullness as any other value would be.
    /// </summary>
    /// <remarks>
    ///     The two paths once disagreed. The typed one asked whether the value read as null before the hook had
    ///     seen it, so a blank never reached the hook, and then parsed whatever the hook wrote without asking the
    ///     same question of it - so a hook could neither supply a value for a blank nor answer null for one.
    ///     Both are what a hook is for.
    /// </remarks>
    [TestClass]
    public class SpanHookNullHandlingTester
    {
        /// <summary>Writes a zero where the value is blank, and otherwise leaves it alone.</summary>
        private static int ZeroForBlank( IColumnContext? context, ReadOnlySpan<char> value, Span<char> destination )
        {
            if (!value.IsWhiteSpace())
            {
                return SpanParsingHooks.Unchanged;
            }
            "0".AsSpan().CopyTo( destination );
            return 1;
        }

        /// <summary>Writes nothing for the word this file uses to mean "no date".</summary>
        private static int NothingForOpen( IColumnContext? context, ReadOnlySpan<char> value, Span<char> destination )
        {
            return value.Trim().SequenceEqual( "OPEN" ) ? 0 : SpanParsingHooks.Unchanged;
        }

        [TestMethod]
        public void TestABlankValue_ReachesTheHook()
        {
            var mapper = FixedLengthTypeMapper.Define<Row>();
            mapper.Property( x => x.Amount, new Window( 4 ) ).OnParsingSpan( ZeroForBlank );

            var rows = mapper.Read( new StringReader( "    \r\n" ), Options() ).ToList();

            Assert.AreEqual( 0m, rows[0].Amount, "the hook supplies a value for a blank, so it has to see one" );
        }

        [TestMethod]
        public void TestAHookThatWritesNothing_ReadsAsNull()
        {
            var mapper = FixedLengthTypeMapper.Define<Row>();
            mapper.Property( x => x.Date, new Window( 4 ) ).OnParsingSpan( NothingForOpen );

            var rows = mapper.Read( new StringReader( "OPEN\r\n" ), Options() ).ToList();

            Assert.IsNull( rows[0].Date, "what the hook answered is judged for nullness as any value would be" );
        }

        [TestMethod]
        public void TestAHookThatWritesNothing_TakesTheDefaultWhereTheColumnCannotHoldNull()
        {
            var column = new Int32Column( "Date" )
            {
                IsNullable = false,
                DefaultValue = DefaultValue.Use( -1 ),
                OnParsingSpan = NothingForOpen
            };
            var schema = new FixedLengthSchema();
            schema.AddColumn( column, new Window( 4 ) );
            var reader = new FixedLengthReader( new StringReader( "OPEN\r\n" ), schema, Options() );

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( -1, reader.GetValues()[0] );
        }

        [TestMethod]
        public void TestTheTwoPathsAgree()
        {
            // The object path and the typed path have to answer the same thing for the same value. A mapped
            // property takes the typed path; a schema read takes the object one.
            foreach (var text in new[] { "    ", "OPEN", "0012" })
            {
                var mapper = FixedLengthTypeMapper.Define<Row>();
                mapper.Property( x => x.Date, new Window( 4 ) ).OnParsingSpan( Combined );
                var typed = mapper.Read( new StringReader( text + "\r\n" ), Options() ).Single().Date;

                var schema = new FixedLengthSchema();
                schema.AddColumn( new Int32Column( "Date" ) { OnParsingSpan = Combined }, new Window( 4 ) );
                var reader = new FixedLengthReader( new StringReader( text + "\r\n" ), schema, Options() );
                Assert.IsTrue( reader.Read() );
                var boxed = reader.GetValues()[0];

                Assert.AreEqual( typed, (int?) boxed, $"the two paths disagree about {text.Trim()}" );
            }
        }

        /// <summary>Both of the above at once, so one hook covers every value the agreement test reads.</summary>
        private static int Combined( IColumnContext? context, ReadOnlySpan<char> value, Span<char> destination )
        {
            if (value.Trim().SequenceEqual( "OPEN" ))
            {
                return 0;
            }
            return ZeroForBlank( context, value, destination );
        }

        [TestMethod]
        public void TestAHookIsStillSkippedForNothing_WhenItLeavesTheValueAlone()
        {
            // Unchanged means exactly that: the value is judged as it lies, blank included.
            var mapper = FixedLengthTypeMapper.Define<Row>();
            mapper.Property( x => x.Amount, new Window( 4 ) )
                .OnParsingSpan( ( _, _, _ ) => SpanParsingHooks.Unchanged );

            var rows = mapper.Read( new StringReader( "    \r\n1234\r\n" ), Options() ).ToList();

            Assert.IsNull( rows[0].Amount );
            Assert.AreEqual( 1234m, rows[1].Amount );
        }

        private static FixedLengthOptions Options()
        {
            return new FixedLengthOptions { RecordSeparator = "\r\n" };
        }

        public class Row
        {
            public decimal? Amount { get; set; }

            public int? Date { get; set; }
        }
    }
}
