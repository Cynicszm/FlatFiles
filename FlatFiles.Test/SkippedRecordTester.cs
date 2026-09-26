using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the records the options say to pass over without reading: a comment, and a blank line. Both are
    ///     answered from the record's text before anything is parsed, so what they must not do is reach the header,
    ///     a schema selector or a handler.
    /// </summary>
    [TestClass]
    public class SkippedRecordTester
    {
        [TestMethod]
        public void TestDelimited_ACommentIsPassedOver()
        {
            const string source = "# a banner\r\nBob,1\r\n# and another\r\nSusan,2\r\n";
            var reader = new DelimitedReader( new StringReader( source ), new DelimitedOptions { CommentPrefix = "#" } );

            var names = Names( reader );

            CollectionAssert.AreEqual( new[] { "Bob", "Susan" }, names );
        }

        [TestMethod]
        public void TestDelimited_CommentsAboveTheHeaderDoNotBecomeTheHeader()
        {
            const string source = "# written by something\r\n# on some date\r\nName,Id\r\nBob,1\r\n";
            var options = new DelimitedOptions { CommentPrefix = "#", IsFirstRecordSchema = true };
            var reader = new DelimitedReader( new StringReader( source ), options );

            Assert.IsTrue( reader.Read() );

            var schema = reader.GetSchema()!;
            CollectionAssert.AreEqual( new[] { "Name", "Id" }, schema.ColumnDefinitions.Select( x => x.ColumnName ).ToArray() );
            Assert.AreEqual( "Bob", reader.GetValues()[0] );
        }

        [TestMethod]
        public void TestDelimited_OnlyAPrefixCounts()
        {
            // A record that merely contains the text is a record, not a comment.
            const string source = "Bob,1\r\nSusan #2,3\r\n";
            var reader = new DelimitedReader( new StringReader( source ), Schema(), new DelimitedOptions { CommentPrefix = "#" } );

            Assert.HasCount( 2, Names( reader ) );
        }

        [TestMethod]
        public void TestDelimited_WithNoPrefixSetNothingIsPassedOver()
        {
            const string source = "# not a comment here\r\nBob,1\r\n";
            var reader = new DelimitedReader( new StringReader( source ), new DelimitedOptions() );

            CollectionAssert.AreEqual( new[] { "# not a comment here", "Bob" }, Names( reader ) );
        }

        [TestMethod]
        public void TestDelimited_ABlankRecordIsPassedOverOnlyWhenAsked()
        {
            const string source = "Bob,1\r\n\r\n   \r\nSusan,2\r\n";

            var kept = new DelimitedReader( new StringReader( source ), new DelimitedOptions() );
            Assert.HasCount( 4, Names( kept ), "A blank line is a record of one empty value, and some files mean it." );

            var skipped = new DelimitedReader( new StringReader( source ), new DelimitedOptions { IsBlankRecordSkipped = true } );
            CollectionAssert.AreEqual( new[] { "Bob", "Susan" }, Names( skipped ) );
        }

        [TestMethod]
        public void TestDelimited_ACommentNeverReachesAHandler()
        {
            const string source = "# a banner\r\nBob,1\r\n";
            var reader = new DelimitedReader( new StringReader( source ), new DelimitedOptions { CommentPrefix = "#" } );
            var seen = new List<string>();
            reader.RecordRead += ( _, e ) => seen.Add( e.Values[0] );

            Names( reader );

            CollectionAssert.AreEqual( new[] { "Bob" }, seen, "Passing a record over is the point of the option; a handler asked to do it makes every record copy its values out." );
        }

        [TestMethod]
        public void TestDelimited_APassedOverRecordStillCountsPhysically()
        {
            const string source = "# a banner\r\nBob,1\r\n";
            var reader = new DelimitedReader( new StringReader( source ), new DelimitedOptions { CommentPrefix = "#" } );
            var physical = 0;
            reader.RecordRead += ( _, e ) => physical = e.RecordContext.PhysicalRecordNumber;

            Assert.IsTrue( reader.Read() );

            // The physical number is where the record sits in the file, and is only useful if it agrees with what
            // a text editor shows.
            Assert.AreEqual( 2, physical );
        }

        [TestMethod]
        public async Task TestDelimited_TheAsynchronousPathPassesThemOverToo()
        {
            const string source = "# a banner\r\nBob,1\r\n\r\nSusan,2\r\n";
            var options = new DelimitedOptions { CommentPrefix = "#", IsBlankRecordSkipped = true };
            var reader = new DelimitedReader( new StringReader( source ), options );

            var names = new List<string>();
            while (await reader.ReadAsync())
            {
                names.Add( (string) reader.GetValues()[0]! );
            }

            CollectionAssert.AreEqual( new[] { "Bob", "Susan" }, names );
        }

        [TestMethod]
        public void TestFixedLength_ACommentIsPassedOverBeforeItIsMeasured()
        {
            // The comment is narrower than a record, which is the whole reason this cannot wait until the record
            // has been partitioned into its windows.
            const string source = "# a banner\r\nBob       01\r\nSusan     02\r\n";
            var options = new FixedLengthOptions { CommentPrefix = "#", RecordSeparator = "\r\n" };
            var reader = new FixedLengthReader( new StringReader( source ), FixedSchema(), options );

            CollectionAssert.AreEqual( new[] { "Bob", "Susan" }, Names( reader ) );
        }

        [TestMethod]
        public void TestFixedLength_ABlankRecordIsPassedOver()
        {
            const string source = "Bob       01\r\n\r\nSusan     02\r\n";
            var options = new FixedLengthOptions { IsBlankRecordSkipped = true, RecordSeparator = "\r\n" };
            var reader = new FixedLengthReader( new StringReader( source ), FixedSchema(), options );

            CollectionAssert.AreEqual( new[] { "Bob", "Susan" }, Names( reader ) );
        }

        [TestMethod]
        public async Task TestFixedLength_TheAsynchronousPathPassesThemOverToo()
        {
            const string source = "# a banner\r\nBob       01\r\n";
            var options = new FixedLengthOptions { CommentPrefix = "#", RecordSeparator = "\r\n" };
            var reader = new FixedLengthReader( new StringReader( source ), FixedSchema(), options );

            var names = new List<string>();
            while (await reader.ReadAsync())
            {
                names.Add( (string) reader.GetValues()[0]! );
            }

            CollectionAssert.AreEqual( new[] { "Bob" }, names );
        }

        [TestMethod]
        public void TestOptionsCloneCarriesThem()
        {
            // The readers and writers run on a private copy of the options, so anything the clone drops is an
            // option that quietly does nothing.
            var delimited = new DelimitedOptions { CommentPrefix = "//", IsBlankRecordSkipped = true }.Clone();
            Assert.AreEqual( "//", delimited.CommentPrefix );
            Assert.IsTrue( delimited.IsBlankRecordSkipped );

            var fixedLength = new FixedLengthOptions { CommentPrefix = "//", IsBlankRecordSkipped = true }.Clone();
            Assert.AreEqual( "//", fixedLength.CommentPrefix );
            Assert.IsTrue( fixedLength.IsBlankRecordSkipped );
        }

        private static DelimitedSchema Schema()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "Name" ) );
            schema.AddColumn( new StringColumn( "Id" ) );
            return schema;
        }

        private static FixedLengthSchema FixedSchema()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "Name" ), new Window( 10 ) );
            schema.AddColumn( new StringColumn( "Id" ), new Window( 2 ) );
            return schema;
        }

        private static string[] Names( IReader reader )
        {
            var names = new List<string>();
            while (reader.Read())
            {
                names.Add( (string) reader.GetValues()[0]! );
            }
            return [.. names];
        }
    }
}
