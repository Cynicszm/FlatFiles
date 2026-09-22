using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests that the options refuse a value outside their enumeration, and say which option refused it. Each
    ///     message names the setting it came from, because an exception that describes a different setting sends the
    ///     reader looking in the wrong place.
    /// </summary>
    [TestClass]
    public class OptionsValidationTester
    {
        [TestMethod]
        public void TestQuoteBehaviour_OutsideTheEnumeration_SaysSo()
        {
            var options = new DelimitedOptions();

            var exception = Assert.ThrowsExactly<ArgumentException>( () => options.QuoteBehaviour = (QuoteBehaviour) 99 );

            StringAssert.Contains( exception.Message, "quote",
                "The message must name quoting, not the fixed-width alignment it used to report." );
        }

        [TestMethod]
        public void TestAlignment_OutsideTheEnumeration_SaysSo()
        {
            var options = new FixedLengthOptions();

            var exception = Assert.ThrowsExactly<ArgumentException>( () => options.Alignment = (FixedAlignment) 99 );

            StringAssert.Contains( exception.Message, "alignment" );
        }

        [TestMethod]
        public void TestTruncationPolicy_OutsideTheEnumeration_SaysSo()
        {
            var options = new FixedLengthOptions();

            var exception = Assert.ThrowsExactly<ArgumentException>( () => options.TruncationPolicy = (OverflowTruncationPolicy) 99 );

            StringAssert.Contains( exception.Message, "truncation" );
        }

        [TestMethod]
        public void TestWindowAlignment_OutsideTheEnumeration_SaysSo()
        {
            var window = new Window( 5 );

            var exception = Assert.ThrowsExactly<ArgumentException>( () => window.Alignment = (FixedAlignment) 99 );

            StringAssert.Contains( exception.Message, "alignment" );
        }

        [TestMethod]
        public void TestSeparator_EmptyOrNull_IsRefused()
        {
            var options = new DelimitedOptions();

            Assert.ThrowsExactly<ArgumentException>( () => options.Separator = "" );
            Assert.ThrowsExactly<ArgumentException>( () => options.Separator = null );
        }

        [TestMethod]
        public void TestClone_CopiesEverySetting()
        {
            var delimited = new DelimitedOptions
            {
                Separator = ";",
                RecordSeparator = "\n",
                Quote = '\'',
                QuoteBehaviour = QuoteBehaviour.AlwaysQuote,
                IsFirstRecordSchema = true,
                PreserveWhiteSpace = true,
                IsColumnContextDisabled = true,
                PreserveRecordText = true
            };

            var copy = delimited.Clone();

            Assert.AreEqual( ";", copy.Separator );
            Assert.AreEqual( "\n", copy.RecordSeparator );
            Assert.AreEqual( '\'', copy.Quote );
            Assert.AreEqual( QuoteBehaviour.AlwaysQuote, copy.QuoteBehaviour );
            Assert.IsTrue( copy.IsFirstRecordSchema );
            Assert.IsTrue( copy.PreserveWhiteSpace );
            Assert.IsTrue( copy.IsColumnContextDisabled );
            Assert.IsTrue( copy.PreserveRecordText );

            var fixedLength = new FixedLengthOptions
            {
                FillCharacter = '0',
                HasRecordSeparator = false,
                IsFirstRecordHeader = true,
                IsLongRecordRejected = true,
                IsRaggedRight = true,
                Alignment = FixedAlignment.RightAligned,
                TruncationPolicy = OverflowTruncationPolicy.TruncateTrailing,
                IsColumnContextDisabled = true
            };

            var fixedCopy = fixedLength.Clone();

            Assert.AreEqual( '0', fixedCopy.FillCharacter );
            Assert.IsFalse( fixedCopy.HasRecordSeparator );
            Assert.IsTrue( fixedCopy.IsFirstRecordHeader );
            Assert.IsTrue( fixedCopy.IsLongRecordRejected );
            Assert.IsTrue( fixedCopy.IsRaggedRight );
            Assert.AreEqual( FixedAlignment.RightAligned, fixedCopy.Alignment );
            Assert.AreEqual( OverflowTruncationPolicy.TruncateTrailing, fixedCopy.TruncationPolicy );
            Assert.IsTrue( fixedCopy.IsColumnContextDisabled );
        }
    }
}
