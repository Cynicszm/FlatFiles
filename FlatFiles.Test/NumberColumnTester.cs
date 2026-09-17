using System;
using System.Buffers;
using System.Globalization;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the shared base of the numeric columns: the defaults each concrete column starts with, the three
    ///     settings that steer parsing and formatting, and that a caller can add a numeric type of their own.
    /// </summary>
    [TestClass]
    public class NumberColumnTester
    {
        [TestMethod]
        public void TestDefaults_IntegralColumnsAcceptIntegersAndFloatingPointColumnsAcceptFloats()
        {
            Assert.AreEqual( NumberStyles.Integer, new ByteColumn( "c" ).NumberStyles );
            Assert.AreEqual( NumberStyles.Integer, new SByteColumn( "c" ).NumberStyles );
            Assert.AreEqual( NumberStyles.Integer, new Int16Column( "c" ).NumberStyles );
            Assert.AreEqual( NumberStyles.Integer, new Int32Column( "c" ).NumberStyles );
            Assert.AreEqual( NumberStyles.Integer, new Int64Column( "c" ).NumberStyles );
            Assert.AreEqual( NumberStyles.Integer, new UInt16Column( "c" ).NumberStyles );
            Assert.AreEqual( NumberStyles.Integer, new UInt32Column( "c" ).NumberStyles );
            Assert.AreEqual( NumberStyles.Integer, new UInt64Column( "c" ).NumberStyles );
            Assert.AreEqual( NumberStyles.Float, new SingleColumn( "c" ).NumberStyles );
            Assert.AreEqual( NumberStyles.Float, new DoubleColumn( "c" ).NumberStyles );
            Assert.AreEqual( NumberStyles.Number, new DecimalColumn( "c" ).NumberStyles );
        }

        [TestMethod]
        public void TestEveryNumericColumn_DerivesFromTheSharedBase()
        {
            IColumnDefinition[] columns =
            [
                new ByteColumn( "c" ), new SByteColumn( "c" ), new Int16Column( "c" ), new Int32Column( "c" ), new Int64Column( "c" ),
                new UInt16Column( "c" ), new UInt32Column( "c" ), new UInt64Column( "c" ),
                new SingleColumn( "c" ), new DoubleColumn( "c" ), new DecimalColumn( "c" )
            ];
            foreach (var column in columns)
            {
                var baseType = column.GetType().BaseType;
                Assert.IsNotNull( baseType );
                Assert.IsTrue( baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof( NumberColumn<> ), column.GetType().Name );
            }
        }

        [TestMethod]
        public void TestParse_NumberStylesAndProvider_SteerParsing()
        {
            var german = CultureInfo.GetCultureInfo( "de-DE" );
            var column = new Int32Column( "c" ) { NumberStyles = NumberStyles.Integer | NumberStyles.AllowThousands, FormatProvider = german };

            Assert.AreEqual( 1234567, column.Parse( null, "1.234.567" ) );
        }

        [TestMethod]
        public void TestParse_DefaultStylesRejectWhatTheyAlwaysDid()
        {
            Assert.ThrowsExactly<FormatException>( () => new Int32Column( "c" ).Parse( null, "1,234" ) );
            Assert.ThrowsExactly<FormatException>( () => new Int32Column( "c" ).Parse( null, "1.5" ) );
            Assert.AreEqual( 1.5, new DoubleColumn( "c" ).Parse( null, "1.5" ) );
            Assert.AreEqual( 1234.5m, new DecimalColumn( "c" ).Parse( null, "1,234.5" ) );
        }

        [TestMethod]
        public void TestFormat_OutputFormatAndProvider_SteerFormatting()
        {
            var german = CultureInfo.GetCultureInfo( "de-DE" );
            var column = new DecimalColumn( "c" ) { OutputFormat = "N2", FormatProvider = german };

            Assert.AreEqual( "1.234,50", column.Format( null, 1234.5m ) );
            var buffer = new ArrayBufferWriter<char>();
            column.Format( null, 1234.5m, buffer );
            Assert.AreEqual( "1.234,50", buffer.WrittenSpan.ToString() );
        }

        [TestMethod]
        public void TestCustomNumericType_ParsesAndFormatsThroughTheBase()
        {
            var column = new BigIntegerColumn( "c" ) { OutputFormat = "D40" };
            var value = BigInteger.Parse( "123456789012345678901234567890", CultureInfo.InvariantCulture );

            Assert.AreEqual( value, column.Parse( null, "123456789012345678901234567890" ) );
            Assert.AreEqual( "0000000000123456789012345678901234567890", column.Format( null, value ) );
        }

        /// <summary>
        ///     A numeric type the library does not ship a column for, added by a caller in a constructor's worth of code.
        /// </summary>
        private sealed class BigIntegerColumn( string columnName ) : NumberColumn<BigInteger>( columnName, NumberStyles.Integer )
        {
        }
    }
}
