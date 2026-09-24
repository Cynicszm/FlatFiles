using System;
using System.IO;
using System.Linq;
using System.Text;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests that a typed read builds the thing that makes its entities once, rather than once for every
    ///     record. The emit code generator defines a type each time it is asked for a factory, so asking per
    ///     record costs a dynamic type per record - several kilobytes and the best part of a millisecond of it,
    ///     on a path whose whole point is to allocate less.
    /// </summary>
    [TestClass]
    public class FactoryReuseTester
    {
        private const int RecordCount = 2000;

        /// <summary>
        ///     A bound, not a measurement. A read that reuses its factory allocates a few hundred bytes a record
        ///     here and one that emits a type per record allocates thousands, so anything under this says the
        ///     factory was not rebuilt, without the test claiming to know what a record ought to cost.
        /// </summary>
        private const int BytesPerRecordCeiling = 1500;

        [TestMethod]
        public void TestOptimisedRead_BuildsTheFactoryOnce()
        {
            AssertReadStaysUnder( true );
        }

        [TestMethod]
        public void TestUnoptimisedRead_BuildsTheFactoryOnce()
        {
            AssertReadStaysUnder( false );
        }

        private static void AssertReadStaysUnder( bool isOptimised )
        {
            var text = BuildRecords();
            // Read once first, so what is measured is a read rather than everything a first read warms up.
            Read( Mapper( isOptimised ), text );

            var mapper = Mapper( isOptimised );
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var before = GC.GetTotalAllocatedBytes( true );
            var read = Read( mapper, text );
            var allocated = GC.GetTotalAllocatedBytes( true ) - before;

            Assert.AreEqual( RecordCount, read );
            var perRecord = allocated / RecordCount;
            Assert.IsLessThan( BytesPerRecordCeiling, perRecord, $"A read allocated {perRecord} bytes a record, which is what rebuilding the factory for every record costs." );
        }

        private static int Read( IDelimitedTypeMapper<Person> mapper, string text )
        {
            return mapper.Read( new StringReader( text ) ).Count();
        }

        private static IDelimitedTypeMapper<Person> Mapper( bool isOptimised )
        {
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( x => x.Id );
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Age );
            mapper.Property( x => x.Balance );
            mapper.OptimiseMapping( isOptimised );
            return mapper;
        }

        private static string BuildRecords()
        {
            var builder = new StringBuilder();
            for (var index = 0; index != RecordCount; ++index)
            {
                builder.Append( index );
                builder.Append( ",Name " );
                builder.Append( index );
                builder.Append( ',' );
                builder.Append( index % 90 );
                builder.Append( ',' );
                builder.Append( index );
                builder.Append( ".25\r\n" );
            }
            return builder.ToString();
        }

        public sealed class Person
        {
            public int Id { get; set; }

            public string Name { get; set; }

            public int Age { get; set; }

            public decimal Balance { get; set; }
        }
    }
}
