using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests DelimitedOptions.PreserveRecordText, which trades IRecordContext.Record against the
    ///     per-record string it costs to build. It defaults to false, so the text is discarded unless a
    ///     caller asks for it.
    /// </summary>
    [TestClass]
    public class RecordTextCaptureTester
    {
        private const string Data = "id,name\r\n1,Bob\r\n2,Jane\r\n";

        private static DelimitedSchema GetSchema()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new Int32Column( "id" ) );
            schema.AddColumn( new StringColumn( "name" ) );
            return schema;
        }

        private static List<string> ReadCapturingRecordText( bool preserveRecordText )
        {
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = true,
                PreserveRecordText = preserveRecordText
            };
            var reader = new DelimitedReader( new StringReader( Data ), GetSchema(), options );
            List<string> captured = [];
            reader.RecordRead += ( _, e ) => captured.Add( e.RecordContext.Record );
            while (reader.Read())
            {
            }
            return captured;
        }

        /// <summary>
        ///     The default discards the text, because reading a file is the common case and paying for a
        ///     string nobody reads is not.
        /// </summary>
        [TestMethod]
        public void TestRecordText_NotPreservedByDefault_ReturnsEmptyString()
        {
            Assert.IsFalse( new DelimitedOptions().PreserveRecordText, "The option should default to off." );

            var captured = ReadCapturingRecordText( preserveRecordText: false );

            CollectionAssert.AreEqual( new[] { string.Empty, string.Empty }, captured,
                "The raw record text should be empty when capture is disabled." );
        }

        /// <summary>
        ///     Turning it on gets the text back, so the capture itself still works.
        /// </summary>
        [TestMethod]
        public void TestRecordText_Preserved_CapturesTheRawText()
        {
            var captured = ReadCapturingRecordText( preserveRecordText: true );

            CollectionAssert.AreEqual( new[] { "1,Bob", "2,Jane" }, captured, "The raw record text was not captured." );
        }

        /// <summary>
        ///     The values are tokenised separately from the raw text, so disabling the capture must not
        ///     change a single parsed value.
        /// </summary>
        [TestMethod]
        public void TestRecordText_NotPreserved_ParsesIdenticalValues()
        {
            static object[][] Read( bool preserve )
            {
                var options = new DelimitedOptions
                {
                    IsFirstRecordSchema = true,
                    PreserveRecordText = preserve
                };
                var reader = new DelimitedReader( new StringReader( Data ), GetSchema(), options );
                List<object[]> rows = [];
                while (reader.Read())
                {
                    rows.Add( reader.GetValues() );
                }
                return [.. rows];
            }

            var preserved = Read( true );
            var discarded = Read( false );

            Assert.AreEqual( preserved.Length, discarded.Length, "A different number of records was read." );
            for (var index = 0; index != preserved.Length; ++index)
            {
                CollectionAssert.AreEqual( preserved[index], discarded[index],
                    $"Record {index} parsed differently when the record text was discarded." );
            }
        }

        /// <summary>
        ///     Reaching the end of the file is signalled separately from the record text, so an empty
        ///     text must not be mistaken for the end of the stream and cut the read short.
        /// </summary>
        [TestMethod]
        public void TestRecordText_NotPreserved_ReadsEveryRecord()
        {
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = true,
                PreserveRecordText = false
            };
            var reader = new DelimitedReader( new StringReader( Data ), GetSchema(), options );
            var count = 0;
            while (reader.Read())
            {
                ++count;
            }

            Assert.AreEqual( 2, count, "Records were lost when the record text was disabled." );
        }

        /// <summary>
        ///     A blank line is a record whose text is genuinely empty. It has to survive the option being
        ///     on, so that an empty text is never load-bearing.
        /// </summary>
        [TestMethod]
        public void TestRecordText_EmptyRecord_StillCapturedWhenPreserved()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "value" ) );
            var options = new DelimitedOptions { PreserveRecordText = true };
            var reader = new DelimitedReader( new StringReader( "a\r\n\r\nb\r\n" ), schema, options );
            List<string> captured = [];
            reader.RecordRead += ( _, e ) => captured.Add( e.RecordContext.Record );
            while (reader.Read())
            {
            }

            CollectionAssert.AreEqual( new[] { "a", string.Empty, "b" }, captured,
                "An empty record should still report its own empty text." );
        }
    }
}
