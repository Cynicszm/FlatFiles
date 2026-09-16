using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests DelimitedOptions.IsRecordTextDisabled, which trades IRecordContext.Record away for the
    ///     per-record string it costs to build. It defaults to true, so the text is discarded unless a
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

        private static List<string> ReadCapturingRecordText( bool isRecordTextDisabled )
        {
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = true,
                IsRecordTextDisabled = isRecordTextDisabled
            };
            var reader = new DelimitedReader( new StringReader( Data ), GetSchema(), options );
            List<string> captured = [];
            reader.RecordRead += ( sender, e ) => captured.Add( e.RecordContext.Record );
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
        public void TestRecordText_DisabledByDefault_ReturnsEmptyString()
        {
            Assert.IsTrue( new DelimitedOptions().IsRecordTextDisabled, "The option should default to on." );

            var captured = ReadCapturingRecordText( isRecordTextDisabled: true );

            CollectionAssert.AreEqual( new[] { String.Empty, String.Empty }, captured,
                "The raw record text should be empty when capture is disabled." );
        }

        /// <summary>
        ///     Turning it off gets the text back, so the capture itself still works.
        /// </summary>
        [TestMethod]
        public void TestRecordText_Enabled_CapturesTheRawText()
        {
            var captured = ReadCapturingRecordText( isRecordTextDisabled: false );

            CollectionAssert.AreEqual( new[] { "1,Bob", "2,Jane" }, captured, "The raw record text was not captured." );
        }

        /// <summary>
        ///     The values are tokenised separately from the raw text, so disabling the capture must not
        ///     change a single parsed value.
        /// </summary>
        [TestMethod]
        public void TestRecordText_Disabled_ParsesIdenticalValues()
        {
            static object[][] Read( bool disabled )
            {
                var options = new DelimitedOptions
                {
                    IsFirstRecordSchema = true,
                    IsRecordTextDisabled = disabled
                };
                var reader = new DelimitedReader( new StringReader( Data ), GetSchema(), options );
                List<object[]> rows = [];
                while (reader.Read())
                {
                    rows.Add( reader.GetValues() );
                }
                return [.. rows];
            }

            var enabled = Read( false );
            var disabled = Read( true );

            Assert.AreEqual( enabled.Length, disabled.Length, "A different number of records was read." );
            for (int index = 0; index != enabled.Length; ++index)
            {
                CollectionAssert.AreEqual( enabled[index], disabled[index],
                    $"Record {index} parsed differently when the record text was disabled." );
            }
        }

        /// <summary>
        ///     Reaching the end of the file is signalled separately from the record text, so an empty
        ///     text must not be mistaken for the end of the stream and cut the read short.
        /// </summary>
        [TestMethod]
        public void TestRecordText_Disabled_ReadsEveryRecord()
        {
            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = true,
                IsRecordTextDisabled = true
            };
            var reader = new DelimitedReader( new StringReader( Data ), GetSchema(), options );
            int count = 0;
            while (reader.Read())
            {
                ++count;
            }

            Assert.AreEqual( 2, count, "Records were lost when the record text was disabled." );
        }

        /// <summary>
        ///     A blank line is a record whose text is genuinely empty. It has to survive the option being
        ///     off, so that an empty text is never load-bearing.
        /// </summary>
        [TestMethod]
        public void TestRecordText_EmptyRecord_StillCapturedWhenEnabled()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "value" ) );
            var options = new DelimitedOptions { IsRecordTextDisabled = false };
            var reader = new DelimitedReader( new StringReader( "a\r\n\r\nb\r\n" ), schema, options );
            List<string> captured = [];
            reader.RecordRead += ( sender, e ) => captured.Add( e.RecordContext.Record );
            while (reader.Read())
            {
            }

            CollectionAssert.AreEqual( new[] { "a", String.Empty, "b" }, captured,
                "An empty record should still report its own empty text." );
        }
    }
}
