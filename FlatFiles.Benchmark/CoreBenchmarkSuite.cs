using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using FlatFiles.TypeMapping;

namespace FlatFiles.Benchmark
{
    public class CoreBenchmarkSuite
    {
        private readonly string data;
        private readonly string quotedData;

        public CoreBenchmarkSuite()
        {
            string[] headers =
            [
                "FirstName", "LastName", "Age", "Street1", "Street2", "City", "State", "Zip", "FavouriteColour", "FavouriteFood", "FavouriteSport", "CreatedOn", "IsActive"
            ];
            string[] values =
            [
                "John", "Smith", "29", "West Street Rd", "Apt 23", "Lexington", "DE", "001569", "Blue", "Cheese and Crackers", "Soccer", "2017-01-01", "true"
            ];
            var header = string.Join( ",", headers );
            var record = string.Join( ",", values );
            data = string.Join( Environment.NewLine, new[] { header }.Concat( Enumerable.Repeat( 0, 10000 ).Select( _ => record ) ) );

            string[] quotedValues =
            [
                "Joe", "Smith", "29", "\"West Street Rd, Apt. 23\"", "ATTN: Will Smith", "Lexington", "DE", "001569", "Blue", "\"Cheese, and Crackers\"", "Soccer", "2017-01-01", "true"
            ];
            var quotedRecord = string.Join( ",", quotedValues );
            quotedData = string.Join( Environment.NewLine, new[] { header }.Concat( Enumerable.Repeat( 0, 10000 ).Select( _ => quotedRecord ) ) );

            sample = [.. GetMapper().Read( new StringReader( data ), new DelimitedOptions { IsFirstRecordSchema = true } )];
            var fixedLengthWriter = new StringWriter();
            GetFixedLengthMapper().Write( fixedLengthWriter, sample, FixedLengthOptions );
            fixedLengthData = fixedLengthWriter.ToString();
        }

        private static readonly FixedLengthOptions FixedLengthOptions = new() { RecordSeparator = Environment.NewLine };

        private readonly List<Person> sample;
        private readonly string fixedLengthData;

