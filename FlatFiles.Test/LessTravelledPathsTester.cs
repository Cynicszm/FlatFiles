using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Pins the branches the feature tests do not take: nested and static member expressions in custom mappings, an
    ///     ignored column with every hook attached, auto-map matchers with more than one candidate, enum and Guid
    ///     conversions in the data reader, injectors with predicates and without defaults, and invalid window values.
    /// </summary>
    [TestClass]
    public class LessTravelledPathsTester
    {
        public enum Colour
        {
            Red = 1,
            Green = 2
        }

        [Flags]
        public enum Permissions
        {
            None = 0,
            Read = 1,
            Write = 2
        }

        public sealed class Inner
        {
            public int Id { get; set; }
        }

        public sealed class Outer
        {
            public string Name { get; set; }

            public string Nickname { get; set; }

            public int Field;

            public int Other;

            public Inner Inner { get; set; } = new();
        }

        [TestMethod]
        public void TestCustomMappingReaderExpression_NestedFieldAndStaticMembers()
        {
            var mapper = DelimitedTypeMapper.Define<Outer>();
            mapper.CustomMapping( new Int32Column( "InnerId" ) ).WithReader( o => o.Inner.Id );
            mapper.CustomMapping( new Int32Column( "Field" ) ).WithReader( o => o.Field );
            mapper.CustomMapping( new StringColumn( "Name" ) ).WithReader( o => o.Name );

            var read = mapper.Read( new StringReader( "7,8,Ann\n" ) ).Single();

            Assert.AreEqual( 7, read.Inner.Id, "A nested member is reached through its parent." );
            Assert.AreEqual( 8, read.Field, "A field is assigned like a property." );
            Assert.AreEqual( "Ann", read.Name );
            Assert.ThrowsExactly<ArgumentException>( () => mapper.CustomMapping( new StringColumn( "Static" ) ).WithReader( _ => string.Empty ), "A static member of another type has no instance to read from." );
        }

        [TestMethod]
        public void TestIgnoredColumn_EveryHookRunsAndTheValueIsStillDropped()
        {
            var calls = 0;
            var column = new IgnoredColumn( "skip" )
            {
                OnParsing = ( _, v ) => { calls++; return v; },
                OnParsed = ( _, v ) => { calls++; return v; },
                OnFormatting = ( _, v ) => { calls++; return v; },
                OnFormatted = ( _, v ) => { calls++; return v; },
                NullFormatter = NullFormatter.ForValue( "-" )
            };
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            schema.AddColumn( column );
            var reader = new DelimitedReader( new StringReader( "x,ignored\n" ), schema );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();
            var text = new StringWriter();
            new DelimitedWriter( text, schema, new DelimitedOptions { RecordSeparator = "\n" } ).Write( [ "x" ] );

            Assert.HasCount( 1, values, "The ignored column takes no slot in the values." );
            Assert.AreEqual( "x,-\n", text.ToString(), "The ignored column is written as its null formatter's text." );
            Assert.AreEqual( 4, calls );
            Assert.IsNull( column.Parse( null, "anything" ) );
        }

        [TestMethod]
        public void TestDataReader_GetString_HonoursIsNullStringAllowed()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            var allowed = new FlatFileDataReader( new DelimitedReader( new StringReader( "\n" ), schema ), new FlatFileDataReaderOptions { IsNullStringAllowed = true } );
            var refused = new FlatFileDataReader( new DelimitedReader( new StringReader( "\n" ), schema ), new FlatFileDataReaderOptions { IsNullStringAllowed = false } );

            Assert.IsTrue( allowed.Read() );
            Assert.IsNull( allowed.GetString( 0 ) );
            Assert.IsTrue( refused.Read() );
            Assert.ThrowsExactly<InvalidCastException>( () => refused.GetString( 0 ) );
        }

        [TestMethod]
        public void TestAutoMapMatcher_WithSeveralCandidates_FallsBackToTheExactNameOrGivesUp()
        {
            const string text = "Name,Field\nAnn,5\n";
            var fallback = AutoMapMatcher.For( ( column, member ) => member.Name.StartsWith( column.ColumnName![0] ), true );
            var noFallback = AutoMapMatcher.For( ( column, member ) => member.Name.StartsWith( column.ColumnName![0] ), false );

            var read = DelimitedTypeMapper.GetAutoMappedReader<Outer>( new StringReader( text ), null, fallback ).ReadAll().Single();

            Assert.AreEqual( "Ann", read.Name, "Name and Nickname both start with N; the exact name wins." );
            Assert.AreEqual( 5, read.Field, "Field and Other do not clash on F, so the field is taken as is." );
            Assert.ThrowsExactly<FlatFileException>( () => DelimitedTypeMapper.GetAutoMappedReader<Outer>( new StringReader( text ), null, noFallback ), "Without the fallback an ambiguous column has no member." );
        }

        [TestMethod]
        public void TestDataReader_GuidAndEnumConversions()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "GuidText" ) );
            schema.AddColumn( new ByteArrayColumn( "GuidBytes" ) { Encoding = System.Text.Encoding.Latin1 } );
            schema.AddColumn( new Int32Column( "Number" ) );
            schema.AddColumn( new StringColumn( "Word" ) );
            var guid = new Guid( "12345678-1234-1234-1234-123456789012" );
            var bytes = System.Text.Encoding.Latin1.GetString( guid.ToByteArray() );
            var reader = new FlatFileDataReader( new DelimitedReader( new StringReader( $"{guid},{bytes},3,Purple\n" ), schema ) );

            Assert.IsTrue( reader.Read() );

            Assert.AreEqual( guid, reader.GetValue<Guid>( "GuidText" ), "A Guid is parsed from text." );
            Assert.AreEqual( guid, reader.GetValue<Guid>( "GuidBytes" ), "A Guid is built from sixteen bytes." );
            Assert.ThrowsExactly<InvalidCastException>( () => reader.GetValue<Guid>( "Number" ), "Anything else is handed back and fails the cast." );
            Assert.AreEqual( Permissions.Read | Permissions.Write, reader.GetValue<Permissions>( "Number" ), "A flags enum takes any combination." );
            Assert.ThrowsExactly<InvalidCastException>( () => reader.GetValue<Colour>( "Word" ), "A name the enum does not have is handed back and fails the cast." );
            Assert.ThrowsExactly<InvalidCastException>( () => reader.GetValue<Colour>( "GuidText" ) );
        }

        [TestMethod]
        public void TestWriter_FormatErrorOnAHookedColumn_IsRecoveredThroughTheHandler()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new Int32Column( "n" ) { OnFormatted = ( _, v ) => v } );
            var text = new StringWriter();
            var writer = new DelimitedWriter( text, schema, new DelimitedOptions { RecordSeparator = "\n" } );
            writer.ColumnError += ( _, e ) => { e.IsHandled = true; e.Substitution = "?"; };

            writer.Write( [ "not an int" ] );

            Assert.AreEqual( "?\n", text.ToString() );
        }

        [TestMethod]
        public void TestSchemaInjectors_NoMatchAndNoDefault_Throw()
        {
            var header = new DelimitedSchema();
            header.AddColumn( new StringColumn( "a" ) );
            var injector = new DelimitedSchemaInjector();
            injector.When( v => v.Length == 1 ).Use( header );
            var writer = new DelimitedWriter( new StringWriter(), injector );

            writer.Write( [ "one" ] );
            Assert.ThrowsExactly<RecordProcessingException>( () => writer.Write( [ "one", "two" ] ), "No matcher and no default." );
            injector.WithDefault( header );
            injector.WithDefault( null );
            Assert.ThrowsExactly<RecordProcessingException>( () => writer.Write( [ "one", "two" ] ), "Clearing the default puts the injector back." );

            var fixedHeader = new FixedLengthSchema();
            fixedHeader.AddColumn( new StringColumn( "a" ), 3 );
            var fixedInjector = new FixedLengthSchemaInjector();
            fixedInjector.When( v => v.Length == 1 ).Use( fixedHeader );
            var fixedWriter = new FixedLengthWriter( new StringWriter(), fixedInjector );
            fixedWriter.Write( [ "one" ] );
            Assert.ThrowsExactly<RecordProcessingException>( () => fixedWriter.Write( [ "one", "two" ] ) );
            fixedInjector.WithDefault( null );

            var selector = new FixedLengthSchemaSelector();
            selector.WithDefault( fixedHeader );
            selector.WithDefault( null );
            Assert.ThrowsExactly<RecordProcessingException>( () => new FixedLengthReader( new StringReader( "abc\n" ), selector ).Read(), "A cleared default leaves no schema for the record." );
        }

        [TestMethod]
        public void TestMetadataColumn_FormatsThroughAColumnContext()
        {
            var column = new RecordNumberColumn( "n" ) { OutputFormat = "D2" };
            var schema = new DelimitedSchema();
            schema.AddColumn( column );
            schema.AddColumn( new StringColumn( "a" ) );
            IRecordContext captured = null;
            var reader = new DelimitedReader( new StringReader( "1,x\n" ), schema );
            reader.RecordParsed += ( _, e ) => captured = e.RecordContext;

            Assert.IsTrue( reader.Read() );
            var context = new ColumnContext( captured, 0, 0 );

            Assert.AreEqual( "01", column.Format( context, null ), "The string form formats the record number from the context." );
            Assert.AreEqual( 1, column.Parse( context, string.Empty ) );
        }

        [TestMethod]
        public void TestDynamicCustomMapping_WriterShapes()
        {
            var mapper = DelimitedTypeMapper.DefineDynamic( typeof( Outer ) );
            var custom = mapper.CustomMapping( new StringColumn( "Name" ) );
            custom.WithReader( ( Action<object, object> ) null );
            custom.WithWriter( ( Action<object, object[]> ) null );
            custom.WithWriter( ( Func<object, object> ) null );
            custom.WithWriter( ( e, values ) => values[0] = ((Outer) e).Name );
            var text = new StringWriter();

            mapper.Write( text, [ new Outer { Name = "Ann" } ], new DelimitedOptions { RecordSeparator = "\n" } );

            Assert.AreEqual( "Ann\n", text.ToString() );
        }

        [TestMethod]
        public async Task TestInjectors_WithAPredicate_ChooseByTheEntityAsWellAsItsType()
        {
            var big = DelimitedTypeMapper.Define<Inner>();
            big.Property( i => i.Id ).ColumnName( "big" );
            var small = DelimitedTypeMapper.Define<Inner>();
            small.Property( i => i.Id ).ColumnName( "small" );
            var injector = new DelimitedTypeMapperInjector();
            injector.When<Inner>( i => i.Id > 9 ).Use( big );
            injector.WithDefault( small );
            var text = new StringWriter();
            var writer = injector.GetWriter( text, new DelimitedOptions { RecordSeparator = "\n" } );

            writer.Write( new Inner { Id = 1 } );
            writer.Write( new Inner { Id = 10 } );
            await writer.WriteSchemaAsync();
            await writer.WriteSchemaAsync();

            Assert.AreEqual( "1\n10\n", text.ToString() );
            Assert.ThrowsExactly<InvalidCastException>( () => injector.GetWriter( new StringWriter() ).Write( "not an Inner" ), "An entity no matcher accepts goes to the default, whose mapper cannot cast it." );

            var fixedBig = FixedLengthTypeMapper.Define<Inner>();
            fixedBig.Property( i => i.Id, 4 );
            var fixedSmall = FixedLengthTypeMapper.Define<Inner>();
            fixedSmall.Property( i => i.Id, 2 );
            var fixedInjector = new FixedLengthTypeMapperInjector();
            fixedInjector.When<Inner>( i => i.Id > 9 ).Use( fixedBig );
            fixedInjector.WithDefault( fixedSmall );
            var fixedText = new StringWriter();
            var fixedWriter = fixedInjector.GetWriter( fixedText, new FixedLengthOptions { RecordSeparator = "\n" } );
            fixedWriter.Write( new Inner { Id = 1 } );
            fixedWriter.Write( new Inner { Id = 10 } );
            await fixedWriter.WriteSchemaAsync();
            await fixedWriter.WriteSchemaAsync();
            fixedInjector.WithDefault<Inner>( null );

            Assert.AreEqual( "1 \n10  \n", fixedText.ToString() );
            Assert.ThrowsExactly<FlatFileException>( () => fixedInjector.GetWriter( new StringWriter() ).Write( new Inner { Id = 1 } ), "With the default cleared, a small id has nowhere to go." );
        }

        [TestMethod]
        public void TestWindow_RejectsValuesOutsideTheEnumerations()
        {
            var window = new Window( 3 );

            Assert.ThrowsExactly<ArgumentException>( () => window.Alignment = (FixedAlignment) 99 );
            Assert.ThrowsExactly<ArgumentException>( () => window.TruncationPolicy = (OverflowTruncationPolicy) 99 );
            window.Alignment = null;
            window.TruncationPolicy = null;
            Assert.IsNull( window.Alignment );
            Assert.IsNull( window.TruncationPolicy );
        }

        [TestMethod]
        public void TestTypeMapperSelectors_ClearingTheDefault()
        {
            var mapper = DelimitedTypeMapper.Define<Inner>();
            mapper.Property( i => i.Id );
            var selector = new DelimitedTypeMapperSelector();
            selector.WithDefault( mapper );
            selector.WithDefault<Inner>( null );
            var reader = selector.GetReader( new StringReader( "1\n" ) );
            reader.RecordError += ( _, e ) => e.IsHandled = true;

            Assert.IsFalse( reader.Read(), "With no default the record is reported and, the report handled, skipped; nothing is left to read." );

            var fixedMapper = FixedLengthTypeMapper.Define<Inner>();
            fixedMapper.Property( i => i.Id, 2 );
            var fixedSelector = new FixedLengthTypeMapperSelector();
            fixedSelector.WithDefault( fixedMapper );
            fixedSelector.WithDefault<Inner>( null );
            Assert.ThrowsExactly<RecordProcessingException>( () => fixedSelector.GetReader( new StringReader( "1 \n" ) ).Read() );
        }

        [TestMethod]
        public void TestMemberAccessorBuilder_PropertyLookupsOnOtherTypes()
        {
            Assert.IsNull( MemberAccessorBuilder.GetProperty<Outer, int>( o => o.Field ), "A field is not a property." );
            Assert.IsNull( MemberAccessorBuilder.GetProperty<Outer, int>( o => o.Inner.Id ), "A property declared on another type is not the entity's." );
            Assert.IsNotNull( MemberAccessorBuilder.GetProperty<Outer, string>( o => o.Name ) );
            Assert.IsNull( MemberAccessorBuilder.GetMethod<Outer>( o => Console.WriteLine( o ) ), "A method declared on another type is not the entity's." );
            Assert.IsNull( MemberAccessorBuilder.GetMethod<Outer>( _ => new Outer() ), "A construction is not a method call." );
            Assert.AreEqual( nameof( object.ToString ), MemberAccessorBuilder.GetMethod<Outer>( o => o.ToString() )!.Name );
        }
    }
}
