using System;
using System.IO;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     A custom mapping can be given a reader and no writer, for a column a file carries and an entity does
    ///     not keep. One of those used to take every other column in the mapping off the path that writes
    ///     straight from the entity.
    /// </summary>
    /// <remarks>
    ///     Writing needs a getter for every column or it uses none of them, and none was built for a column that
    ///     was never going to write anything - so the whole mapping went back through the array of values on
    ///     account of the one column with nothing to say. It is the last corner of the same defect the rest of
    ///     the mapper was fixed for.
    /// </remarks>
    [TestClass]
    public class ReaderOnlyMappingTester
    {
        public class Trade
        {
            public string Key { get; set; } = string.Empty;

            public int Quantity { get; set; }
        }

        /// <summary>The middle column is read onto nothing and never written.</summary>
        private static IDelimitedTypeMapper<Trade> Mapper()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Trade() );
            mapper.Property( x => x.Key ).ColumnName( "key" );
            mapper.CustomMapping( new StringColumn( "note" ) )
                .WithReader( ( _, _ ) => { } );
            mapper.Property( x => x.Quantity ).ColumnName( "quantity" );
            return mapper;
        }

        [TestMethod]
        public void TestItDoesNotTakeTheOtherColumnsWithIt()
        {
            Assert.IsNotNull( ( (IMapperSource<Trade>) Mapper() ).GetMapper().GetColumnGetters(),
                "one column that writes nothing took the whole mapping off the assembled path" );
        }

        /// <summary>
        ///     What it writes has to be what it wrote before, which is the null the array of values was left
        ///     holding - not an empty string by coincidence, and not a skipped column.
        /// </summary>
        [TestMethod]
        public void TestItWritesWhatTheArrayPathWrote()
        {
            var entities = new[]
            {
                new Trade { Key = "K1", Quantity = 7 },
                new Trade { Key = "K2", Quantity = -1 }
            };

            var fromEntities = new StringWriter();
            Mapper().Write( fromEntities, entities );

            // The same schema driven by hand, with the middle column given the null the serialiser left there.
            var fromValues = new StringWriter();
            var writer = new DelimitedWriter( fromValues, Mapper().GetSchema() );
            foreach (var entity in entities)
            {
                writer.Write( [entity.Key, null, entity.Quantity] );
            }

            Assert.AreEqual( fromValues.ToString(), fromEntities.ToString() );
        }

        /// <summary>
        ///     The column's own null formatter still decides what nothing looks like, which is the part that
        ///     would be lost by writing an empty string instead.
        /// </summary>
        [TestMethod]
        public void TestTheColumnsNullFormatterIsHonoured()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Trade() );
            mapper.Property( x => x.Key ).ColumnName( "key" );
            mapper.CustomMapping( new StringColumn( "note" ) { NullFormatter = NullFormatter.ForValue( "(none)" ) } )
                .WithReader( ( _, _ ) => { } );

            var written = new StringWriter();
            mapper.Write( written, [new Trade { Key = "K1" }] );

            Assert.AreEqual( "K1,(none)", written.ToString().Trim() );
        }

        /// <summary>Reading still puts the record through the reader, which is the half it was declared for.</summary>
        [TestMethod]
        public void TestTheReaderStillRuns()
        {
            var seen = 0;
            var mapper = DelimitedTypeMapper.Define( () => new Trade() );
            mapper.Property( x => x.Key ).ColumnName( "key" );
            mapper.CustomMapping( new StringColumn( "note" ) )
                .WithReader( ( _, value ) => seen += value is null ? 0 : 1 );
            mapper.Property( x => x.Quantity ).ColumnName( "quantity" );

            var text = string.Join( Environment.NewLine, "K1,first,7", "K2,second,8" );
            var read = 0;
            var reader = mapper.GetReader( new StringReader( text ) );
            while (reader.Read())
            {
                ++read;
            }

            Assert.AreEqual( 2, read );
            Assert.AreEqual( 2, seen, "the reader was not given the column's value" );
        }
    }
}
