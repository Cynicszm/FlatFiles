using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests that what a read prepares, it prepares once. Preparing means asking the code generator for a
    ///     factory, a constructor, a deserialiser or a serialiser, and the emit generator answers each request by
    ///     defining a type in its dynamic module. Asking once a read costs nothing worth measuring; asking once a
    ///     record emits a type per record, which is what 8.1.0 shipped and what every test that checks only what a
    ///     read produces let through.
    ///     <para>
    ///         Two kinds of check, because one alone would not have caught it. Counting what a read asks for is
    ///         exact and says where the cost went. A ceiling on what a read allocates is a blunter instrument that
    ///         holds for the reflection generator too, where preparing is cheap enough not to show in a count but
    ///         doing it per record is still wrong.
    ///     </para>
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class ReadPreparationTester
    {
        private const int Few = 1;
        private const int Many = 1000;

        /// <summary>
        ///     A bound, not a measurement. A read that prepares once allocates a few hundred bytes a record here
        ///     and one that prepares per record allocates thousands, so anything under this says the preparation
        ///     was not repeated, without the test claiming to know what a record ought to cost.
        /// </summary>
        private const int BytesPerRecordCeiling = 1500;

        [TestMethod]
        public void TestDelimitedTypedRead_PreparesOncePerRead()
        {
            AssertPreparationDoesNotFollowRecords( count => ReadDelimited( DelimitedMapper( true ), count ) );
        }

        [TestMethod]
        public void TestFixedLengthTypedRead_PreparesOncePerRead()
        {
            AssertPreparationDoesNotFollowRecords( count => ReadFixedLength( FixedLengthMapper(), count ) );
        }

        [TestMethod]
        public void TestDelimitedWrite_PreparesOncePerWrite()
        {
            AssertPreparationDoesNotFollowRecords( count =>
            {
                var people = Enumerable.Range( 0, count ).Select( index => new Person { Id = index, Name = "Name", Age = index % 90, Balance = index } );
                var writer = new StringWriter();
                DelimitedMapper( true ).Write( writer, people );
                return count;
            } );
        }

        [TestMethod]
        public void TestConstructorMappedRead_PreparesOncePerRead()
        {
            AssertPreparationDoesNotFollowRecords( count =>
            {
                var mapper = DelimitedTypeMapper.Define<Built>();
                mapper.Property( x => x.Id );
                mapper.Property( x => x.Name );
                return mapper.Read( new StringReader( BuildRecords( count, 2 ) ) ).Count();
            } );
        }

        [TestMethod]
        public void TestOptimisedRead_StaysUnderTheCeiling()
        {
            AssertReadStaysUnderTheCeiling( true );
        }

        [TestMethod]
        public void TestUnoptimisedRead_StaysUnderTheCeiling()
        {
            AssertReadStaysUnderTheCeiling( false );
        }

        /// <summary>
        ///     Runs the same work over one record and over a thousand, and holds what was prepared to the same
        ///     count. What that count is does not matter; that it does not follow the records does.
        /// </summary>
        private static void AssertPreparationDoesNotFollowRecords( Func<int, int> work )
        {
            // Once through first: the first read of a process prepares things neither count should carry.
            work( Few );

            var before = EmitCodeGenerator.Preparations;
            var fewRead = work( Few );
            var forFew = EmitCodeGenerator.Preparations - before;

            before = EmitCodeGenerator.Preparations;
            var manyRead = work( Many );
            var forMany = EmitCodeGenerator.Preparations - before;

            Assert.AreEqual( Few, fewRead );
            Assert.AreEqual( Many, manyRead );
            if (DynamicCode.IsSupported)
            {
                Assert.IsGreaterThan( 0, forFew, "Nothing was prepared, so the count is not holding anything." );
            }
            Assert.AreEqual( forFew, forMany, $"{Few} record prepared {forFew} times and {Many} records prepared {forMany} times, so preparing is following the records." );
        }

        private static void AssertReadStaysUnderTheCeiling( bool isOptimised )
        {
            ReadDelimited( DelimitedMapper( isOptimised ), Many );

            var mapper = DelimitedMapper( isOptimised );
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var before = GC.GetTotalAllocatedBytes( true );
            var read = ReadDelimited( mapper, Many );
            var allocated = GC.GetTotalAllocatedBytes( true ) - before;

            Assert.AreEqual( Many, read );
            var perRecord = allocated / Many;
            Assert.IsLessThan( BytesPerRecordCeiling, perRecord, $"A read allocated {perRecord} bytes a record, which is what preparing for every record costs." );
        }

        private static int ReadDelimited( IDelimitedTypeMapper<Person> mapper, int count )
        {
            return mapper.Read( new StringReader( BuildRecords( count, 4 ) ) ).Count();
        }

        private static int ReadFixedLength( IFixedLengthTypeMapper<Person> mapper, int count )
        {
            var builder = new StringBuilder();
            for (var index = 0; index != count; ++index)
            {
                builder.Append( index.ToString().PadRight( 8 ) );
                builder.Append( "Name".PadRight( 10 ) );
                builder.Append( (index % 90).ToString().PadRight( 4 ) );
                builder.Append( index.ToString().PadRight( 10 ) );
                builder.Append( "\r\n" );
            }
            return mapper.Read( new StringReader( builder.ToString() ) ).Count();
        }

        private static IDelimitedTypeMapper<Person> DelimitedMapper( bool isOptimised )
        {
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( x => x.Id );
            mapper.Property( x => x.Name );
            mapper.Property( x => x.Age );
            mapper.Property( x => x.Balance );
            mapper.OptimiseMapping( isOptimised );
            return mapper;
        }

        private static IFixedLengthTypeMapper<Person> FixedLengthMapper()
        {
            var mapper = FixedLengthTypeMapper.Define<Person>();
            mapper.Property( x => x.Id, 8 );
            mapper.Property( x => x.Name, 10 );
            mapper.Property( x => x.Age, 4 );
            mapper.Property( x => x.Balance, 10 );
            return mapper;
        }

        private static string BuildRecords( int count, int columns )
        {
            var builder = new StringBuilder();
            for (var index = 0; index != count; ++index)
            {
                builder.Append( index );
                builder.Append( ",Name " );
                builder.Append( index );
                if (columns == 4)
                {
                    builder.Append( ',' );
                    builder.Append( index % 90 );
                    builder.Append( ',' );
                    builder.Append( index );
                    builder.Append( ".25" );
                }
                builder.Append( "\r\n" );
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

        /// <summary>
        ///     Built through its constructor, so a read of it asks for one of those as well as a deserialiser.
        /// </summary>
        public sealed class Built( int id, string name )
        {
            public int Id { get; } = id;

            public string Name { get; } = name;
        }
    }
}
