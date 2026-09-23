using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Drives the members of the readers, writers and their typed wrappers that the feature tests do not reach:
    ///     event subscription and removal, the schema and options accessors, the error paths that end in a handler or
    ///     a rethrow, the reading-after-an-error refusal, the raw and schema writes, and the cancelled-token overloads.
    /// </summary>
    [TestClass]
    public class ReaderWriterSurfaceTester
    {
        public sealed class Header
        {
            public string Batch { get; set; }
        }

        public sealed class Detail
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private static readonly CancellationToken Cancelled = new( true );

        private static IDelimitedTypeMapper<Header> HeaderMapper()
        {
            var mapper = DelimitedTypeMapper.Define<Header>();
            mapper.Property( h => h.Batch );
            return mapper;
        }

        private static IDelimitedTypeMapper<Detail> DetailMapper()
        {
            var mapper = DelimitedTypeMapper.Define<Detail>();
            mapper.Property( d => d.Id );
            mapper.Property( d => d.Name );
            return mapper;
        }

        private static IFixedLengthTypeMapper<Header> FixedHeaderMapper()
        {
            var mapper = FixedLengthTypeMapper.Define<Header>();
            mapper.Property( h => h.Batch, 10 );
            return mapper;
        }

        private static IFixedLengthTypeMapper<Detail> FixedDetailMapper()
        {
            var mapper = FixedLengthTypeMapper.Define<Detail>();
            mapper.Property( d => d.Id, 3 );
            mapper.Property( d => d.Name, 7 );
            return mapper;
        }

        [TestMethod]
        public async Task TestDelimitedSelectorReader_ExposesEventsSchemaAndSkip()
        {
            var selector = new DelimitedTypeMapperSelector();
            selector.When( v => v.Length == 1 ).Use( HeaderMapper() );
            selector.WithDefault( DetailMapper() );
            var reader = selector.GetReader( new StringReader( "B1\n1,Ann\n2,Bob\n3,Cid\n" ), new DelimitedOptions { RecordSeparator = "\n" } );
            int read = 0, parsed = 0, typedParsed = 0, errors = 0, columnErrors = 0;
            EventHandler<DelimitedRecordReadEventArgs> onRead = ( _, _ ) => read++;
            EventHandler<DelimitedRecordParsedEventArgs> onParsed = ( _, _ ) => parsed++;
            EventHandler<IRecordParsedEventArgs> onTypedParsed = ( _, _ ) => typedParsed++;
            EventHandler<RecordErrorEventArgs> onError = ( _, _ ) => errors++;
            EventHandler<ColumnErrorEventArgs> onColumnError = ( _, _ ) => columnErrors++;
            reader.RecordRead += onRead;
            reader.RecordParsed += onParsed;
            ((ITypedReader<object>) reader).RecordParsed += onTypedParsed;
            reader.RecordError += onError;
            reader.ColumnError += onColumnError;

            Assert.IsNull( reader.GetSchema(), "A selector-based reader has no single schema." );
            Assert.IsNull( ((ITypedReader<object>) reader).GetSchema() );
            Assert.IsNotNull( reader.Reader );
            Assert.IsNotNull( ((ITypedReader<object>) reader).Reader );
            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( "B1", ((Header) reader.Current).Batch );
            Assert.IsTrue( reader.Skip() );
            Assert.IsTrue( await reader.ReadAsync() );
            Assert.AreEqual( "Bob", ((Detail) reader.Current).Name );
            Assert.IsTrue( await reader.SkipAsync() );
            Assert.IsFalse( await reader.ReadAsync( CancellationToken.None ) );

            reader.RecordRead -= onRead;
            reader.RecordParsed -= onParsed;
            ((ITypedReader<object>) reader).RecordParsed -= onTypedParsed;
            reader.RecordError -= onError;
            reader.ColumnError -= onColumnError;
            Assert.AreEqual( 2, read, "A skipped record raises no event; only the records returned are read and parsed." );
            Assert.AreEqual( 2, parsed );
            Assert.AreEqual( 2, typedParsed );
            Assert.AreEqual( 0, errors + columnErrors );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( async () => await reader.ReadAsync( Cancelled ) );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( async () => await reader.SkipAsync( Cancelled ) );
        }

        [TestMethod]
        public async Task TestFixedLengthSelectorReader_ExposesEventsSchemaAndSkip()
        {
            var selector = new FixedLengthTypeMapperSelector();
            selector.When( r => r.StartsWith( 'B' ) ).Use( FixedHeaderMapper() );
            selector.WithDefault( FixedDetailMapper() );
            var reader = selector.GetReader( new StringReader( "B1        \n1  Ann    \n2  Bob    \n3  Cid    \n" ), new FixedLengthOptions { RecordSeparator = "\n" } );
            int read = 0, partitioned = 0, parsed = 0, typedParsed = 0;
            EventHandler<FixedLengthRecordReadEventArgs> onRead = ( _, _ ) => read++;
            EventHandler<FixedLengthRecordPartitionedEventArgs> onPartitioned = ( _, _ ) => partitioned++;
            EventHandler<FixedLengthRecordParsedEventArgs> onParsed = ( _, _ ) => parsed++;
            EventHandler<IRecordParsedEventArgs> onTypedParsed = ( _, _ ) => typedParsed++;
            EventHandler<RecordErrorEventArgs> onError = ( _, _ ) => { };
            EventHandler<ColumnErrorEventArgs> onColumnError = ( _, _ ) => { };
            reader.RecordRead += onRead;
            reader.RecordPartitioned += onPartitioned;
            reader.RecordParsed += onParsed;
            ((ITypedReader<object>) reader).RecordParsed += onTypedParsed;
            reader.RecordError += onError;
            reader.ColumnError += onColumnError;

            Assert.IsNull( reader.GetSchema() );
            Assert.IsNull( ((ITypedReader<object>) reader).GetSchema() );
            Assert.IsNotNull( reader.Reader );
            Assert.IsNotNull( ((ITypedReader<object>) reader).Reader );
            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( "B1", ((Header) reader.Current).Batch );
            Assert.IsTrue( reader.Skip() );
            Assert.IsTrue( await reader.ReadAsync() );
            Assert.AreEqual( 2, ((Detail) reader.Current).Id );
            Assert.IsTrue( await reader.SkipAsync() );
            Assert.IsFalse( reader.Read() );

            reader.RecordRead -= onRead;
            reader.RecordPartitioned -= onPartitioned;
            reader.RecordParsed -= onParsed;
            ((ITypedReader<object>) reader).RecordParsed -= onTypedParsed;
            reader.RecordError -= onError;
            reader.ColumnError -= onColumnError;
            Assert.AreEqual( 2, read, "A skipped record raises no event." );
            Assert.AreEqual( 2, partitioned );
            Assert.AreEqual( 2, parsed );
            Assert.AreEqual( 2, typedParsed );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( async () => await reader.ReadAsync( Cancelled ) );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( async () => await reader.SkipAsync( Cancelled ) );
        }

        [TestMethod]
        public void TestSelectorReaders_RecordWithNoMatchAndNoDefault_IsAnErrorTheHandlerCanSkip()
        {
            var delimited = new DelimitedTypeMapperSelector();
            delimited.When( v => v.Length == 1 ).Use( HeaderMapper() );
            var reader = delimited.GetReader( new StringReader( "B1\n1,Ann\nB2\n" ), new DelimitedOptions { RecordSeparator = "\n" } );
            var skipped = 0;
            reader.RecordError += ( _, e ) => { skipped++; e.IsHandled = true; };

            var batches = reader.ReadAll().Cast<Header>().Select( h => h.Batch ).ToList();

            Assert.AreEqual( 1, skipped, "The unmatched record is reported once." );
            CollectionAssert.AreEqual( new[] { "B1", "B2" }, batches, "A handled report skips the record, as it does for every other record error." );

            var fixedLength = new FixedLengthTypeMapperSelector();
            fixedLength.When( r => r.StartsWith( 'B' ) ).Use( FixedHeaderMapper() );
            var strict = fixedLength.GetReader( new StringReader( "B1        \n1  Ann    \n" ), new FixedLengthOptions { RecordSeparator = "\n" } );

            Assert.IsTrue( strict.Read() );
            Assert.ThrowsExactly<RecordProcessingException>( () => strict.Read(), "With no handler and no default the record is refused." );
            Assert.ThrowsExactly<InvalidOperationException>( () => strict.Read(), "After an unhandled error the reader refuses to continue." );
            Assert.ThrowsExactly<InvalidOperationException>( () => strict.Skip() );
        }

        [TestMethod]
        public async Task TestReaders_AfterAnUnhandledError_RefuseEveryEntryPoint()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new Int32Column( "n" ) );
            var reader = new DelimitedReader( new StringReader( "x\n1\n" ), schema, new DelimitedOptions { RecordSeparator = "\n" } );
            Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );
            Assert.ThrowsExactly<InvalidOperationException>( () => reader.Read() );
            Assert.ThrowsExactly<InvalidOperationException>( () => reader.Skip() );
            await Assert.ThrowsExactlyAsync<InvalidOperationException>( async () => await reader.ReadAsync() );
            await Assert.ThrowsExactlyAsync<InvalidOperationException>( async () => await reader.SkipAsync() );

            var fixedSchema = new FixedLengthSchema();
            fixedSchema.AddColumn( new Int32Column( "n" ), 1 );
            var fixedReader = new FixedLengthReader( new StringReader( "x\n1\n" ), fixedSchema, new FixedLengthOptions { RecordSeparator = "\n" } );
            Assert.ThrowsExactly<RecordProcessingException>( () => fixedReader.Read() );
            Assert.ThrowsExactly<InvalidOperationException>( () => fixedReader.Read() );
            Assert.ThrowsExactly<InvalidOperationException>( () => fixedReader.Skip() );
            await Assert.ThrowsExactlyAsync<InvalidOperationException>( async () => await fixedReader.ReadAsync() );
            await Assert.ThrowsExactlyAsync<InvalidOperationException>( async () => await fixedReader.SkipAsync() );
        }

        [TestMethod]
        public async Task TestReaders_SchemaAndOptionsAccessors()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            var options = new DelimitedOptions { Separator = ";" };
            IReader reader = new DelimitedReader( new StringReader( "x\n" ), schema, options );
            Assert.AreSame( schema, reader.GetSchema() );
            Assert.AreSame( schema, await reader.GetSchemaAsync() );
            Assert.AreEqual( ";", ((DelimitedOptions) reader.Options).Separator );
            var parsed = 0;
            EventHandler<IRecordParsedEventArgs> onParsed = ( _, _ ) => parsed++;
            reader.RecordParsed += onParsed;
            Assert.IsTrue( reader.Read() );
            reader.RecordParsed -= onParsed;
            Assert.IsFalse( reader.Read() );
            Assert.AreEqual( 1, parsed, "Removing the handler through the interface event works." );

            var fixedSchema = new FixedLengthSchema();
            fixedSchema.AddColumn( new StringColumn( "a" ), 1 );
            var fixedOptions = new FixedLengthOptions { FillCharacter = '.' };
            IReader fixedReader = new FixedLengthReader( new StringReader( "x\n" ), fixedSchema, fixedOptions );
            Assert.AreSame( fixedSchema, fixedReader.GetSchema() );
            Assert.AreSame( fixedSchema, await fixedReader.GetSchemaAsync() );
            Assert.AreSame( fixedSchema, await fixedReader.GetSchemaAsync( CancellationToken.None ) );
            Assert.AreEqual( '.', ((FixedLengthOptions) fixedReader.Options).FillCharacter );
            var fixedParsed = 0;
            EventHandler<IRecordParsedEventArgs> onFixedParsed = ( _, _ ) => fixedParsed++;
            fixedReader.RecordParsed += onFixedParsed;
            Assert.IsTrue( fixedReader.Read() );
            fixedReader.RecordParsed -= onFixedParsed;
            Assert.AreEqual( 1, fixedParsed );

            var selector = new FixedLengthSchemaSelector();
            selector.WithDefault( fixedSchema );
            IReader selected = new FixedLengthReader( new StringReader( "x\n" ), selector );
            Assert.IsNull( selected.GetSchema() );
            Assert.IsNull( await selected.GetSchemaAsync() );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( async () => await fixedReader.GetSchemaAsync( Cancelled ) );
        }

        [TestMethod]
        public void TestDelimitedReader_SeparatorEqualToRecordSeparator_IsRefused()
        {
            var options = new DelimitedOptions { Separator = "\n", RecordSeparator = "\n" };

            Assert.ThrowsExactly<ArgumentException>( () => new DelimitedReader( new StringReader( "a\nb" ), options ) );
        }

        [TestMethod]
        public async Task TestWriters_SchemaRawAndErrorPaths()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            schema.AddColumn( new Int32Column( "b" ) );
            var text = new StringWriter();
            IWriter writer = new DelimitedWriter( text, schema, new DelimitedOptions { RecordSeparator = "\n" } );
            var columnErrors = 0;
            EventHandler<ColumnErrorEventArgs> onColumnError = ( _, _ ) => columnErrors++;
            writer.ColumnError += onColumnError;
            writer.ColumnError -= onColumnError;
            var handled = 0;
            writer.RecordError += ( _, e ) => { handled++; e.IsHandled = true; };

            Assert.AreSame( schema, writer.GetSchema() );
            writer.WriteSchema();
            await writer.WriteSchemaAsync();
            writer.Write( [ "x", 1 ] );
            writer.Write( [ "too few" ] );
            await writer.WriteAsync( [ "too", "many", 3 ] );
            writer.WriteRaw( "raw", true );
            await writer.WriteRawAsync( "more", false );

            Assert.AreEqual( "a,b\nx,1\nraw\nmore", text.ToString(), "The schema is written once however often it is asked for." );
            Assert.AreEqual( 2, handled, "A record with the wrong number of values is a record error the handler can absorb." );
            Assert.AreEqual( 0, columnErrors );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( () => writer.WriteAsync( [ "x", 1 ], Cancelled ) );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( () => writer.WriteSchemaAsync( Cancelled ) );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( () => writer.WriteRawAsync( "x", false, Cancelled ) );
        }

        [TestMethod]
        public async Task TestFixedLengthWriter_SchemaRawAndErrorPaths()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "a" ), 2 );
            schema.AddColumn( new Int32Column( "b" ), 2 );
            var text = new StringWriter();
            IWriter writer = new FixedLengthWriter( text, schema, new FixedLengthOptions { RecordSeparator = "\n" } );
            var columnErrors = 0;
            EventHandler<ColumnErrorEventArgs> onColumnError = ( _, _ ) => columnErrors++;
            writer.ColumnError += onColumnError;
            writer.ColumnError -= onColumnError;

            Assert.AreSame( schema, writer.GetSchema() );
            writer.WriteSchema();
            await writer.WriteSchemaAsync();
            writer.Write( [ "x", 1 ] );
            writer.WriteRaw( "raw", true );
            await writer.WriteRawAsync( "more", false );
            Assert.ThrowsExactly<RecordProcessingException>( () => writer.Write( [ "too few" ] ), "Without a handler the wrong number of values throws." );
            var handled = 0;
            writer.RecordError += ( _, e ) => { handled++; e.IsHandled = true; };
            writer.Write( [ "too few" ] );
            await writer.WriteAsync( [ 1, 2, 3 ] );

            Assert.AreEqual( "a b \nx 1 \nraw\nmore", text.ToString(), "The schema is written once however often it is asked for." );
            Assert.AreEqual( 2, handled );
            Assert.AreEqual( 0, columnErrors );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( () => writer.WriteAsync( [ "x", 1 ], Cancelled ) );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( () => writer.WriteSchemaAsync( Cancelled ) );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( () => writer.WriteRawAsync( "x", false, Cancelled ) );
        }

        [TestMethod]
        public async Task TestInjectorWriters_HaveNoSchemaAndForwardEverything()
        {
            var injector = new DelimitedTypeMapperInjector();
            injector.When<Header>().Use( HeaderMapper() );
            injector.WithDefault( DetailMapper() );
            var text = new StringWriter();
            var writer = injector.GetWriter( text, new DelimitedOptions { RecordSeparator = "\n" } );
            var columnErrors = 0;
            var recordErrors = 0;
            EventHandler<ColumnErrorEventArgs> onColumn = ( _, _ ) => columnErrors++;
            EventHandler<RecordErrorEventArgs> onRecord = ( _, _ ) => recordErrors++;
            writer.ColumnError += onColumn;
            writer.RecordError += onRecord;

            Assert.IsNull( writer.GetSchema(), "An injector-based writer has no single schema." );
            Assert.IsNotNull( writer.Writer );
            writer.WriteSchema();
            await writer.WriteSchemaAsync();
            writer.Write( new Header { Batch = "B1" } );
            await writer.WriteAsync( new Detail { Id = 1, Name = "Ann" } );
            await writer.WriteAsync( new Detail { Id = 2, Name = "Bob" }, CancellationToken.None );
            writer.ColumnError -= onColumn;
            writer.RecordError -= onRecord;

            Assert.AreEqual( "B1\n1,Ann\n2,Bob\n", text.ToString(), "Writing the schema of an injector writes nothing." );
            Assert.AreEqual( 0, columnErrors + recordErrors );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( () => writer.WriteAsync( new Header(), Cancelled ) );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( () => writer.WriteSchemaAsync( Cancelled ) );

            var fixedInjector = new FixedLengthTypeMapperInjector();
            fixedInjector.When<Header>().Use( FixedHeaderMapper() );
            fixedInjector.WithDefault( FixedDetailMapper() );
            var fixedText = new StringWriter();
            var fixedWriter = fixedInjector.GetWriter( fixedText, new FixedLengthOptions { RecordSeparator = "\n" } );
            fixedWriter.WriteSchema();
            fixedWriter.Write( new Header { Batch = "B1" } );
            await fixedWriter.WriteAsync( new Detail { Id = 1, Name = "Ann" } );
            Assert.AreEqual( "B1        \n1  Ann    \n", fixedText.ToString() );
            Assert.IsNull( fixedWriter.GetSchema() );
        }

        [TestMethod]
        public async Task TestTypedReaderAndWriter_ForwardEventsSchemaAndCancellation()
        {
            var mapper = DetailMapper();
            var options = new DelimitedOptions { RecordSeparator = "\n", IsFirstRecordSchema = true };
            var reader = mapper.GetReader( new StringReader( "Id,Name\n1,Ann\n2,Bob\n3,Cid\n" ), options );
            var parsed = 0;
            var errors = 0;
            var columnErrors = 0;
            EventHandler<IRecordParsedEventArgs> onParsed = ( _, _ ) => parsed++;
            EventHandler<RecordErrorEventArgs> onError = ( _, _ ) => errors++;
            EventHandler<ColumnErrorEventArgs> onColumnError = ( _, _ ) => columnErrors++;
            ((ITypedReader<Detail>) reader).RecordParsed += onParsed;
            reader.RecordError += onError;
            reader.ColumnError += onColumnError;

            Assert.IsNotNull( reader.GetSchema() );
            Assert.IsNotNull( reader.Reader );
            Assert.IsTrue( reader.Skip() );
            Assert.IsTrue( await reader.ReadAsync() );
            Assert.AreEqual( "Bob", reader.Current.Name );
            Assert.IsTrue( await reader.SkipAsync() );
            Assert.IsFalse( reader.Read() );
            ((ITypedReader<Detail>) reader).RecordParsed -= onParsed;
            reader.RecordError -= onError;
            reader.ColumnError -= onColumnError;
            Assert.AreEqual( 1, parsed );
            Assert.AreEqual( 0, errors + columnErrors );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( async () => await reader.ReadAsync( Cancelled ) );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( async () => await reader.SkipAsync( Cancelled ) );

            var text = new StringWriter();
            var writer = mapper.GetWriter( text, options );
            writer.ColumnError += onColumnError;
            writer.RecordError += onError;
            Assert.IsNotNull( writer.GetSchema() );
            Assert.IsNotNull( writer.Writer );
            writer.WriteSchema();
            await writer.WriteSchemaAsync();
            writer.Write( new Detail { Id = 1, Name = "Ann" } );
            await writer.WriteAsync( new Detail { Id = 2, Name = "Bob" } );
            writer.ColumnError -= onColumnError;
            writer.RecordError -= onError;
            Assert.AreEqual( "Id,Name\n1,Ann\n2,Bob\n", text.ToString() );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( () => writer.WriteAsync( new Detail(), Cancelled ) );
            await Assert.ThrowsExactlyAsync<OperationCanceledException>( () => writer.WriteSchemaAsync( Cancelled ) );

            var fixedReader = FixedDetailMapper().GetReader( new StringReader( "1  Ann    \n" ), new FixedLengthOptions { RecordSeparator = "\n" } );
            Assert.IsNotNull( fixedReader.GetSchema() );
            Assert.IsNotNull( fixedReader.Reader );
            Assert.IsTrue( fixedReader.Read() );
            Assert.AreEqual( "Ann", fixedReader.Current.Name );
        }

        [TestMethod]
        public void TestSchema_GetOrdinal_AndContextlessErrorPaths()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "a" ) );
            schema.AddColumn( new Int32Column( "b" ) { OnParsed = ( _, v ) => v } );
            Assert.AreEqual( 1, schema.GetOrdinal( "b" ) );
            Assert.AreEqual( -1, schema.GetOrdinal( "missing" ) );

            // A parse error on a column that carries a context, with and without a handler.
            var withHandler = new DelimitedReader( new StringReader( "x,notanumber\n" ), schema, new DelimitedOptions { RecordSeparator = "\n" } );
            withHandler.ColumnError += ( _, e ) => { e.IsHandled = true; e.Substitution = 0; };
            Assert.IsTrue( withHandler.Read() );
            Assert.AreEqual( 0, withHandler.GetValues()[1] );
            var withoutHandler = new DelimitedReader( new StringReader( "x,notanumber\n" ), schema, new DelimitedOptions { RecordSeparator = "\n" } );
            Assert.ThrowsExactly<RecordProcessingException>( () => withoutHandler.Read() );

            // With the context disabled, a parse or format error is a ColumnProcessingException built without one.
            var disabled = new DelimitedOptions { RecordSeparator = "\n", IsColumnContextDisabled = true };
            var contextless = new DelimitedReader( new StringReader( "x,notanumber\n" ), schema, disabled );
            var exception = Assert.ThrowsExactly<RecordProcessingException>( () => contextless.Read() );
            Assert.IsInstanceOfType<ColumnProcessingException>( exception.InnerException );
            var text = new StringWriter();
            var writer = new DelimitedWriter( text, schema, disabled );
            writer.Write( [ "x", 1 ] );
            Assert.AreEqual( "x,1\n", text.ToString() );
            Assert.ThrowsExactly<RecordProcessingException>( () => writer.Write( [ "x", "not an int" ] ) );
            var hooked = new DelimitedWriter( new StringWriter(), schema, new DelimitedOptions { RecordSeparator = "\n" } );
            Assert.ThrowsExactly<RecordProcessingException>( () => hooked.Write( [ "x", "not an int" ] ), "A format error on a column with a context and no handler is rethrown." );
        }

        [TestMethod]
        public void TestRecordNumberColumn_AccessorsAndContextRequirement()
        {
            var column = new RecordNumberColumn( "n" )
            {
                FormatProvider = System.Globalization.CultureInfo.InvariantCulture,
                NumberStyles = System.Globalization.NumberStyles.Integer,
                OutputFormat = "D3"
            };

            Assert.AreEqual( System.Globalization.CultureInfo.InvariantCulture, column.FormatProvider );
            Assert.AreEqual( System.Globalization.NumberStyles.Integer, column.NumberStyles );
            Assert.AreEqual( "D3", column.OutputFormat );
            Assert.ThrowsExactly<FlatFileException>( () => column.Format( null, 1 ), "A metadata column cannot be formatted without a record context." );
            Assert.ThrowsExactly<FlatFileException>( () => column.Parse( null, "1" ) );

            var schema = new DelimitedSchema();
            schema.AddColumn( column );
            schema.AddColumn( new StringColumn( "a" ) );
            var text = new StringWriter();
            var writer = new DelimitedWriter( text, schema, new DelimitedOptions { RecordSeparator = "\n" } );
            writer.Write( [ null, "x" ] );
            writer.Write( [ null, "y" ] );
            Assert.AreEqual( "001,x\n002,y\n", text.ToString(), "The metadata column takes a slot in the values and fills it from the record number." );
        }

        [TestMethod]
        public void TestCustomMapping_EveryReaderAndWriterShape()
        {
            var mapper = DelimitedTypeMapper.Define<Detail>();
            mapper.CustomMapping( new Int32Column( "Id" ) ).WithReader( ( d, v ) => d.Id = (int) v ).WithWriter( d => d.Id );
            mapper.CustomMapping( new StringColumn( "Name" ) ).WithReader( ( _, d, v ) => d.Name = (string) v ).WithWriter( ( d, values ) => values[1] = d.Name );
            var options = new DelimitedOptions { RecordSeparator = "\n" };
            var text = new StringWriter();

            mapper.Write( text, [ new Detail { Id = 1, Name = "Ann" } ], options );
            var read = mapper.Read( new StringReader( text.ToString() ), options ).Single();

            Assert.AreEqual( "1,Ann\n", text.ToString() );
            Assert.AreEqual( 1, read.Id );
            Assert.AreEqual( "Ann", read.Name );

            var contextual = DelimitedTypeMapper.Define<Detail>();
            contextual.CustomMapping( new Int32Column( "Id" ) ).WithReader( d => d.Id ).WithWriter( ( _, d, values ) => values[0] = d.Id );
            contextual.CustomMapping( new StringColumn( "Name" ) ).WithReader( d => d.Name ).WithWriter( ( _, d ) => d.Name.ToUpperInvariant() );
            var upper = new StringWriter();
            contextual.Write( upper, [ new Detail { Id = 2, Name = "Bob" } ], options );
            Assert.AreEqual( "2,BOB\n", upper.ToString() );
            Assert.ThrowsExactly<ArgumentException>( () => contextual.CustomMapping( new StringColumn( "x" ) ).WithReader( d => d.Name.Length ), "Only a member access can be a reader expression." );
            Assert.ThrowsExactly<ArgumentException>( () => mapper.CustomMapping( new StringColumn( " " ) ), "A custom column needs a name." );
        }

        [TestMethod]
        public void TestEmitGenerator_RefusesWhatItCannotMap()
        {
            // A type with no parameterless constructor is built through the constructor it does have, whose
            // parameter names say where its values come from.
            var noConstructor = DelimitedTypeMapper.DefineDynamic( typeof( NoDefaultConstructor ) );
            noConstructor.Int32Property( "Id" );
            var constructed = (NoDefaultConstructor) noConstructor.Read( new StringReader( "1\n" ) ).Single();
            Assert.AreEqual( 1, constructed.Id, "The constructor takes the parsed value." );

            var withFactory = DelimitedTypeMapper.Define( () => new NoDefaultConstructor( 5 ) );
            withFactory.Property( x => x.Id );
            Assert.AreEqual( 1, withFactory.Read( new StringReader( "1\n" ) ).Single().Id, "A factory still says how to build it." );

            // Nothing matches a parameter called something no mapped member is called, and the reader says what
            // it tried rather than reporting a missing default constructor.
            var unmatched = DelimitedTypeMapper.Define<UnmatchedConstructor>();
            unmatched.Property( x => x.Id );
            var failure = Assert.ThrowsExactly<FlatFileException>( () => unmatched.Read( new StringReader( "1\n" ) ).ToList(), "No constructor can be satisfied." );
            StringAssert.Contains( failure.Message, "UnmatchedConstructor", "The message names the type." );
            StringAssert.Contains( failure.Message, "Id", "The message names what was mapped." );

            var readOnly = DelimitedTypeMapper.Define<ReadOnlyEntity>();
            readOnly.Property( x => x.Computed );
            Assert.ThrowsExactly<FlatFileException>( () => readOnly.Read( new StringReader( "1\n" ) ).ToList(), "A read-only property cannot be a target." );

            var writeOnly = DelimitedTypeMapper.DefineDynamic( typeof( WriteOnlyEntity ) );
            writeOnly.Int32Property( "Sink" );
            Assert.ThrowsExactly<FlatFileException>( () => writeOnly.Write( new StringWriter(), [ new WriteOnlyEntity() ] ), "A write-only property cannot be a source." );
        }

        public sealed class NoDefaultConstructor( int id )
        {
            public int Id { get; set; } = id;
        }

        public sealed class UnmatchedConstructor( int somethingElse )
        {
            public int Id { get; set; } = somethingElse;
        }

        public sealed class ReadOnlyEntity
        {
            public int Computed => 1;
        }

        public sealed class WriteOnlyEntity
        {
            public int Sink
            {
                set { }
            }
        }
    }
}
