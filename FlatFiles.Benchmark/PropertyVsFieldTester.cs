using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using FlatFiles.TypeMapping;

namespace FlatFiles.Benchmark
{
    public class PropertyVsFieldTester
    {
        private readonly PropertyPerson[] propertyPeople;
        private readonly FieldPerson[] fieldPeople;

        public PropertyVsFieldTester()
        {
            var propertyPerson = new PropertyPerson
            {
                FirstName = "John",
                LastName = "Smith",
                Age = 29,
                Street1 = "West Street Rd",
                Street2 = "Apt 23",
                City = "Lexington",
                State = "DE",
                Zip = "001569",
                FavouriteColour = "Blue",
                FavouriteFood = "Cheese and Crackers",
                FavouriteSport = "Soccer",
                CreatedOn = new DateTime( 2017, 01, 01 ),
                IsActive = true
            };
            propertyPeople = [.. Enumerable.Repeat( 0, 10000 ).Select( _ => propertyPerson )];

            var fieldPerson = new FieldPerson
            {
                FirstName = "John",
                LastName = "Smith",
                Age = 29,
                Street1 = "West Street Rd",
                Street2 = "Apt 23",
                City = "Lexington",
                State = "DE",
                Zip = "001569",
                FavouriteColour = "Blue",
                FavouriteFood = "Cheese and Crackers",
                FavouriteSport = "Soccer",
                CreatedOn = new DateTime( 2017, 01, 01 ),
                IsActive = true
            };
            fieldPeople = [.. Enumerable.Repeat( 0, 10000 ).Select( _ => fieldPerson )];
        }

        [Benchmark]
        public void RunPropertyTest()
        {
            var mapper = DelimitedTypeMapper.Define( () => new PropertyPerson() );
            mapper.Property( x => x.FirstName );
            mapper.Property( x => x.LastName );
            mapper.Property( x => x.Age );
            mapper.Property( x => x.Street1 );
            mapper.Property( x => x.Street2 );
            mapper.Property( x => x.City );
            mapper.Property( x => x.State );
            mapper.Property( x => x.Zip );
            mapper.Property( x => x.FavouriteColour );
            mapper.Property( x => x.FavouriteFood );
            mapper.Property( x => x.FavouriteSport );
            mapper.Property( x => x.CreatedOn );
            mapper.Property( x => x.IsActive );

            var writer = new StringWriter();
            mapper.Write( writer, propertyPeople );
            var serialised = writer.ToString();

            var reader = new StringReader( serialised );
            _ = mapper.Read( reader ).ToArray();
        }

        [Benchmark]
        public void RunFieldTest()
        {
            var mapper = DelimitedTypeMapper.Define( () => new FieldPerson() );
            mapper.Property( x => x.FirstName );
            mapper.Property( x => x.LastName );
            mapper.Property( x => x.Age );
            mapper.Property( x => x.Street1 );
            mapper.Property( x => x.Street2 );
            mapper.Property( x => x.City );
            mapper.Property( x => x.State );
            mapper.Property( x => x.Zip );
            mapper.Property( x => x.FavouriteColour );
            mapper.Property( x => x.FavouriteFood );
            mapper.Property( x => x.FavouriteSport );
            mapper.Property( x => x.CreatedOn );
            mapper.Property( x => x.IsActive );

            var writer = new StringWriter();
            mapper.Write( writer, fieldPeople );
            var serialised = writer.ToString();

            var reader = new StringReader( serialised );
            _ = mapper.Read( reader ).ToArray();
        }
        
        public class PropertyPerson
        {
            public string FirstName { get; set; } = string.Empty;

            public string LastName { get; set; } = string.Empty;

            public int Age { get; set; }

            public string Street1 { get; set; } = string.Empty;

            public string Street2 { get; set; } = string.Empty;

            public string City { get; set; } = string.Empty;

            public string State { get; set; } = string.Empty;

            public string Zip { get; set; } = string.Empty;

            public string FavouriteColour { get; set; } = string.Empty;

            public string FavouriteFood { get; set; } = string.Empty;

            public string FavouriteSport { get; set; } = string.Empty;
            
            public DateTime? CreatedOn { get; set; }

            public bool IsActive { get; set; }
        }

        public class FieldPerson
        {
            public string FirstName = string.Empty;
            public string LastName = string.Empty;
            public int Age;
            public string Street1 = string.Empty;
            public string Street2 = string.Empty;
            public string City = string.Empty;
            public string State = string.Empty;
            public string Zip = string.Empty;
            public string FavouriteColour = string.Empty;
            public string FavouriteFood = string.Empty;
            public string FavouriteSport = string.Empty;
            public DateTime? CreatedOn;
            public bool IsActive;
        }
    }
}
