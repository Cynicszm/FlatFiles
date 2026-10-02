using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     A selector can be told to pass a record over rather than read it.
    /// </summary>
    /// <remarks>
    ///     Without this, a file carrying sections meant for somebody else can only be read by skipping those lines
    ///     from a handler for the record being read - and the two readers raise that event on opposite sides of
    ///     choosing a schema, so the same arrangement does not work for both. The fixed-length reader offers the
    ///     record before it has a schema, and the delimited reader after, by which point a line no schema matches
    ///     has already been reported. Deciding it in the selector decides it the same way in both.
    /// </remarks>
    [TestClass]
    public class SelectorSkipTester
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

        [TestMethod]
        public void TestDelimitedSkip_TheRecordIsNeitherReadNorReported()
        {
            var selector = new DelimitedSchemaSelector();
            selector.When( values => values[0] == "#" ).Skip();
            selector.When( values => values[0] == "A" ).Use( TwoColumns() );

            var reader = new DelimitedReader( new StringReader( Lines( "A,one", "#,theirs", "A,two" ) ), selector );

            var errors = new List<string>();
            reader.RecordError += ( _, e ) =>
            {
                errors.Add( e.Exception.Message );
                e.IsHandled = true;
            };

            var read = new List<string>();
            while (reader.Read())
            {
                read.Add( string.Join( ",", reader.GetValues() ) );
            }

            Assert.AreEqual( "A,one|A,two", string.Join( "|", read ) );
            Assert.IsEmpty( errors, string.Join( Environment.NewLine, errors ) );
        }

        /// <summary>A record nothing matches is still an error; only a record matched by a skip is not.</summary>
        [TestMethod]
        public void TestDelimitedSkip_AnUnmatchedRecordIsStillReported()
        {
            var selector = new DelimitedSchemaSelector();
            selector.When( values => values[0] == "#" ).Skip();
            selector.When( values => values[0] == "A" ).Use( TwoColumns() );

            var reader = new DelimitedReader( new StringReader( Lines( "A,one", "#,theirs", "Z,what" ) ), selector,
                new DelimitedOptions { PreserveRecordText = true } );

            var reported = new List<string?>();
            reader.RecordError += ( _, e ) =>
            {
                reported.Add( e.RecordContext.Record );
                e.IsHandled = true;
            };

            while (reader.Read())
            {
            }

            Assert.HasCount( 1, reported );
            Assert.AreEqual( "Z,what", reported[0] );
        }

        /// <summary>The text form costs neither splitting the record nor copying its values out.</summary>
        [TestMethod]
        public void TestDelimitedSkipText_TheRecordIsPassedOver()
        {
            var selector = new DelimitedSchemaSelector();
            selector.WhenText( text => text.StartsWith( "#" ) ).Skip();
            selector.WhenText( text => text.StartsWith( "A" ) ).Use( TwoColumns() );

            var reader = new DelimitedReader( new StringReader( Lines( "A,one", "#theirs", "A,two" ) ), selector );

            var read = new List<string>();
            while (reader.Read())
            {
                read.Add( string.Join( ",", reader.GetValues() ) );
            }

            Assert.AreEqual( "A,one|A,two", string.Join( "|", read ) );
        }

        /// <summary>
        ///     Predicates are asked in the order they were registered, so a skip registered after a match does not
        ///     take records from it. Worth pinning: the opposite would make the order of two independent-looking
        ///     calls decide what a file reads as.
        /// </summary>
        [TestMethod]
        public void TestDelimitedSkip_RegisteredAfterAMatch_DoesNotTakeFromIt()
        {
            var selector = new DelimitedSchemaSelector();
            selector.When( values => values[0] == "A" ).Use( TwoColumns() );
            selector.When( _ => true ).Skip();

            var reader = new DelimitedReader( new StringReader( Lines( "A,one", "B,two" ) ), selector );

            var read = new List<string>();
            while (reader.Read())
            {
                read.Add( string.Join( ",", reader.GetValues() ) );
            }

            // The first record still matched; only the second reached the skip.
            Assert.AreEqual( "A,one", string.Join( "|", read ) );
        }

        [TestMethod]
        public void TestDelimitedSkip_OnMatchStillRuns()
        {
            var skipped = 0;
            var selector = new DelimitedSchemaSelector();
            selector.When( values => values[0] == "#" ).Skip().OnMatch( () => ++skipped );
            selector.When( values => values[0] == "A" ).Use( TwoColumns() );

            var reader = new DelimitedReader( new StringReader( Lines( "A,one", "#,x", "#,y" ) ), selector );
            while (reader.Read())
            {
            }

            Assert.AreEqual( 2, skipped );
        }

        /// <summary>
        ///     The interfaces carry <c>Skip</c> with a default implementation, which is what lets it be added at
        ///     all: the member is new, and without a default anyone implementing the interface would stop
        ///     compiling - and package validation refuses it outright.
        /// </summary>
        /// <remarks>
        ///     Nothing outside this library implements these, because they are only ever returned and never
        ///     taken as a parameter, so the default is unreachable in practice. It is still what makes the
        ///     addition free, so it is worth one test saying what it does rather than leaving a later reader to
        ///     decide it is dead weight.
        /// </remarks>
        [TestMethod]
        public void TestTheDefaultRefusesRatherThanFailingToCompile()
        {
            IDelimitedSchemaSelectorWhenBuilder delimited = new OutsideDelimitedBuilder();
            IFixedLengthSchemaSelectorWhenBuilder fixedLength = new OutsideFixedLengthBuilder();

            Assert.ThrowsExactly<NotSupportedException>( () => delimited.Skip() );
            Assert.ThrowsExactly<NotSupportedException>( () => fixedLength.Skip() );
        }

        /// <summary>What somebody implementing the interface without knowing about Skip would have.</summary>
        private sealed class OutsideDelimitedBuilder : IDelimitedSchemaSelectorWhenBuilder
        {
            public IDelimitedSchemaSelectorUseBuilder Use( DelimitedSchema schema )
            {
                throw new NotImplementedException();
            }
        }

        private sealed class OutsideFixedLengthBuilder : IFixedLengthSchemaSelectorWhenBuilder
        {
            public IFixedLengthSchemaSelectorUseBuilder Use( FixedLengthSchema schema )
            {
                throw new NotImplementedException();
            }
        }

        [TestMethod]
        public void TestFixedLengthSkip_TheRecordIsNeitherReadNorReported()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "one" ), new Window( 5 ) );

            var selector = new FixedLengthSchemaSelector();
            selector.When( record => record.StartsWith( "#" ) ).Skip();
            selector.When( record => record.StartsWith( "A" ) ).Use( schema );

            var options = new FixedLengthOptions { RecordSeparator = Environment.NewLine };
            var reader = new FixedLengthReader( new StringReader( Lines( "Aone ", "#thrs", "Atwo " ) ), selector, options );

            var errors = new List<string>();
            reader.RecordError += ( _, e ) =>
            {
                errors.Add( e.Exception.Message );
                e.IsHandled = true;
            };

            var read = new List<string>();
            while (reader.Read())
            {
                read.Add( (string) reader.GetValues()[0]! );
            }

            Assert.AreEqual( "Aone|Atwo", string.Join( "|", read ) );
            Assert.IsEmpty( errors, string.Join( Environment.NewLine, errors ) );
        }

        /// <summary>
        ///     The point of the whole thing: one arrangement, written once, reads both shapes of file the same way.
        /// </summary>
        [TestMethod]
        public void TestBothReadersSkipTheSameSections()
        {
            var delimited = new DelimitedSchemaSelector();
            delimited.WhenText( text => text.StartsWith( "#" ) ).Skip();
            delimited.WhenText( text => text.StartsWith( "A" ) ).Use( TwoColumns() );

            var fixedSchema = new FixedLengthSchema();
            fixedSchema.AddColumn( new StringColumn( "one" ), new Window( 1 ) )
                .AddColumn( new StringColumn( "two" ), new Window( 4 ) );
            var fixedLength = new FixedLengthSchemaSelector();
            fixedLength.When( record => record.StartsWith( "#" ) ).Skip();
            fixedLength.When( record => record.StartsWith( "A" ) ).Use( fixedSchema );

            var delimitedReader = new DelimitedReader(
                new StringReader( Lines( "A,one", "#,xxx", "A,two" ) ), delimited );
            var fixedReader = new FixedLengthReader(
                new StringReader( Lines( "Aone ", "#xxxx", "Atwo " ) ), fixedLength,
                new FixedLengthOptions { RecordSeparator = Environment.NewLine } );

            var fromDelimited = new List<string>();
            while (delimitedReader.Read())
            {
                fromDelimited.Add( (string) delimitedReader.GetValues()[1]! );
            }
            var fromFixed = new List<string>();
            while (fixedReader.Read())
            {
                fromFixed.Add( (string) fixedReader.GetValues()[1]! );
            }

            Assert.AreEqual( "one|two", string.Join( "|", fromDelimited ) );
            Assert.AreEqual( "one|two", string.Join( "|", fromFixed ) );
        }
    }
}
