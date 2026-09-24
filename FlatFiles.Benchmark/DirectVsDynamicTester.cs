using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using FlatFiles.TypeMapping;

namespace FlatFiles.Benchmark
{
    public class DirectVsDynamicTester
    {
        private readonly IDelimitedTypeMapper<Person> directMapper;
        private readonly IDynamicDelimitedTypeMapper dynamicMapper;
        private readonly Person[] people;

        public DirectVsDynamicTester()
        {
            var classTypeMapper = DelimitedTypeMapper.Define( () => new Person() );
            classTypeMapper.Property( x => x.Name ).ColumnName( "Name" );
            classTypeMapper.Property( x => x.IQ ).ColumnName( "IQ" );
            classTypeMapper.Property( x => x.BirthDate ).ColumnName( "BirthDate" );
            classTypeMapper.Property( x => x.TopSpeed ).ColumnName( "TopSpeed" );
            classTypeMapper.Property( x => x.IsActive ).ColumnName( "IsActive" );
            directMapper = classTypeMapper;

            var dynamicTypeMapper = DelimitedTypeMapper.DefineDynamic( typeof( Person ) );
            dynamicTypeMapper.StringProperty( "Name" ).ColumnName( "Name" );
            dynamicTypeMapper.Int32Property( "IQ" ).ColumnName( "IQ" );
            dynamicTypeMapper.DateTimeProperty( "BirthDate" ).ColumnName( "BirthDate" );
            dynamicTypeMapper.DecimalProperty( "TopSpeed" ).ColumnName( "TopSpeed" );
            dynamicTypeMapper.BooleanProperty( "IsActive" ).ColumnName( "IsActive" );
            dynamicMapper = dynamicTypeMapper;

            people = [.. Enumerable.Range( 0, 10000 ).Select( _ => new Person
            {
                Name = "Susan",
                IQ = 132,
                BirthDate = new DateTime( 1984, 3, 15 ),
                TopSpeed = 10.1m
            } )];
        }

        [Benchmark]
        public void Direct()
        {
            var writer = new StringWriter();
            directMapper.Write( writer, people );
            var peopleData = writer.ToString();

            var reader = new StringReader( peopleData );
            _ = directMapper.Read( reader ).ToList();
        }

        [Benchmark]
        public void Dynamic()
        {
            var writer = new StringWriter();
            dynamicMapper.Write( writer, people );
            var peopleData = writer.ToString();
            
            var reader = new StringReader( peopleData );
            _ = dynamicMapper.Read( reader ).ToList();
        }

        public class Person
        {
            public string Name { get; set; } = string.Empty;

            public int? IQ { get; set; }

            public DateTime BirthDate { get; set; }

            public decimal TopSpeed { get; set; }

            public bool IsActive { get; set; }
        }
    }
}
