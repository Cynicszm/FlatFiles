using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests what each reader does with a record that carries more or less than its schema asks for: which are
    ///     refused, which are padded, which have their surplus discarded, and that the two readers can now be told
    ///     in the same words.
    /// </summary>
    [TestClass]
    public class RecordLengthRuleTester
    {
        [TestMethod]
        public void TestDelimited_AShortRecordIsRefusedByDefault()
        {
            var reader = new DelimitedReader( new StringReader( "one\r\n" ), Schema(), Options() );

            Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );
        }

        [TestMethod]
        public void TestDelimited_AShortRecordWithAnIgnoredColumnIsReportedRatherThanThrowingOutOfRange()
        {
            // An ignored column takes a value from the record and throws it away, so it counts towards the length a
            // record needs. Until this was counted, a record this short reached the parsing loop and walked off the
            // end of it - an IndexOutOfRangeException no record handler could catch, because it is not a
            // FlatFileException.
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "Name" ) );
            schema.AddColumn( new IgnoredColumn( "Spacer" ) );
            schema.AddColumn( new StringColumn( "Town" ) );
            var reader = new DelimitedReader( new StringReader( "Bob,x\r\n" ), schema, Options() );

            Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );
        }

        [TestMethod]
        public void TestDelimited_AShortRecordCanBePadded()
        {
            var options = Options();
            options.ShortRecordHandling = ShortRecordHandling.Pad;
            var reader = new DelimitedReader( new StringReader( "Bob\r\n" ), Schema(), options );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();
            Assert.AreEqual( "Bob", values[0] );
            Assert.IsNull( values[1], "An empty value is what the column's null handling turns into null." );
        }

        [TestMethod]
        public void TestDelimited_APaddedRecordFillsPastAnIgnoredColumn()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "Name" ) );
            schema.AddColumn( new IgnoredColumn( "Spacer" ) );
            schema.AddColumn( new StringColumn( "Town" ) );
            var options = Options();
            options.ShortRecordHandling = ShortRecordHandling.Pad;
            var reader = new DelimitedReader( new StringReader( "Bob\r\n" ), schema, options );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();
            Assert.AreEqual( 2, values.Length, "An ignored column takes a value but yields none." );
            Assert.AreEqual( "Bob", values[0] );
            Assert.IsNull( values[1] );
        }

        [TestMethod]
        public void TestDelimited_APaddedRecordReachesAHandlerPadded()
        {
            // A handler is given the values as an array it may write into, and those have to be the same values the
            // schema goes on to parse.
            var options = Options();
            options.ShortRecordHandling = ShortRecordHandling.Pad;
            var reader = new DelimitedReader( new StringReader( "Bob\r\n" ), Schema(), options );
            string[]? seen = null;
            reader.RecordRead += ( _, e ) => seen = e.Values;

            Assert.IsTrue( reader.Read() );
            Assert.IsNotNull( seen );
            Assert.AreEqual( 2, seen.Length );
            Assert.AreEqual( string.Empty, seen[1] );
        }

        [TestMethod]
        public void TestDelimited_ALongRecordIsAcceptedByDefault()
        {
            var reader = new DelimitedReader( new StringReader( "Bob,Leeds,surplus\r\n" ), Schema(), Options() );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();
            Assert.AreEqual( 2, values.Length );
            Assert.AreEqual( "Leeds", values[1] );
        }

        [TestMethod]
        public void TestDelimited_ALongRecordCanBeRefused()
        {
            var options = Options();
            options.LongRecordHandling = LongRecordHandling.Refuse;
            var reader = new DelimitedReader( new StringReader( "Bob,Leeds,surplus\r\n" ), Schema(), options );

            Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );
        }

        [TestMethod]
        public void TestDelimited_ARefusedLongRecordReachesTheErrorHandler()
        {
            // The point of the option is that the record becomes visible, and a handler can carry on past it.
            var options = Options();
            options.LongRecordHandling = LongRecordHandling.Refuse;
            var reader = new DelimitedReader( new StringReader( "Bob,Leeds,surplus\r\nSue,York\r\n" ), Schema(), options );
            List<int> errored = [];
            reader.RecordError += ( _, e ) =>
            {
                errored.Add( e.RecordContext.PhysicalRecordNumber );
                e.IsHandled = true;
            };

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( "Sue", reader.GetValues()[0] );
            Assert.IsFalse( reader.Read() );
            CollectionAssert.AreEqual( new[] { 1 }, errored );
        }

        [TestMethod]
        public void TestFixedLength_AShortRecordIsRefusedByDefault()
        {
            var reader = new FixedLengthReader( new StringReader( "Bob  \r\n" ), FixedSchema(), FixedOptions() );

            Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );
        }

        [TestMethod]
        public void TestFixedLength_AShortRecordCanBePadded()
        {
            var options = FixedOptions();
            options.ShortRecordHandling = ShortRecordHandling.Pad;
            var reader = new FixedLengthReader( new StringReader( "Bob  \r\n" ), FixedSchema(), options );

            Assert.IsTrue( reader.Read() );
            var values = reader.GetValues();
            Assert.AreEqual( "Bob", values[0] );
            Assert.IsNull( values[1], "A window the record never reaches yields an empty value." );
        }

        [TestMethod]
        public void TestFixedLength_PaddingLeavesTheLastWindowItsWidth()
        {
            // The difference from ragged right, which would give the last column everything that follows it.
            var options = FixedOptions();
            options.ShortRecordHandling = ShortRecordHandling.Pad;
            var reader = new FixedLengthReader( new StringReader( "Bob  Leeds     surplus\r\n" ), FixedSchema(), options );

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( "Leeds", reader.GetValues()[1] );
        }

        [TestMethod]
        public void TestFixedLength_ALongRecordCanBeRefusedByTheSharedName()
        {
            var options = FixedOptions();
            options.LongRecordHandling = LongRecordHandling.Refuse;
            var reader = new FixedLengthReader( new StringReader( "Bob  Leeds     surplus\r\n" ), FixedSchema(), options );

            Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );
        }

        [TestMethod]
        public void TestFixedLength_TheLongRuleDefaultsToDiscarding()
        {
            Assert.AreEqual( LongRecordHandling.Discard, new FixedLengthOptions().LongRecordHandling );
            Assert.AreEqual( LongRecordHandling.Discard, new DelimitedOptions().LongRecordHandling );
        }

        [TestMethod]
        public void TestFixedLength_RaggedRightStillAnswersBothQuestions()
        {
            var options = FixedOptions();
            options.IsRaggedRight = true;
            options.LongRecordHandling = LongRecordHandling.Refuse;
            var reader = new FixedLengthReader( new StringReader( "Bob  Leeds     surplus\r\nSue\r\n" ), FixedSchema(), options );

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( "Leeds     surplus", reader.GetValues()[1], "The last column runs to the end of the record." );
            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( "Sue", reader.GetValues()[0] );
            Assert.IsFalse( reader.Read() );
        }

        [TestMethod]
        public void TestOptions_TheSettingsSurviveAClone()
        {
            var delimited = new DelimitedOptions
            {
                ShortRecordHandling = ShortRecordHandling.Pad,
                LongRecordHandling = LongRecordHandling.Refuse
            };
            var delimitedCopy = delimited.Clone();
            Assert.AreEqual( ShortRecordHandling.Pad, delimitedCopy.ShortRecordHandling );
            Assert.AreEqual( LongRecordHandling.Refuse, delimitedCopy.LongRecordHandling );

            var fixedLength = new FixedLengthOptions
            {
                ShortRecordHandling = ShortRecordHandling.Pad,
                LongRecordHandling = LongRecordHandling.Refuse
            };
            var fixedCopy = fixedLength.Clone();
            Assert.AreEqual( ShortRecordHandling.Pad, fixedCopy.ShortRecordHandling );
            Assert.AreEqual( LongRecordHandling.Refuse, fixedCopy.LongRecordHandling );
        }

        [TestMethod]
        public void TestOptions_AnUndefinedHandlingIsRefused()
        {
            Assert.ThrowsExactly<ArgumentException>( () => new DelimitedOptions { ShortRecordHandling = (ShortRecordHandling) 7 } );
            Assert.ThrowsExactly<ArgumentException>( () => new DelimitedOptions { LongRecordHandling = (LongRecordHandling) 7 } );
            Assert.ThrowsExactly<ArgumentException>( () => new FixedLengthOptions { ShortRecordHandling = (ShortRecordHandling) 7 } );
            Assert.ThrowsExactly<ArgumentException>( () => new FixedLengthOptions { LongRecordHandling = (LongRecordHandling) 7 } );
        }

        private static DelimitedOptions Options()
        {
            return new DelimitedOptions { RecordSeparator = "\r\n" };
        }

        private static DelimitedSchema Schema()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "Name" ) );
            schema.AddColumn( new StringColumn( "Town" ) );
            return schema;
        }

        private static FixedLengthOptions FixedOptions()
        {
            return new FixedLengthOptions { RecordSeparator = "\r\n" };
        }

        private static FixedLengthSchema FixedSchema()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "Name" ), new Window( 5 ) );
            schema.AddColumn( new StringColumn( "Town" ), new Window( 10 ) );
            return schema;
        }
    }
}
