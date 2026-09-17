using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests that readers and writers hand out one execution context per schema rather than rebuilding
    ///     it, options clone included, for every record.
    /// </summary>
    [TestClass]
    public class ExecutionContextCacheTester
    {
        private static DelimitedSchema Columns( params string[] names )
        {
            var schema = new DelimitedSchema();
            foreach (var name in names)
            {
                schema.AddColumn( new StringColumn( name ) );
            }
            return schema;
        }

        private static List<IExecutionContext> ReadContexts( DelimitedReader reader )
        {
            List<IExecutionContext> contexts = [];
            reader.RecordRead += ( _, e ) => contexts.Add( e.RecordContext.ExecutionContext );
            while (reader.Read())
            {
            }
            return contexts;
        }

        /// <summary>
        ///     With one schema for the whole file, every record sees the same execution context.
        /// </summary>
        [TestMethod]
        public void TestReader_FixedSchema_ReusesOneExecutionContext()
        {
            var reader = new DelimitedReader( new StringReader( "a,b\r\nc,d\r\ne,f\r\n" ), Columns( "x", "y" ) );

            var contexts = ReadContexts( reader );

            Assert.AreEqual( 3, contexts.Count, "Every record should have raised the event." );
            Assert.IsTrue( ReferenceEquals( contexts[0], contexts[1] ), "The second record rebuilt the execution context." );
            Assert.IsTrue( ReferenceEquals( contexts[0], contexts[2] ), "The third record rebuilt the execution context." );
        }

        /// <summary>
        ///     A schema selector rebuilds the context only when consecutive records take different schemas. The
        ///     cache holds a single entry, so switching back to an earlier schema rebuilds again rather than
        ///     recalling it - that is deliberate, and this pins it.
        /// </summary>
        [TestMethod]
        public void TestReader_SchemaSelector_RebuildsOnlyWhenTheSchemaChanges()
        {
            var two = Columns( "x", "y" );
            var three = Columns( "x", "y", "z" );
            var selector = new DelimitedSchemaSelector();
            selector.When( values => values.Length == 2 ).Use( two );
            selector.When( values => values.Length == 3 ).Use( three );
            var reader = new DelimitedReader( new StringReader( "a,b\r\nc,d\r\ne,f,g\r\nh,i\r\n" ), selector );

            var contexts = ReadContexts( reader );

            Assert.AreEqual( 4, contexts.Count, "Every record should have raised the event." );
            Assert.IsTrue( ReferenceEquals( contexts[0], contexts[1] ), "Consecutive records on the same schema should share a context." );
            Assert.IsFalse( ReferenceEquals( contexts[1], contexts[2] ), "A change of schema should produce a new context." );
            Assert.IsFalse( ReferenceEquals( contexts[2], contexts[3] ), "Returning to an earlier schema should produce a new context, not recall the old one." );
            Assert.AreSame( two, contexts[0].Schema, "The first context should carry the two-column schema." );
            Assert.AreSame( three, contexts[2].Schema, "The third context should carry the three-column schema." );
        }

        /// <summary>
        ///     Without a schema the reader infers one from each record's width. Inferred schemas are shared for
        ///     equal widths, so records of the same width share a context and a record of a different width
        ///     takes a new one.
        /// </summary>
        [TestMethod]
        public void TestReader_DynamicSchema_SharesTheContextForEqualWidths()
        {
            var reader = new DelimitedReader( new StringReader( "a,b\r\nc,d\r\ne,f,g\r\n" ) );

            var contexts = ReadContexts( reader );

            Assert.AreEqual( 3, contexts.Count, "Every record should have raised the event." );
            Assert.IsTrue( ReferenceEquals( contexts[0], contexts[1] ), "Two records of the same width should share a context." );
            Assert.IsFalse( ReferenceEquals( contexts[1], contexts[2] ), "A record of a different width takes a different inferred schema, and so a new context." );
        }

        /// <summary>
        ///     The options exposed through the context are a copy, never the caller's own instance, and with a
        ///     fixed schema that copy is made once rather than per record.
        /// </summary>
        [TestMethod]
        public void TestReader_Options_AreCopiedOnceAndAreNeverTheCallers()
        {
            var options = new DelimitedOptions();
            var reader = new DelimitedReader( new StringReader( "a,b\r\nc,d\r\n" ), Columns( "x", "y" ), options );
            List<DelimitedOptions> exposed = [];
            reader.RecordRead += ( _, e ) => exposed.Add( ((IDelimitedRecordContext) e.RecordContext).ExecutionContext.Options );
            while (reader.Read())
            {
            }

            Assert.AreEqual( 2, exposed.Count, "Every record should have raised the event." );
            Assert.IsFalse( ReferenceEquals( options, exposed[0] ), "The context must not expose the caller's own options instance." );
            Assert.IsTrue( ReferenceEquals( exposed[0], exposed[1] ), "The options copy should be made once for the schema, not once per record." );
        }

        /// <summary>
        ///     The fixed-length reader caches in the same way.
        /// </summary>
        [TestMethod]
        public void TestFixedLengthReader_FixedSchema_ReusesOneExecutionContext()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "x" ), new Window( 5 ) );
            var reader = new FixedLengthReader( new StringReader( "aaaaa\r\nbbbbb\r\n" ), schema );
            List<IExecutionContext> contexts = [];
            reader.RecordParsed += ( _, e ) => contexts.Add( e.RecordContext.ExecutionContext );
            while (reader.Read())
            {
            }

            Assert.AreEqual( 2, contexts.Count, "Every record should have raised the event." );
            Assert.IsTrue( ReferenceEquals( contexts[0], contexts[1] ), "The second record rebuilt the execution context." );
        }

        /// <summary>
        ///     The writer's metadata path, which the typed writers call once per record, reuses the context too.
        /// </summary>
        [TestMethod]
        public void TestWriter_Metadata_ReusesOneExecutionContext()
        {
            var writer = new DelimitedWriter( new StringWriter(), Columns( "x", "y" ) );
            var metadata = (IWriterWithMetadata) writer;

            var first = metadata.GetMetadata().ExecutionContext;
            var second = metadata.GetMetadata().ExecutionContext;

            Assert.IsTrue( ReferenceEquals( first, second ), "Two metadata requests on the same schema rebuilt the execution context." );
        }
    }
}
