using System;
using System.IO;
using System.Linq;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests that a column which cannot be read or written without its value becoming an object costs only
    ///     itself. Until this was so, one such column took every other column of the mapping off the path that
    ///     reads a record straight onto an entity, because the mapping needed an accessor for all of them or none.
    /// </summary>
    [TestClass]
    public class HookedColumnCostTester
    {
        [TestMethod]
        public void TestAHookedColumn_LeavesTheOtherColumnsTyped()
        {
            var mapper = DelimitedTypeMapper.Define<Row>();
            mapper.Property( x => x.First );
            mapper.Property( x => x.Hooked ).OnParsing( ( _, value ) => value.TrimStart( '#' ) );
            mapper.Property( x => x.Last );

            var setters = Setters( mapper );

            Assert.IsNotNull( setters, "one hooked column should not cost the mapping its setters" );
            Assert.IsInstanceOfType<ColumnSetter<Row, int>>( setters[0] );
            Assert.IsInstanceOfType<BoxingColumnSetter<Row, int>>( setters[1] );
            Assert.IsInstanceOfType<ColumnSetter<Row, int>>( setters[2] );
        }

        [TestMethod]
        public void TestAHookedColumn_LeavesTheOtherColumnsTypedWhenWriting()
        {
            var mapper = DelimitedTypeMapper.Define<Row>();
            mapper.Property( x => x.First );
            mapper.Property( x => x.Hooked ).OnFormatted( ( _, value ) => "#" + value );
            mapper.Property( x => x.Last );

            var getters = Getters( mapper );

            Assert.IsNotNull( getters, "one hooked column should not cost the mapping its getters" );
            Assert.IsInstanceOfType<ColumnGetter<Row, int>>( getters[0] );
            Assert.IsInstanceOfType<BoxingColumnGetter<Row, int>>( getters[1] );
            Assert.IsInstanceOfType<ColumnGetter<Row, int>>( getters[2] );
        }

        [TestMethod]
        public void TestAHookedColumn_StillReadsExactlyAsItDid()
        {
            var mapper = DelimitedTypeMapper.Define<Row>();
            mapper.Property( x => x.First );
            mapper.Property( x => x.Hooked ).OnParsing( ( _, value ) => value.TrimStart( '#' ) );
            mapper.Property( x => x.Last );

            var rows = mapper.Read( new StringReader( "1,#42,3\r\n7,#8,9\r\n" ), Options() ).ToList();

            Assert.AreEqual( 2, rows.Count );
            CollectionAssert.AreEqual( new[] { 1, 42, 3 }, new[] { rows[0].First, rows[0].Hooked, rows[0].Last } );
            CollectionAssert.AreEqual( new[] { 7, 8, 9 }, new[] { rows[1].First, rows[1].Hooked, rows[1].Last } );
        }

        [TestMethod]
        public void TestAHookedColumn_StillWritesExactlyAsItDid()
        {
            var mapper = DelimitedTypeMapper.Define<Row>();
            mapper.Property( x => x.First );
            mapper.Property( x => x.Hooked ).OnFormatted( ( _, value ) => "#" + value );
            mapper.Property( x => x.Last );

            var writer = new StringWriter();
            mapper.Write( writer, [new Row { First = 1, Hooked = 42, Last = 3 }], Options() );

            Assert.AreEqual( "1,#42,3\r\n", writer.ToString() );
        }

        [TestMethod]
        public void TestAHookedColumn_StillRecoversFromAnError()
        {
            // The hooked column reaches the reader's column error handling by the same route as any other.
            var mapper = DelimitedTypeMapper.Define<Row>();
            mapper.Property( x => x.First );
            mapper.Property( x => x.Hooked ).OnParsing( ( _, value ) => value.TrimStart( '#' ) );
            mapper.Property( x => x.Last );
            var reader = mapper.GetReader( new StringReader( "1,#nonsense,3\r\n" ), Options() );
            var errors = 0;
            reader.ColumnError += ( _, e ) =>
            {
                ++errors;
                e.IsHandled = true;
                e.Substitution = -1;
            };

            Assert.IsTrue( reader.Read() );
            Assert.AreEqual( 1, errors );
            Assert.AreEqual( -1, reader.Current.Hooked );
            Assert.AreEqual( 3, reader.Current.Last, "the columns after it are still read" );
        }

        [TestMethod]
        public void TestEveryColumnHooked_StillWorks()
        {
            var mapper = DelimitedTypeMapper.Define<Row>();
            mapper.Property( x => x.First ).OnParsing( ( _, value ) => value.TrimStart( '#' ) );
            mapper.Property( x => x.Hooked ).OnParsing( ( _, value ) => value.TrimStart( '#' ) );
            mapper.Property( x => x.Last ).OnParsing( ( _, value ) => value.TrimStart( '#' ) );

            var rows = mapper.Read( new StringReader( "#1,#2,#3\r\n" ), Options() ).ToList();

            Assert.ContainsSingle( rows );
            CollectionAssert.AreEqual( new[] { 1, 2, 3 }, new[] { rows[0].First, rows[0].Hooked, rows[0].Last } );
        }

        private static IColumnSetter<Row>[]? Setters( IDelimitedTypeMapper<Row> mapper )
        {
            return ( (IMapperSource<Row>) mapper ).GetMapper().GetColumnSetters();
        }

        private static IColumnGetter<Row>[]? Getters( IDelimitedTypeMapper<Row> mapper )
        {
            return ( (IMapperSource<Row>) mapper ).GetMapper().GetColumnGetters();
        }

        private static DelimitedOptions Options()
        {
            return new DelimitedOptions { RecordSeparator = "\r\n" };
        }

        public class Row
        {
            public int First { get; set; }

            public int Hooked { get; set; }

            public int Last { get; set; }
        }
    }
}
