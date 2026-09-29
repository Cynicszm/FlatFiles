using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Linq;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the parsing hook that reads a value where it lies: that it transforms what the column parses,
    ///     that it can ask for a longer buffer, and that - unlike the string hook it sits beside - it leaves the
    ///     mapping able to read a record straight onto an entity.
    /// </summary>
    [TestClass]
    public class SpanParsingHookTester
    {
        [TestMethod]
        public void TestDelimited_TheHookTransformsTheValue()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new Int32Column( "Amount" ) { OnParsingSpan = StripLeadingHash } );
            var reader = new DelimitedReader( new StringReader( "#42\r\n" ), schema, Options() );

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( 42, reader.GetValues()[0] );
        }

        [TestMethod]
        public void TestFixedLength_TheHookTransformsTheValue()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "Amount" ) { OnParsingSpan = StripLeadingHash }, new Window( 3 ) );
            var options = new FixedLengthOptions { RecordSeparator = "\r\n" };
            var reader = new FixedLengthReader( new StringReader( "#42\r\n" ), schema, options );

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( 42, reader.GetValues()[0] );
        }

        [TestMethod]
        public void TestUnchanged_ParsesTheValueWhereItLies()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new Int32Column( "Amount" ) { OnParsingSpan = ( _, _, _ ) => SpanParsingHooks.Unchanged } );
            var reader = new DelimitedReader( new StringReader( "42\r\n" ), schema, Options() );

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( 42, reader.GetValues()[0] );
        }

        [TestMethod]
        public void TestAHookWantingALongerBuffer_IsAskedAgainWithOne()
        {
            var asked = 0;
            var lengths = new System.Collections.Generic.List<int>();
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "Padded" )
            {
                Trim = false,
                OnParsingSpan = ( _, value, destination ) =>
                {
                    ++asked;
                    lengths.Add( destination.Length );
                    // Wants far more room than a value of this size would be given to begin with.
                    const int wanted = 5000;
                    if (destination.Length < wanted)
                    {
                        return SpanParsingHooks.NeedsLength( wanted );
                    }
                    destination[..wanted].Fill( 'x' );
                    return wanted;
                }
            } );
            var reader = new DelimitedReader( new StringReader( "a\r\n" ), schema, Options() );

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( 5000, ( (string) reader.GetValues()[0]! ).Length );
            Assert.AreEqual( 2, asked, "the hook should be asked once, then again with the room it wanted" );
            Assert.IsTrue( lengths[1] >= 5000, $"the second buffer was {lengths[1]}" );
        }

        [TestMethod]
        public void TestAHookThatKeepsAskingForMore_IsReported()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "Greedy" )
            {
                OnParsingSpan = ( _, _, _ ) => SpanParsingHooks.NeedsLength( 32 )
            } );
            var reader = new DelimitedReader( new StringReader( "a\r\n" ), schema, Options() );

            var exception = Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );

            StringAssert.Contains( exception.InnerException!.Message, "Greedy" );
        }

        [TestMethod]
        public void TestNeedsLength_RefusesANonsenseLength()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>( () => SpanParsingHooks.NeedsLength( 0 ) );
            Assert.ThrowsExactly<ArgumentOutOfRangeException>( () => SpanParsingHooks.NeedsLength( -1 ) );
        }

        [TestMethod]
        public void TestAskingForOneCharacter_IsNotMistakenForUnchanged()
        {
            // A request is the negation of the length, so asking for one is -1. Were Unchanged that value, a
            // reader would read this as "leave the value alone", parse the original and never call the hook
            // again - losing its answer without a word. Unchanged is int.MinValue so that cannot happen.
            Assert.AreNotEqual( SpanParsingHooks.Unchanged, SpanParsingHooks.NeedsLength( 1 ) );
            Assert.AreEqual( int.MinValue, SpanParsingHooks.Unchanged );
        }

        [TestMethod]
        public void TestAHookAskingForOneCharacter_IsAskedAgain()
        {
            // The property that matters is behavioural, not arithmetic: a hook asking for room gets it.
            var asked = 0;
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "Tiny" )
            {
                Trim = false,
                OnParsingSpan = ( _, _, destination ) =>
                {
                    ++asked;
                    if (asked == 1)
                    {
                        return SpanParsingHooks.NeedsLength( 1 );
                    }
                    destination[0] = 'x';
                    return 1;
                }
            } );
            var reader = new DelimitedReader( new StringReader( "a" ), schema, Options() );

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( "x", reader.GetValues()[0] );
            Assert.AreEqual( 2, asked, "the hook should be asked again rather than ignored" );
        }

        [TestMethod]
        public void TestEveryValidRequestIsTellableFromUnchanged()
        {
            // The property the refusal exists to give: no length a hook may ask for encodes as Unchanged.
            foreach (var length in new[] { 2, 3, 16, 17, 1024, int.MaxValue })
            {
                Assert.AreNotEqual( SpanParsingHooks.Unchanged, SpanParsingHooks.NeedsLength( length ),
                    $"a request for {length} cannot be told from Unchanged" );
            }
        }

        [TestMethod]
        public void TestAHookIsNeverOfferedLessThanItIsPromised()
        {
            // NeedsLength refuses one on the grounds that a hook always has room for it. That is only true if
            // the reader really does offer at least this much, so the claim is checked rather than asserted.
            var offered = int.MaxValue;
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "Tiny" )
            {
                OnParsingSpan = ( _, _, destination ) =>
                {
                    offered = Math.Min( offered, destination.Length );
                    return SpanParsingHooks.Unchanged;
                }
            } );
            var reader = new DelimitedReader( new StringReader( ",,a" ), schema, Options() );

            while (reader.Read())
            {
            }

            Assert.IsTrue( offered >= SpanParsingHooks.MinimumOffered,
                $"the shortest buffer offered was {offered}, and the API promises {SpanParsingHooks.MinimumOffered}" );
        }

        [TestMethod]
        public void TestTheStringHookWins_WhereBothAreSet()
        {
            // A column that has to build a string for one hook may as well give it to both, so the older hook
            // takes the value and the span hook is not called.
            var called = false;
            var schema = new DelimitedSchema();
            schema.AddColumn( new Int32Column( "Amount" )
            {
                OnParsing = ( _, value ) => value.Replace( "#", string.Empty, StringComparison.Ordinal ),
                OnParsingSpan = ( _, _, _ ) =>
                {
                    called = true;
                    return SpanParsingHooks.Unchanged;
                }
            } );
            var reader = new DelimitedReader( new StringReader( "#42\r\n" ), schema, Options() );

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( 42, reader.GetValues()[0] );
            Assert.IsFalse( called, "the span hook should not be called when the string hook is set" );
        }

        [TestMethod]
        public void TestTheStringOverload_RunsTheHookToo()
        {
            // A caller holding a string should get the same value a reader holding the record would.
            var column = new Int32Column( "Amount" ) { OnParsingSpan = StripLeadingHash };

            Assert.AreEqual( 42, column.Parse( null, "#42" ) );
        }

        [TestMethod]
        public void TestNullHandlingAndTrimmingStillApplyAfterTheHook()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new Int32Column( "Amount" )
            {
                // Hands back spaces, which the column trims, leaving nothing, which reads as null.
                OnParsingSpan = ( _, _, destination ) =>
                {
                    destination[..3].Fill( ' ' );
                    return 3;
                }
            } );
            var reader = new DelimitedReader( new StringReader( "42\r\n" ), schema, Options() );

            Assert.IsTrue( reader.Read() );
            Assert.IsNull( reader.GetValues()[0] );
        }

        [TestMethod]
        public void TestTheMappingCanStillBeReadStraightOntoAnEntity()
        {
            // The point of the hook: its column keeps a typed setter, where a string hook leaves that column
            // reading its value as an object. Both mappings keep their setters - a hooked column no longer takes
            // the rest of the mapping with it - so what separates them is which setter the column gets.
            var withSpanHook = DelimitedTypeMapper.Define<Entity>();
            withSpanHook.Property( x => x.Amount ).OnParsingSpan( StripLeadingHash );

            var withStringHook = DelimitedTypeMapper.Define<Entity>();
            withStringHook.Property( x => x.Amount ).OnParsing( ( _, value ) => value.TrimStart( '#' ) );

            var spanSetters = Setters( withSpanHook );
            var stringSetters = Setters( withStringHook );

            Assert.IsNotNull( spanSetters );
            Assert.IsNotNull( stringSetters );
            Assert.IsInstanceOfType<ColumnSetter<Entity, int>>( spanSetters[0], "a span hook keeps its column typed" );
            Assert.IsInstanceOfType<BoxingColumnSetter<Entity, int>>( stringSetters[0], "a string hook costs its column, and only its column" );
        }

        [TestMethod]
        public void TestAMapperReadsThroughTheHook()
        {
            var mapper = DelimitedTypeMapper.Define<Entity>();
            mapper.Property( x => x.Amount ).OnParsingSpan( StripLeadingHash );

            var entities = mapper.Read( new StringReader( "#42\r\n#7\r\n" ), Options() ).ToList();

            CollectionAssert.AreEqual( new[] { 42, 7 }, entities.Select( x => x.Amount ).ToArray() );
        }

        [TestMethod]
        public void TestTheHookReachesAComplexColumn()
        {
            var inner = DelimitedTypeMapper.Define<Entity>();
            inner.Property( x => x.Amount ).OnParsingSpan( StripLeadingHash );
            var outer = DelimitedTypeMapper.Define<Outer>();
            outer.Property( x => x.Name );
            outer.ComplexProperty( x => x.Inner!, inner ).OnParsingSpan( ( _, _, _ ) => SpanParsingHooks.Unchanged );

            var results = outer.Read( new StringReader( "Bob,\"#42\"\r\n" ), Options() ).ToList();

            Assert.ContainsSingle( results );
            Assert.AreEqual( 42, results[0].Inner!.Amount );
        }

        [TestMethod]
        public void TestAMappingWrittenBeforeTheHookExisted_SaysSoRatherThanIgnoringIt()
        {
            // The method is a default on the mapping interfaces so that an implementation written outside this
            // library keeps compiling. Silently dropping a hook someone set would be worse than saying so.
            IIgnoredMapping mapping = new MappingFromBeforeTheHook();

            var exception = Assert.ThrowsExactly<NotSupportedException>(
                () => mapping.OnParsingSpan( ( _, _, _ ) => SpanParsingHooks.Unchanged ) );

            StringAssert.Contains( exception.Message, "span parsing hook" );
        }

        [TestMethod]
        public void TestTheHookIsReachableThroughTheColumnInterface()
        {
            // OnParsing has always been settable through IColumnDefinition; this sits beside it rather than
            // making a caller cast to reach one hook but not the other.
            IColumnDefinition column = new Int32Column( "Amount" );
            column.OnParsingSpan = StripLeadingHash;

            var schema = new DelimitedSchema();
            schema.AddColumn( column );
            var reader = new DelimitedReader( new StringReader( "#42\r\n" ), schema, Options() );

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( 42, reader.GetValues()[0] );
            Assert.IsNotNull( column.OnParsingSpan );
        }

        [TestMethod]
        public void TestAColumnWrittenBeforeTheHookExisted_SaysSoRatherThanIgnoringIt()
        {
            IColumnDefinition column = new ColumnFromBeforeTheHook();

            Assert.IsNull( column.OnParsingSpan, "reading says there is no hook" );
            var exception = Assert.ThrowsExactly<NotSupportedException>( () => column.OnParsingSpan = StripLeadingHash );
            StringAssert.Contains( exception.Message, "span parsing hook" );
        }

        /// <summary>Stands in for a column implemented outside this library before the hook existed.</summary>
        private sealed class ColumnFromBeforeTheHook : IColumnDefinition
        {
            public string? ColumnName => "Old";

            public bool IsIgnored => false;

            public bool IsNullable => true;

            public bool IsComplex => false;

            public Type ColumnType => typeof( string );

            public IDefaultValue DefaultValue { get; set; } = FlatFiles.DefaultValue.Disabled();

            public INullFormatter NullFormatter { get; set; } = FlatFiles.NullFormatter.Default;

            public Func<IColumnContext?, string, string?>? OnParsing { get; set; }

            public Func<IColumnContext?, object?, object?>? OnParsed { get; set; }

            public Func<IColumnContext?, object?, object?>? OnFormatting { get; set; }

            public Func<IColumnContext?, string, string?>? OnFormatted { get; set; }

            public object? Parse( IColumnContext? context, string value ) => value;

            public object? Parse( IColumnContext? context, ReadOnlySpan<char> value ) => value.ToString();

            public string Format( IColumnContext? context, object? value ) => value?.ToString() ?? string.Empty;

            public void Format( IColumnContext? context, object? value, IBufferWriter<char> destination ) =>
                destination.Write( Format( context, value ).AsSpan() );

            public bool IsColumnContextRequired => false;
        }

        /// <summary>Stands in for a mapping implemented outside this library before the hook existed.</summary>
        private sealed class MappingFromBeforeTheHook : IIgnoredMapping
        {
            public IIgnoredMapping ColumnName( string name ) => this;

            public IIgnoredMapping NullFormatter( INullFormatter formatter ) => this;

            public IIgnoredMapping OnParsing( Func<IColumnContext?, string, string?>? handler ) => this;

            public IIgnoredMapping OnParsed( Func<IColumnContext?, object?, object?>? handler ) => this;

            public IIgnoredMapping OnFormatting( Func<IColumnContext?, object?, object?>? handler ) => this;

            public IIgnoredMapping OnFormatted( Func<IColumnContext?, string?, string?>? handler ) => this;
        }

        private static IColumnSetter<Entity>[]? Setters( IDelimitedTypeMapper<Entity> mapper )
        {
            return ( (IMapperSource<Entity>) mapper ).GetMapper().GetColumnSetters();
        }

        private static int StripLeadingHash( IColumnContext? context, ReadOnlySpan<char> value, Span<char> destination )
        {
            var trimmed = value.TrimStart( '#' );
            if (trimmed.Length == value.Length)
            {
                return SpanParsingHooks.Unchanged;
            }
            trimmed.CopyTo( destination );
            return trimmed.Length;
        }

        private static DelimitedOptions Options()
        {
            return new DelimitedOptions { RecordSeparator = "\r\n" };
        }

        public class Entity
        {
            public int Amount { get; set; }
        }

        public class Outer
        {
            public string Name { get; set; } = string.Empty;

            public Entity? Inner { get; set; }
        }
    }
}
