using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests that a mapper works its accessors out once. A reader and a writer each ask for them once, so
    ///     nothing in ordinary use asks twice - which is exactly why it is worth a test of its own rather than
    ///     being left to something else to establish by accident.
    /// </summary>
    [TestClass]
    public class MapperAccessorCachingTester
    {
        [TestMethod]
        public void TestTheSettersAreWorkedOutOnce()
        {
            var mapper = Mapper();

            Assert.AreSame( mapper.GetColumnSetters(), mapper.GetColumnSetters() );
        }

        [TestMethod]
        public void TestTheGettersAreWorkedOutOnce()
        {
            var mapper = Mapper();

            Assert.AreSame( mapper.GetColumnGetters(), mapper.GetColumnGetters() );
        }

        [TestMethod]
        public void TestAMappingWithNoneToWorkOut_KeepsAnsweringNothing()
        {
            var source = DelimitedTypeMapper.Define<Person>();
            source.Property( x => x.Id );
            // A custom writer takes its values as objects, so there are no getters to be had - and the answer
            // must be the same the second time as the first.
            source.CustomMapping( new StringColumn( "Name" ) ).WithWriter( ( Person entity, object?[] values ) => values[1] = entity.Name );
            var mapper = ( (IMapperSource<Person>) source ).GetMapper();

            Assert.IsNull( mapper.GetColumnGetters() );
            Assert.IsNull( mapper.GetColumnGetters() );
        }

        private static IMapper<Person> Mapper()
        {
            var source = DelimitedTypeMapper.Define<Person>();
            source.Property( x => x.Id );
            source.Property( x => x.Name );
            return ( (IMapperSource<Person>) source ).GetMapper();
        }

        public sealed class Person
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;
        }
    }
}
