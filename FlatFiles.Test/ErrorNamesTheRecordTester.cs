using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     An error has to name the record it is about.
    /// </summary>
    /// <remarks>
    ///     The delimited reader kept the last record's context in a field and handed it to every error raised
    ///     before the next record had one of its own, so a bad third record was reported as the second - by
    ///     number, by text and by values. Only the first record in a file was ever named correctly, because
    ///     until then there was nothing stale to answer with.
    ///     <para>
    ///         Two errors are raised that way: a record the parser cannot read at all, and a record no schema
    ///         matches. The second passed the record to the method that built the context and had it ignored,
    ///         which is what makes it clear this was never the intent.
    ///     </para>
    /// </remarks>
    [TestClass]
    public class ErrorNamesTheRecordTester
    {
        private static string Lines( params string[] records )
        {
            return string.Join( Environment.NewLine, records );
        }

        private static DelimitedSchema TwoColumns()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "one" ) ).AddColumn( new StringColumn( "two" ) );
            return schema;
        }

        /// <summary>A record the parser cannot read, with two good ones in front of it.</summary>
        [TestMethod]
        public void TestUnreadableRecord_TheErrorNamesIt()
        {
            // The third record opens a quote it never closes.
            var text = Lines( "a,b", "c,d", "e,\"f" );
            var reader = new DelimitedReader( new StringReader( text ), TwoColumns(),
                new DelimitedOptions { PreserveRecordText = true } );

            Assert.IsTrue( reader.Read() );
            Assert.IsTrue( reader.Read() );

            var exception = Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );
            var context = exception.RecordContext;

            Assert.AreEqual( 3, context.PhysicalRecordNumber, "the third record is the one that could not be read" );
            // There is no text to report, because the parser never managed to read any. What matters is that it
            // is not the record before it, which is what used to be reported here.
            Assert.AreNotEqual( "c,d", context.Record );
        }

        /// <summary>A record no schema matches, with two matching ones in front of it.</summary>
        [TestMethod]
        public void TestUnmatchedRecord_TheErrorNamesIt()
        {
            var schema = TwoColumns();
            var selector = new DelimitedSchemaSelector();
            selector.When( values => values[0] == "A" ).Use( schema );

            var text = Lines( "A,one", "A,two", "Z,three" );
            var reader = new DelimitedReader( new StringReader( text ), selector,
                new DelimitedOptions { PreserveRecordText = true } );

            var errors = new List<IRecordContext>();
            reader.RecordError += ( _, e ) =>
            {
                errors.Add( e.RecordContext );
                e.IsHandled = true;
            };

            while (reader.Read())
            {
            }

            Assert.HasCount( 1, errors );
            Assert.AreEqual( 3, errors[0].PhysicalRecordNumber );
            Assert.AreEqual( "Z,three", errors[0].Record );
        }

        /// <summary>
        ///     The first record in a file was always named correctly, there being nothing stale to answer with.
        ///     It still has to be.
        /// </summary>
        [TestMethod]
        public void TestUnmatchedFirstRecord_TheErrorStillNamesIt()
        {
            var selector = new DelimitedSchemaSelector();
            selector.When( values => values[0] == "A" ).Use( TwoColumns() );

            var reader = new DelimitedReader( new StringReader( Lines( "Z,one", "A,two" ) ), selector,
                new DelimitedOptions { PreserveRecordText = true } );

            var errors = new List<IRecordContext>();
            reader.RecordError += ( _, e ) =>
            {
                errors.Add( e.RecordContext );
                e.IsHandled = true;
            };

            while (reader.Read())
            {
            }

            Assert.HasCount( 1, errors );
            Assert.AreEqual( 1, errors[0].PhysicalRecordNumber );
            Assert.AreEqual( "Z,one", errors[0].Record );
        }

        /// <summary>
        ///     A handler for a record being read is told which record it is. The fixed-length reader raises that
        ///     event before a schema has been chosen, so the context carries no schema - but it has to carry the
        ///     record number, or a handler that skips a line cannot say which line it skipped.
        /// </summary>
        [TestMethod]
        public void TestFixedLengthRecordRead_KnowsWhichRecord()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "one" ), new Window( 3 ) );

            var reader = new FixedLengthReader( new StringReader( Lines( "aaa", "bbb", "ccc" ) ), schema,
                new FixedLengthOptions { RecordSeparator = Environment.NewLine } );

            var seen = new List<string>();
            reader.RecordRead += ( _, e ) =>
            {
                seen.Add( $"{e.RecordContext.PhysicalRecordNumber}:{e.Record}" );
                e.IsSkipped = e.Record == "bbb";
            };

            var read = new List<string>();
            while (reader.Read())
            {
                read.Add( (string) reader.GetValues()[0]! );
            }

            Assert.AreEqual( "1:aaa|2:bbb|3:ccc", string.Join( "|", seen ) );
            // The skipped record is gone from the values, and skipping it did not disturb the numbering above.
            Assert.AreEqual( "aaa|ccc", string.Join( "|", read ) );
        }
    }
}
