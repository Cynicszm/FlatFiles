using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests a selector reading records onto entities without boxing their values on the way. A selector is
    ///     handed several mappers and does not know which a record belongs to until it has read it, so it takes
    ///     one assembler per mapper and uses the one the matched schema belongs to.
    ///     <para>
    ///         It is all of them or none: the reader takes the assembled path for every record once it has an
    ///         assembler, so a mapping that cannot be read that way - a custom reader here - puts every mapping
    ///         back on the values path. Both paths have to produce the same entities, which is what these check.
    ///     </para>
    /// </summary>
    [TestClass]
    public class MapperSelectorAssemblyTester
    {
        [TestMethod]
        public void TestFixedLengthSelector_ReadsEachLayoutOntoItsType()
        {
            var selector = new FixedLengthTypeMapperSelector();
            selector.When( x => x.StartsWith( 'A' ) ).Use( HeaderMapper() );
            selector.When( x => x.StartsWith( 'B' ) ).Use( DetailMapper( false ) );

            var read = selector.GetReader( new StringReader( "AJan\r\nB000012345\r\nB000067890\r\n" ) ).ReadAll().ToList();

            AssertFixedLength( read );
        }

        [TestMethod]
        public void TestFixedLengthSelector_WithAMappingThatCannotAssemble_StillReads()
        {
            var selector = new FixedLengthTypeMapperSelector();
            selector.When( x => x.StartsWith( 'A' ) ).Use( HeaderMapper() );
            // A custom reader takes its value as an object, so this mapping cannot be assembled and neither can
            // the one beside it.
            selector.When( x => x.StartsWith( 'B' ) ).Use( DetailMapper( true ) );

            var read = selector.GetReader( new StringReader( "AJan\r\nB000012345\r\nB000067890\r\n" ) ).ReadAll().ToList();

            AssertFixedLength( read );
        }

        [TestMethod]
        public void TestFixedLengthSelector_WithARecordParsedHandler_StillReads()
        {
            var selector = new FixedLengthTypeMapperSelector();
            selector.When( x => x.StartsWith( 'A' ) ).Use( HeaderMapper() );
            selector.When( x => x.StartsWith( 'B' ) ).Use( DetailMapper( false ) );

            var reader = selector.GetReader( new StringReader( "AJan\r\nB000012345\r\nB000067890\r\n" ) );
            var parsed = 0;
            // A handler is given the parsed values, so the reader hands the record back to the values path for
            // every record it touches. The entities have to come out the same.
            reader.RecordParsed += ( _, _ ) => ++parsed;

            var read = reader.ReadAll().ToList();

            AssertFixedLength( read );
            Assert.AreEqual( 3, parsed );
        }

        [TestMethod]
        public void TestDelimitedSelector_ReadsEachSchemaOntoItsType()
        {
            var selector = new DelimitedTypeMapperSelector();
            var header = DelimitedTypeMapper.Define<Header>();
            header.Property( x => x.Name );
            var detail = DelimitedTypeMapper.Define<Detail>();
            detail.Property( x => x.Amount );
            detail.Property( x => x.Quantity );
            selector.When( values => values.Length == 1 ).Use( header );
            selector.When( values => values.Length == 2 ).Use( detail );

            var read = selector.GetReader( new StringReader( "Jan\r\n12345,7\r\n67890,9\r\n" ) ).ReadAll().ToList();

            Assert.HasCount( 3, read );
            Assert.AreEqual( "Jan", ( (Header) read[0] ).Name );
            Assert.AreEqual( 12345, ( (Detail) read[1] ).Amount );
            Assert.AreEqual( 9, ( (Detail) read[2] ).Quantity );
        }

        [TestMethod]
        public void TestDelimitedSelector_WithADefaultMapper_ReadsWhatNothingMatched()
        {
            var selector = new DelimitedTypeMapperSelector();
            var header = DelimitedTypeMapper.Define<Header>();
            header.Property( x => x.Name );
            var detail = DelimitedTypeMapper.Define<Detail>();
            detail.Property( x => x.Amount );
            detail.Property( x => x.Quantity );
            selector.When( values => values.Length == 1 ).Use( header );
            selector.WithDefault( detail );

            var read = selector.GetReader( new StringReader( "Jan\r\n12345,7\r\n" ) ).ReadAll().ToList();

            Assert.HasCount( 2, read );
            Assert.AreEqual( "Jan", ( (Header) read[0] ).Name );
            Assert.AreEqual( 7, ( (Detail) read[1] ).Quantity );
        }

        private static void AssertFixedLength( List<object> read )
        {
            Assert.HasCount( 3, read );
            Assert.AreEqual( "Jan", ( (Header) read[0] ).Name );
            Assert.AreEqual( 12345, ( (Detail) read[1] ).Amount );
            Assert.AreEqual( 67890, ( (Detail) read[2] ).Amount );
        }

        private static IFixedLengthTypeMapper<Header> HeaderMapper()
        {
            var mapper = FixedLengthTypeMapper.Define<Header>();
            mapper.Ignored( new Window( 1 ) );
            mapper.Property( x => x.Name, new Window( 3 ) );
            return mapper;
        }

        private static IFixedLengthTypeMapper<Detail> DetailMapper( bool withCustomReader )
        {
            var mapper = FixedLengthTypeMapper.Define<Detail>();
            mapper.Ignored( new Window( 1 ) );
            if (withCustomReader)
            {
                mapper.CustomMapping( new Int32Column( "Amount" ), new Window( 9 ) )
                    .WithReader( ( entity, value ) => entity.Amount = (int) value! );
                return mapper;
            }
            mapper.Property( x => x.Amount, new Window( 9 ) );
            return mapper;
        }

        public sealed class Header
        {
            public string Name { get; set; }
        }

        public sealed class Detail
        {
            public int Amount { get; set; }

            public int Quantity { get; set; }
        }
    }
}
