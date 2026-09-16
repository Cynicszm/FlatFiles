using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the character buffer the parsers scan: refilling, compaction of consumed text, growth when a record
    ///     outgrows the buffer, and end-of-stream detection.
    /// </summary>
    [TestClass]
    public class CharBufferReaderTester
    {
        [TestMethod]
        public void TestFill_NothingConsumed_GrowsUntilTheStreamEnds()
        {
            var reader = new CharBufferReader( new StringReader( "0123456789" ), 4 );

            reader.Fill();
            Assert.AreEqual( "0123", reader.Span.ToString() );
            Assert.IsFalse( reader.IsEndOfStream, "Four characters filled the buffer exactly, so the stream may continue." );

            reader.Fill();
            Assert.AreEqual( "01234567", reader.Span.ToString(), "A full buffer with nothing consumed must grow." );
            Assert.IsFalse( reader.IsEndOfStream );

            reader.Fill();
            Assert.AreEqual( "0123456789", reader.Span.ToString() );
            Assert.IsTrue( reader.IsEndOfStream, "A short read marks the end of the stream." );
            Assert.AreEqual( 10, reader.Available );
        }

        [TestMethod]
        public void TestFill_AfterConsuming_CompactsAndKeepsTheRest()
        {
            var reader = new CharBufferReader( new StringReader( "abcdefgh" ), 4 );
            reader.Fill();
            reader.Consume( 2 );
            Assert.AreEqual( "cd", reader.Span.ToString() );

            reader.Fill();

            Assert.AreEqual( "cdef", reader.Span.ToString(), "The unconsumed text stays at the front and the buffer is topped up behind it." );
            Assert.AreEqual( 4, reader.Available );
        }

        [TestMethod]
        public void TestConsume_Everything_LeavesTheBufferEmpty()
        {
            var reader = new CharBufferReader( new StringReader( "abc" ), 8 );
            reader.Fill();

            reader.Consume( 3 );

            Assert.AreEqual( 0, reader.Available );
            Assert.IsTrue( reader.Span.IsEmpty );
            Assert.IsTrue( reader.IsEndOfStream );
        }

        [TestMethod]
        public void TestConsume_MoreThanAvailable_Throws()
        {
            var reader = new CharBufferReader( new StringReader( "abc" ), 8 );
            reader.Fill();

            Assert.ThrowsExactly<ArgumentOutOfRangeException>( () => reader.Consume( 4 ) );
            Assert.ThrowsExactly<ArgumentOutOfRangeException>( () => reader.Consume( -1 ) );
        }

        [TestMethod]
        public void TestFill_AtEndOfStream_DoesNothing()
        {
            var reader = new CharBufferReader( new StringReader( "ab" ), 8 );
            reader.Fill();
            Assert.IsTrue( reader.IsEndOfStream );

            reader.Fill();

            Assert.AreEqual( "ab", reader.Span.ToString() );
        }

        [TestMethod]
        public async Task TestFillAsync_BehavesLikeFill()
        {
            var reader = new CharBufferReader( new StringReader( "0123456789" ), 4 );

            await reader.FillAsync();
            reader.Consume( 1 );
            await reader.FillAsync();
            await reader.FillAsync();
            await reader.FillAsync();

            Assert.AreEqual( "123456789", reader.Span.ToString() );
            Assert.IsTrue( reader.IsEndOfStream );
        }
    }
}
