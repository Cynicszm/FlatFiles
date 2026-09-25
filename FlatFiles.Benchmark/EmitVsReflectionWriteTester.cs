using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using FlatFiles.TypeMapping;

namespace FlatFiles.Benchmark
{
    public class EmitVsReflectionWriteTester
    {
        private readonly IDelimitedTypeMapper<Person> mapper;
        private readonly Person[] people;

        public EmitVsReflectionWriteTester()
        {
            var delimitedTypeMapper = DelimitedTypeMapper.Define( () => new Person() );
            delimitedTypeMapper.Property( x => x.Name ).ColumnName( "Name" );
            delimitedTypeMapper.Property( x => x.IQ ).ColumnName( "IQ" );
            delimitedTypeMapper.Property( x => x.BirthDate ).ColumnName( "BirthDate" );
            delimitedTypeMapper.Property( x => x.TopSpeed ).ColumnName( "TopSpeed" );
            mapper = delimitedTypeMapper;

            people = [.. Enumerable.Range( 0, 10000 ).Select( _ => new Person
            {
                Name = "Susan",
                IQ = 132,
                BirthDate = new DateTime( 1984, 3, 15 ),
                TopSpeed = 10.1m
            } )];
        }

        [Benchmark( Description = "SerialiseEmit" )]
        public string SerialiseEmit()
        {
            mapper.OptimiseMapping();
            var writer = new StringWriter();
            mapper.Write( writer, people );
            return writer.ToString();
        }

        [Benchmark( Description = "SerialiseReflection" )]
        public string SerialiseReflection()
        {
            mapper.OptimiseMapping( false );
            var writer = new StringWriter();
            mapper.Write( writer, people );
            return writer.ToString();
        }

        public class Person
        {
            public string Name { get; set; } = string.Empty;

            public int? IQ { get; set; }

            public DateTime BirthDate { get; set; }

            public decimal TopSpeed { get; set; }
        }
    }
}