        private static IDelimitedTypeMapper<Person> GetMapper()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Person() );
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
            return mapper;
        }

        private static IFixedLengthTypeMapper<Person> GetFixedLengthMapper()
        {
            var mapper = FixedLengthTypeMapper.Define( () => new Person() );
            mapper.Property( x => x.FirstName, 10 );
            mapper.Property( x => x.LastName, 10 );
            mapper.Property( x => x.Age, 3 );
            mapper.Property( x => x.Street1, 20 );
            mapper.Property( x => x.Street2, 10 );
            mapper.Property( x => x.City, 12 );
            mapper.Property( x => x.State, 2 );
            mapper.Property( x => x.Zip, 6 );
            mapper.Property( x => x.FavouriteColour, 8 );
            mapper.Property( x => x.FavouriteFood, 20 );
            mapper.Property( x => x.FavouriteSport, 8 );
            mapper.Property( x => x.CreatedOn, 10 ).OutputFormat( "yyyy-MM-dd" );
            mapper.Property( x => x.IsActive, 5 );
            return mapper;
        }

        [Benchmark]
        public string RunFlatFiles_TypeMapper_Write()
        {
            var writer = new StringWriter();
            GetMapper().Write( writer, sample, new DelimitedOptions { IsFirstRecordSchema = true } );
            return writer.ToString();
        }

        [Benchmark]
        public int RunFlatFiles_FixedLength_TypeMapper()
        {
            return GetFixedLengthMapper().Read( new StringReader( fixedLengthData ), FixedLengthOptions ).Count();
        }

        [Benchmark]
        public string RunFlatFiles_FixedLength_TypeMapper_Write()
        {
            var writer = new StringWriter();
            GetFixedLengthMapper().Write( writer, sample, FixedLengthOptions );
            return writer.ToString();
        }

        [Benchmark]
        public string RunCsvHelper_Write()
        {
            var writer = new StringWriter();
            var csvWriter = new CsvHelper.CsvWriter( writer, CultureInfo.InvariantCulture );
            csvWriter.WriteRecords( sample );
            csvWriter.Flush();
            return writer.ToString();
        }

        [Benchmark]
        public void RunFlatFiles_NoSchema()
        {
            var reader = new StringReader( data );
            var csvReader = new DelimitedReader( reader );
            var people = new List<object?[]>();
            while (csvReader.Read())
            {
                people.Add( csvReader.GetValues() );
            }
        }

        [Benchmark]
        public void RunFlatFiles_TypeMapper()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Person() );
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

            var reader = new StringReader( data );
            _ = mapper.Read( reader, new DelimitedOptions { IsFirstRecordSchema = true } ).ToArray();
        }

        [Benchmark]
        public async Task RunFlatFiles_TypeMapper_Async()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Person() );
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

            var textReader = new StringReader( data );
            var reader = mapper.GetReader( textReader, new DelimitedOptions { IsFirstRecordSchema = true } );
            var people = new List<Person>();
            await foreach (var person in reader.ReadAllAsync().ConfigureAwait( false ))
            {
                people.Add( person );
            }
        }

        [Benchmark]
        public void RunFlatFiles_TypeMapper_Quoted()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Person() );
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

            var reader = new StringReader( quotedData );
            _ = mapper.Read( reader, new DelimitedOptions { IsFirstRecordSchema = true } ).ToArray();
        }

        [Benchmark]
        public void RunFlatFiles_TypeMapper_Fields()
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

            var reader = new StringReader( data );
            _ = mapper.Read( reader, new DelimitedOptions { IsFirstRecordSchema = true } ).ToArray();
        }

        [Benchmark]
        public void RunFlatFiles_TypeMapper_Unoptimized()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Person() );
            mapper.OptimiseMapping( false );
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

            var reader = new StringReader( data );
            _ = mapper.Read( reader, new DelimitedOptions { IsFirstRecordSchema = true } ).ToArray();
        }

        [Benchmark]
        public void RunFlatFiles_TypeMapper_CustomMapping()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Person() );
            mapper.CustomMapping( new StringColumn( "FirstName" ) ).WithReader( p => p.FirstName );
            mapper.CustomMapping( new StringColumn( "LastName" ) ).WithReader( p => p.LastName );
            mapper.CustomMapping( new Int32Column( "Age" ) ).WithReader( p => p.Age );
            mapper.CustomMapping( new StringColumn( "Street1" ) ).WithReader( p => p.Street1 );
            mapper.CustomMapping( new StringColumn( "Street2" ) ).WithReader( p => p.Street2 );
            mapper.CustomMapping( new StringColumn( "City" ) ).WithReader( p => p.City );
            mapper.CustomMapping( new StringColumn( "State" ) ).WithReader( p => p.State );
            mapper.CustomMapping( new StringColumn( "Zip" ) ).WithReader( p => p.Zip );
            mapper.CustomMapping( new StringColumn( "FavouriteColour" ) ).WithReader( p => p.FavouriteColour );
            mapper.CustomMapping( new StringColumn( "FavouriteFood)" ) ).WithReader( p => p.FavouriteFood );
            mapper.CustomMapping( new StringColumn( "FavouriteSport" ) ).WithReader( p => p.FavouriteSport );
            mapper.CustomMapping( new DateTimeColumn( "CreatedOn" ) ).WithReader( p => p.CreatedOn );
            mapper.CustomMapping( new BooleanColumn( "IsActive" ) ).WithReader( p => p.IsActive );

            var reader = new StringReader( data );
            _ = mapper.Read( reader, new DelimitedOptions { IsFirstRecordSchema = true } ).ToArray();
        }

        [Benchmark]
        public void RunFlatFiles_TypeMapper_CustomMapping_Unoptimized()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Person() );
            mapper.OptimiseMapping( false );
            mapper.CustomMapping( new StringColumn( "FirstName" ) ).WithReader( p => p.FirstName );
            mapper.CustomMapping( new StringColumn( "LastName" ) ).WithReader( p => p.LastName );
            mapper.CustomMapping( new Int32Column( "Age" ) ).WithReader( p => p.Age );
            mapper.CustomMapping( new StringColumn( "Street1" ) ).WithReader( p => p.Street1 );
            mapper.CustomMapping( new StringColumn( "Street2" ) ).WithReader( p => p.Street2 );
            mapper.CustomMapping( new StringColumn( "City" ) ).WithReader( p => p.City );
            mapper.CustomMapping( new StringColumn( "State" ) ).WithReader( p => p.State );
            mapper.CustomMapping( new StringColumn( "Zip" ) ).WithReader( p => p.Zip );
            mapper.CustomMapping( new StringColumn( "FavouriteColour" ) ).WithReader( p => p.FavouriteColour );
            mapper.CustomMapping( new StringColumn( "FavouriteFood)" ) ).WithReader( p => p.FavouriteFood );
            mapper.CustomMapping( new StringColumn( "FavouriteSport" ) ).WithReader( p => p.FavouriteSport );
            mapper.CustomMapping( new DateTimeColumn( "CreatedOn" ) ).WithReader( p => p.CreatedOn );
            mapper.CustomMapping( new BooleanColumn( "IsActive" ) ).WithReader( p => p.IsActive );

            var reader = new StringReader( data );
            _ = mapper.Read( reader, new DelimitedOptions { IsFirstRecordSchema = true } ).ToArray();
        }

        [Benchmark]
        public void RunFlatFiles_AutoMapped()
        {
            var reader = new StringReader( data );
            var csvReader = DelimitedTypeMapper.GetAutoMappedReader<Person>( reader );
            var people = new List<Person>();
            foreach (var person in csvReader.ReadAll())
            {
                people.Add( person );
            }
        }

        [Benchmark]
        public async Task RunFlatFiles_AutoMapped_Async()
        {
            var reader = new StringReader( data );
            var csvReader = await DelimitedTypeMapper.GetAutoMappedReaderAsync<Person>( reader );
            var people = new List<Person>();
            while (await csvReader.ReadAsync().ConfigureAwait( false ))
            {
                people.Add( csvReader.Current );
            }
        }

        [Benchmark]
        public void RunFlatFiles_FlatFileDataReader_ByPosition()
        {
            var reader = new StringReader( data );
            var schema = GetSchema();
            var csvReader = new DelimitedReader( reader, schema, new DelimitedOptions { IsFirstRecordSchema = true } );
            var dataReader = new FlatFileDataReader( csvReader );
            var people = new List<Person>();
            while (dataReader.Read())
            {
                var person = new Person
                {
                    FirstName = dataReader.GetString( 0 )!,
                    LastName = dataReader.GetString( 1 )!,
                    Age = dataReader.GetInt32( 2 ),
                    Street1 = dataReader.GetString( 3 )!,
                    Street2 = dataReader.GetString( 4 )!,
                    City = dataReader.GetString( 5 )!,
                    State = dataReader.GetString( 6 )!,
                    Zip = dataReader.GetString( 7 )!,
                    FavouriteColour = dataReader.GetString( 8 )!,
                    FavouriteFood = dataReader.GetString( 9 )!,
                    FavouriteSport = dataReader.GetString( 10 )!,
                    CreatedOn = dataReader.GetDateTime( 11 ),
                    IsActive = dataReader.GetBoolean( 12 )
                };
                people.Add( person );
            }
        }

        [Benchmark]
        public void RunFlatFiles_FlatFileDataReader_ByName()
        {
            var reader = new StringReader( data );
            var schema = GetSchema();
            var csvReader = new DelimitedReader( reader, schema, new DelimitedOptions { IsFirstRecordSchema = true } );
            var dataReader = new FlatFileDataReader( csvReader );
            var people = new List<Person>();
            while (dataReader.Read())
            {
                var person = new Person
                {
                    FirstName = dataReader.GetString( "FirstName" ),
                    LastName = dataReader.GetString( "LastName" ),
                    Age = dataReader.GetInt32( "Age" ),
                    Street1 = dataReader.GetString( "Street1" ),
                    Street2 = dataReader.GetString( "Street2" ),
                    City = dataReader.GetString( "City" ),
                    State = dataReader.GetString( "State" ),
                    Zip = dataReader.GetString( "Zip" ),
                    FavouriteColour = dataReader.GetString( "FavouriteColour" ),
                    FavouriteFood = dataReader.GetString( "FavouriteFood" ),
                    FavouriteSport = dataReader.GetString( "FavouriteSport" ),
                    CreatedOn = dataReader.GetDateTime( "CreatedOn" ),
                    IsActive = dataReader.GetBoolean( "IsActive" )
                };
                people.Add( person );
            }
        }

        [Benchmark]
        public void RunFlatFiles_FlatFileDataReader_GetValue()
        {
            var reader = new StringReader( data );
            var schema = GetSchema();
            var csvReader = new DelimitedReader( reader, schema, new DelimitedOptions { IsFirstRecordSchema = true } );
            var dataReader = new FlatFileDataReader( csvReader );
            var people = new List<Person>();
            while (dataReader.Read())
            {
                var person = new Person
                {
                    FirstName = dataReader.GetValue<string>( 0 )!,
                    LastName = dataReader.GetValue<string>( 1 )!,
                    Age = dataReader.GetValue<int>( 2 ),
                    Street1 = dataReader.GetValue<string>( 3 )!,
                    Street2 = dataReader.GetValue<string>( 4 )!,
                    City = dataReader.GetValue<string>( 5 )!,
                    State = dataReader.GetValue<string>( 6 )!,
                    Zip = dataReader.GetValue<string>( 7 )!,
                    FavouriteColour = dataReader.GetValue<string>( 8 )!,
                    FavouriteFood = dataReader.GetValue<string>( 9 )!,
                    FavouriteSport = dataReader.GetValue<string>( 10 )!,
                    CreatedOn = dataReader.GetValue<DateTime?>( 11 ),
                    IsActive = dataReader.GetValue<bool>( 12 )
                };
                people.Add( person );
            }
        }

        [Benchmark]
        public void RunFlatFiles_DataTable()
        {
            var reader = new StringReader( data );
            var schema = GetSchema();
            var csvReader = new DelimitedReader( reader, schema, new DelimitedOptions { IsFirstRecordSchema = true } );
            var dataTable = new DataTable();
            dataTable.ReadFlatFile( csvReader );
        }

        private static DelimitedSchema GetSchema()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "FirstName" ) );
            schema.AddColumn( new StringColumn( "LastName" ) );
            schema.AddColumn( new Int32Column( "Age" ) );
            schema.AddColumn( new StringColumn( "Street1" ) );
            schema.AddColumn( new StringColumn( "Street2" ) );
            schema.AddColumn( new StringColumn( "City" ) );
            schema.AddColumn( new StringColumn( "State" ) );
            schema.AddColumn( new StringColumn( "Zip" ) );
            schema.AddColumn( new StringColumn( "FavouriteColour" ) );
            schema.AddColumn( new StringColumn( "FavouriteFood" ) );
            schema.AddColumn( new StringColumn( "FavouriteSport" ) );
            schema.AddColumn( new DateTimeColumn( "CreatedOn" ) );
            schema.AddColumn( new BooleanColumn( "IsActive" ) );
            return schema;
        }

        [Benchmark]
        public void RunCsvHelper()
        {
            var reader = new StringReader( data );
            var csvReader = new CsvHelper.CsvReader( reader, CultureInfo.InvariantCulture );
            var people = new List<Person>();
            foreach (var person in csvReader.GetRecords<Person>())
            {
                people.Add( person );
            }
        }

        [Benchmark]
        public async Task RunCsvHelper_Async()
        {
            var reader = new StringReader( data );
            var csvReader = new CsvHelper.CsvReader( reader, CultureInfo.InvariantCulture );
            var people = new List<Person>();
            await csvReader.ReadAsync().ConfigureAwait( false );
            csvReader.ReadHeader();
            while (await csvReader.ReadAsync().ConfigureAwait( false ))
            {
                people.Add( csvReader.GetRecord<Person>() );
            }
        }

        [Benchmark]
        public void RunCsvHelper_Quoted()
        {
            var reader = new StringReader( quotedData );
            var csvReader = new CsvHelper.CsvReader( reader, CultureInfo.InvariantCulture );
            var people = new List<Person>();
            foreach (var person in csvReader.GetRecords<Person>())
            {
                people.Add( person );
            }
        }

        [Benchmark]
        public void RunStringSplit()
        {
            var lines = data.Split( Environment.NewLine );
            var records = lines.Skip( 1 ).Select( l => l.Split( "," ) );
            var people = new List<Person>();
            foreach (var record in records)
            {
                var person = new Person
                {
                    FirstName = record[0],
                    LastName = record[1],
                    Age = int.Parse( record[2] ),
                    Street1 = record[3],
                    Street2 = record[4],
                    City = record[5],
                    State = record[6],
                    Zip = record[7],
                    FavouriteColour = record[8],
                    FavouriteFood = record[9],
                    FavouriteSport = record[10],
                    CreatedOn = DateTime.Parse( record[11] ),
                    IsActive = bool.Parse( record[12] )
                };
                people.Add( person );
            }
        }

        public class Person
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
