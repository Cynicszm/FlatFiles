using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    [TestClass]
    public class DelimitedAsyncReaderTester
    {
        [TestMethod]
        public async Task TestTypeMapper_Roundtrip()
        {
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( p => p.Id ).ColumnName( "id" );
            mapper.Property( p => p.Name ).ColumnName( "name" );
            mapper.Property( p => p.Created ).ColumnName( "created" ).InputFormat( "yyyyMMdd" ).OutputFormat( "yyyyMMdd" );
            mapper.Property( p => p.IsActive ).ColumnName( "active" );

            var bob = new Person { Id = 123, Name = "Bob", Created = new DateTime( 2013, 1, 19 ), IsActive = true };

            var stringWriter = new StringWriter();
            await mapper.WriteAsync( stringWriter, [ bob ] ).ConfigureAwait( false );

            var stringReader = new StringReader( stringWriter.ToString() );
            var reader = mapper.GetReader( stringReader );
            List<Person> people = [];
            while (await reader.ReadAsync().ConfigureAwait( false ))
            {
                people.Add( reader.Current );
            }
            Assert.AreEqual( 1, people.Count );
            var person = people.SingleOrDefault();
            Assert.AreEqual( bob.Id, person.Id );
            Assert.AreEqual( bob.Name, person.Name );
            Assert.AreEqual( bob.Created, person.Created );
            Assert.IsTrue( person.IsActive );
        }

        internal class Person
        {
            public int Id { get; set; }

            public string Name { get; set; }

            public DateTime Created { get; set; }

            public bool? IsActive { get; set; }
        }
    }
}
