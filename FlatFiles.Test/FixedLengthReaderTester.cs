using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the FixedLengthParserTester class.
    /// </summary>
    [TestClass]
    public class FixedLengthReaderTester
    {
        /// <summary>
        ///     If we try to pass null text to the parser, an exception should be thrown.
        /// </summary>
        [TestMethod]
        public void TestCtor_Options_TextNull_Throws()
        {
            TextReader reader = null!;
            var schema = new FixedLengthSchema();
            var options = new FixedLengthOptions();
            Assert.ThrowsExactly<ArgumentNullException>( () => new FixedLengthReader( reader!, schema, options ) );
        }

        /// <summary>
        ///     If we trying to pass a null schema, an exception should be thrown.
        /// </summary>
        [TestMethod]
        public void TestCtor_SchemaNull_Throws()
        {
            var reader = new StringReader( string.Empty );
            FixedLengthSchema schema = null!;
            Assert.ThrowsExactly<ArgumentNullException>( () => new FixedLengthReader( reader, schema! ) );
        }

        /// <summary>
        ///     If we pass a single record, Read should return true once.
        /// </summary>
        [TestMethod]
        public void TestRead_SingleRecord_ReturnsTrueOnce()
        {
            const string text = @"       123                      Bob 1/19/2013";
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "id" ), new Window( 10 ) )
                .AddColumn( new StringColumn( "name" ), new Window( 25 ) )
                .AddColumn( new DateTimeColumn( "created" ), new Window( 10 ) );

            var stringReader = new StringReader( text );
            var parser = new FixedLengthReader( stringReader, schema );
            Assert.IsTrue( parser.Read(), "Could not read the record." );
            object[] expected = [ 123, "Bob", new DateTime( 2013, 1, 19 ) ];
            var actual = parser.GetValues();
            CollectionAssert.AreEqual( expected, actual );
            Assert.IsFalse( parser.Read(), "No more records should have been read." );
        }

        /// <summary>
        ///     If we skip a bad record, it should not result in a parsing error.
        /// </summary>
        [TestMethod]
        public void TestRead_SkipRecord_NoParsingError()
        {
            const string text = "a b c";
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "A" ), 8 );
            schema.AddColumn( new DateTimeColumn( "B" ), 23 );
            schema.AddColumn( new GuidColumn( "C" ), 2 );

            var stringReader = new StringReader( text );
            var parser = new FixedLengthReader( stringReader, schema );
            var canRead = parser.Skip();
            Assert.IsTrue( canRead, "Could not skip the record." );
            canRead = parser.Read();
            Assert.IsFalse( canRead, "No more records should have been read." );
        }

        /// <summary>
        ///     If we try to get the values before calling Read, an exception should be thrown.
        /// </summary>
        [TestMethod]
        public void TestRead_GetValuesWithoutReading_Throws()
        {
            const string text = @"       123                      Bob 1/19/2013";
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "id" ), new Window( 10 ) )
                .AddColumn( new StringColumn( "name" ), new Window( 25 ) )
                .AddColumn( new DateTimeColumn( "created" ), new Window( 10 ) );

            var stringReader = new StringReader( text );
            var parser = new FixedLengthReader( stringReader, schema );
            Assert.ThrowsExactly<InvalidOperationException>( parser.GetValues );
        }

        /// <summary>
        ///     We should be able to read the same values as many times as we want.
        /// </summary>
        [TestMethod]
        public void TestRead_MultipleCallsToValues_ReturnsSameValues()
        {
            const string text = @"       123                      Bob 1/19/2013";
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "id" ), new Window( 10 ) )
                .AddColumn( new StringColumn( "name" ), new Window( 25 ) )
                .AddColumn( new DateTimeColumn( "created" ), new Window( 10 ) );

            var stringReader = new StringReader( text );
            var parser = new FixedLengthReader( stringReader, schema );
            var canRead = parser.Read();
            Assert.IsTrue( canRead, "Could not read the record." );
            object[] expected = [ 123, "Bob", new DateTime( 2013, 1, 19 ) ];
            var actual = parser.GetValues();
            CollectionAssert.AreEqual( expected, actual );
            actual = parser.GetValues();
            CollectionAssert.AreEqual( expected, actual );
        }

        /// <summary>
        ///     If Read returns false, requesting the Values will cause an exception to be thrown.
        /// </summary>
        [TestMethod]
        public void TestRead_ValuesAfterEndOfFile_Throws()
        {
            const string text = @"       123                      Bob 1/19/2013";
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "id" ), new Window( 10 ) )
                .AddColumn( new StringColumn( "name" ), new Window( 25 ) )
                .AddColumn( new DateTimeColumn( "created" ), new Window( 10 ) );

            var stringReader = new StringReader( text );
            var parser = new FixedLengthReader( stringReader, schema );
            Assert.IsTrue( parser.Read(), "Could not read the record." );
            Assert.IsFalse( parser.Read(), "We should have reached the end of the file." );
            Assert.ThrowsExactly<InvalidOperationException>( parser.GetValues );
        }

        /// <summary>
        ///     We should be able to inspect each raw record as we process a file.
        /// </summary>
        [TestMethod]
        public void TestRead_InspectRawRecords()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "id" ), 10 )
                  .AddColumn( new StringColumn( "name" ), 25 )
                  .AddColumn( new DateTimeColumn( "created" ), 10 );

            const string text = @"       123                      Bob 1/19/2013";
            var stringReader = new StringReader( text );
            var reader = new FixedLengthReader( stringReader, schema );
            reader.RecordRead += ( _, e ) => {
                Assert.AreEqual( @"       123                      Bob 1/19/2013", e.Record );
            };
            reader.RecordParsed += ( _, e ) => {
                Assert.AreEqual( @"       123                      Bob 1/19/2013", e.RecordContext.Record );
                CollectionAssert.AreEqual( new[] { "       123", "                      Bob", " 1/19/2013" }, e.RecordContext.Values );
            };
            Assert.IsTrue( reader.Read() );
            Assert.IsFalse( reader.Read() );
        }

        /// <summary>
        ///     If we provide a schema, it will be used to parse the values
        ///     and can be retrieved.
        /// </summary>
        [TestMethod]
        public void TestGetSchema_SchemaProvided_ParsesValues()
        {
            const string text = @"       123                      Bob 1/19/2013";
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "id" ), new Window( 10 ) )
                .AddColumn( new StringColumn( "name" ), new Window( 25 ) )
                .AddColumn( new DateTimeColumn( "created" ), new Window( 10 ) );

            var stringReader = new StringReader( text );
            IReader parser = new FixedLengthReader( stringReader, schema );
            var actual = parser.GetSchema();
            Assert.AreSame( schema, actual );
        }

        /// <summary>
        ///     The records in the file must have a value for each column.
        /// </summary>
        [TestMethod]
        public void TestGetSchema_SchemaProvided_WrongNumberOfColumns_Throws()
        {
            const string text = @"       123                      Bob";
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "id" ), new Window( 10 ) )
                  .AddColumn( new StringColumn( "name" ), new Window( 25 ) )
                  .AddColumn( new DateTimeColumn( "created" ), new Window( 10 ) );

            var stringReader = new StringReader( text );
            var parser = new FixedLengthReader( stringReader, schema );
            Assert.ThrowsExactly<RecordProcessingException>( () => parser.Read() );
        }

        /// <summary>
        ///     If we specify a custom record separator, it should be used
        ///     to split records in the file.
        /// </summary>
        [TestMethod]
        public void TestGetValues_CustomRecordSeparator_SplitsFile()
        {             
            const string text = "       123                      Bob 1/19/2013BOOM       234                      Sam12/20/2013";
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "id" ), new Window( 10 ) )
                  .AddColumn( new StringColumn( "name" ), new Window( 25 ) )
                  .AddColumn( new DateTimeColumn( "created" ), new Window( 10 ) );
            var options = new FixedLengthOptions { RecordSeparator = "BOOM" };

            var stringReader = new StringReader( text );
            var parser = new FixedLengthReader( stringReader, schema, options );

            Assert.IsTrue( parser.Read(), "Could not read the first record." );
            object[] expected = [ 123, "Bob", new DateTime( 2013, 1, 19 ) ];
            var actual = parser.GetValues();
            CollectionAssert.AreEqual( expected, actual );

            Assert.IsTrue( parser.Read(), "Could not read the second record." );
            expected = [ 234, "Sam", new DateTime( 2013, 12, 20 ) ];
            actual = parser.GetValues();
            CollectionAssert.AreEqual( expected, actual );
        }

        /// <summary>
        ///     If we specify no record separator, the length of the record
        ///     is expected to perfectly match the length of schema.
        /// </summary>
        [TestMethod]
        public void TestGetValues_NoRecordSeparator_SplitsFile()
        {
            const string text = "       123                      Bob 1/19/2013       234                      Sam12/20/2013";
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "id" ), new Window( 10 ) )
                  .AddColumn( new StringColumn( "name" ), new Window( 25 ) )
                  .AddColumn( new DateTimeColumn( "created" ), new Window( 10 ) );
            var options = new FixedLengthOptions { HasRecordSeparator = false };

            var stringReader = new StringReader( text );
            var parser = new FixedLengthReader( stringReader, schema, options );

            Assert.IsTrue( parser.Read(), "Could not read the first record." );
            object[] expected = [ 123, "Bob", new DateTime( 2013, 1, 19 ) ];
            var actual = parser.GetValues();
            CollectionAssert.AreEqual( expected, actual );

            Assert.IsTrue( parser.Read(), "Could not read the second record." );
            expected = [ 234, "Sam", new DateTime( 2013, 12, 20 ) ];
            actual = parser.GetValues();
            CollectionAssert.AreEqual( expected, actual );
        }

        /// <summary>
        ///     If we specify a custom fill character, it should be used to buffer fields in the file.
        /// </summary>
        [TestMethod]
        public void TestGetValues_CustomFillCharacter_TrimsFill()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "id" ), new Window( 10 ) { Alignment = FixedAlignment.LeftAligned } )
                  .AddColumn( new StringColumn( "name" ), new Window( 25 ) { Alignment = FixedAlignment.LeftAligned } )
                  .AddColumn( new DateTimeColumn( "created" ) { InputFormat = "M/d/yyyy", OutputFormat = "M/d/yyyy" }, new Window( 10 ) { Alignment = FixedAlignment.LeftAligned } );
            var options = new FixedLengthOptions { FillCharacter = '@' };
            object[] sources = [ 123, "Bob", new DateTime( 2013, 1, 19 ) ];

            var stringWriter = new StringWriter();
            var builder = new FixedLengthWriter( stringWriter, schema, options );
            builder.Write( sources );

            var stringReader = new StringReader( stringWriter.ToString() );
            var parser = new FixedLengthReader( stringReader, schema, options );

            Assert.IsTrue( parser.Read(), "Could not read the first record." );
            var actual = parser.GetValues();
            CollectionAssert.AreEqual( sources, actual );
        }

        /// <summary>
        ///     If we specify a record filter, those records should be automatically skipped while reading the document.
        /// </summary>
        [TestMethod]
        public void TestGetValues_WithUnpartitionedRecordFilter_SkipRecordsMatchingCriteria()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "id" ), new Window( 10 ) { Alignment = FixedAlignment.RightAligned } )
                  .AddColumn( new StringColumn( "name" ), new Window( 25 ) { Alignment = FixedAlignment.RightAligned } )
                  .AddColumn( new DateTimeColumn( "created" ) { InputFormat = "M/d/yyyy" }, new Window( 10 ) { Alignment = FixedAlignment.RightAligned } );

            const string lines = @"       123                Bob Smith 4/21/2017
a weird row that should be skipped
       234                Jay Smith 5/21/2017";

            var stringReader = new StringReader( lines );
            var parser = new FixedLengthReader( stringReader, schema );
            parser.RecordRead += ( _, e ) =>
            {
                e.IsSkipped = e.Record.StartsWith( "a" );
            };

            Assert.IsTrue( parser.Read(), "Could not read the first record." );
            var actual1 = parser.GetValues();
            CollectionAssert.AreEqual( new object[] { 123, "Bob Smith", new DateTime( 2017, 04, 21 ) }, actual1 );

            Assert.IsTrue( parser.Read(), "Could not read the second record." );
            var actual2 = parser.GetValues();
            CollectionAssert.AreEqual( new object[] { 234, "Jay Smith", new DateTime( 2017, 05, 21 ) }, actual2 );

            Assert.IsFalse( parser.Read(), "There should not be any more records." );
        }

        /// <summary>
        ///     If we specify a record filter, those records should be automatically skipped while reading the document.
        /// </summary>
        [TestMethod]
        public void TestGetValues_WithPartitionedRecordFilter_SkipRecordsMatchingCriteria()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "id" ), new Window( 10 ) { Alignment = FixedAlignment.RightAligned } )
                  .AddColumn( new StringColumn( "name" ), new Window( 25 ) { Alignment = FixedAlignment.RightAligned } )
                  .AddColumn( new DateTimeColumn( "created" ) { InputFormat = "M/d/yyyy" }, new Window( 10 ) { Alignment = FixedAlignment.RightAligned } );

            const string lines = @"       123                Bob Smith 4/21/2017
        -1                Jay Smith 8/14/2017
       234                Jay Smith 5/21/2017";

            var stringReader = new StringReader( lines );
            var parser = new FixedLengthReader( stringReader, schema );
            parser.RecordPartitioned += ( _, e ) =>
            {
                e.IsSkipped = e.Values.Length == 3 && e.Values[0].StartsWith( "-" );
            };

            Assert.IsTrue( parser.Read(), "Could not read the first record." );
            var actual1 = parser.GetValues();
            CollectionAssert.AreEqual( new object[] { 123, "Bob Smith", new DateTime( 2017, 04, 21 ) }, actual1 );

            Assert.IsTrue( parser.Read(), "Could not read the second record." );
            var actual2 = parser.GetValues();
            CollectionAssert.AreEqual( new object[] { 234, "Jay Smith", new DateTime( 2017, 05, 21 ) }, actual2 );

            Assert.IsFalse( parser.Read(), "There should not be any more records." );
        }

        /// <summary>
        ///     We should be able to write and read values using a type mappers.
        /// </summary>
        [TestMethod]
        public void TestTypeMapper_Roundtrip()
        {
            var mapper = FixedLengthTypeMapper.Define<Person>();
            mapper.Property( p => p.Id, new Window( 25 ) ).ColumnName( "id" );
            mapper.Property( p => p.Name, new Window( 100 ) ).ColumnName( "name" );
            mapper.Property( p => p.Created, new Window( 8 ) ).ColumnName( "created" ).InputFormat( "yyyyMMdd" ).OutputFormat( "yyyyMMdd" );

            var bob = new Person { Id = 123, Name = "Bob", Created = new DateTime( 2013, 1, 19 ) };
            var options = new FixedLengthOptions { FillCharacter = '@' };

            var stringWriter = new StringWriter();
            mapper.Write( stringWriter, [ bob ], options );

            var stringReader = new StringReader( stringWriter.ToString() );
            Person[] people = [.. mapper.Read( stringReader, options )];
            Assert.AreEqual( 1, people.Length );
            var person = people.SingleOrDefault();
            Assert.AreEqual( bob.Id, person!.Id );
            Assert.AreEqual( bob.Name, person.Name );
            Assert.AreEqual( bob.Created, person.Created );
        }

        /// <summary>
        ///     We should be able to write and read values using a type mappers.
        /// </summary>
        [TestMethod]
        public void TestTypeMapper_RoundTrip_SkipHeaderRow()
        {
            var mapper = FixedLengthTypeMapper.Define<Person>();
            mapper.Property( p => p.Id, new Window( 25 ) ).ColumnName( "id" );
            mapper.Property( p => p.Name, new Window( 100 ) ).ColumnName( "name" );
            mapper.Property( p => p.Created, new Window( 8 ) ).ColumnName( "created" ).InputFormat( "yyyyMMdd" ).OutputFormat( "yyyyMMdd" );

            var bob = new Person { Id = 123, Name = "Bob", Created = new DateTime( 2013, 1, 19 ) };
            var options = new FixedLengthOptions { IsFirstRecordHeader = true, FillCharacter = '@' };

            var stringWriter = new StringWriter();
            mapper.Write( stringWriter, [ bob ], options );

            var stringReader = new StringReader( stringWriter.ToString() );
            Person[] people = [.. mapper.Read( stringReader, options )];
            Assert.AreEqual( 1, people.Length );
            var person = people.SingleOrDefault();
            Assert.AreEqual( bob.Id, person!.Id );
            Assert.AreEqual( bob.Name, person.Name );
            Assert.AreEqual( bob.Created, person.Created );
        }

        /// <summary>
        ///     We should be able to write and read values using a type mapper with a null value.
        /// </summary>
        [TestMethod]
        public void TestTypeMapper_RoundtripWithNull()
        {
            var mapper = FixedLengthTypeMapper.Define<Person>();
            mapper.Property( p => p.Id, new Window( 25 ) ).ColumnName( "id" );
            mapper.Property( p => p.Name, new Window( 100 ) ).ColumnName( "name" );
            mapper.Property( p => p.Created, new Window( 8 ) ).ColumnName( "created" ).InputFormat( "yyyyMMdd" ).OutputFormat( "yyyyMMdd" );

            var bob = new Person { Id = 123, Name = null!, Created = new DateTime( 2013, 1, 19 ) };
            var options = new FixedLengthOptions { FillCharacter = '@' };

            var stringWriter = new StringWriter();
            mapper.Write( stringWriter, [ bob ], options );

            var stringReader = new StringReader( stringWriter.ToString() );
            Person[] people = [.. mapper.Read( stringReader, options )];
            Assert.AreEqual( 1, people.Length );
            var person = people.SingleOrDefault();
            Assert.IsNotNull( person );
            Assert.AreEqual( bob.Id, person.Id );
            Assert.AreEqual( bob.Name, person.Name );
            Assert.AreEqual( bob.Created, person.Created );
        }

        /// <summary>
        ///     We should be able to round-trip a schema that has separators in a fixed-length schema.
        /// </summary>
        [TestMethod]
        public void TestTypeMapper_IgnoredSeparators_RoundTrip()
        {
            var mapper = FixedLengthTypeMapper.Define<Person>();
            mapper.Property( p => p.Id, new Window( 25 ) ).ColumnName( "id" );
            mapper.Ignored( new Window( 1 ) { FillCharacter = '|' } );
            mapper.Property( p => p.Name, new Window( 100 ) ).ColumnName( "name" );
            mapper.Ignored( new Window( 1 ) { FillCharacter = '|' } );
            mapper.Property( p => p.Created, new Window( 8 ) ).ColumnName( "created" ).InputFormat( "yyyyMMdd" ).OutputFormat( "yyyyMMdd" );

            var bob = new Person { Id = 123, Name = "Bob Smith", Created = new DateTime( 2013, 1, 19 ) };
            var options = new FixedLengthOptions { FillCharacter = ' ' };

            var stringWriter = new StringWriter();
            mapper.Write( stringWriter, [ bob ], options );

            var stringReader = new StringReader( stringWriter.ToString() );
            Person[] people = [.. mapper.Read( stringReader, options )];
            Assert.AreEqual( 1, people.Length );
            var person = people.SingleOrDefault();
            Assert.IsNotNull( person );
            Assert.AreEqual( bob.Id, person.Id );
            Assert.AreEqual( bob.Name, person.Name );
            Assert.AreEqual( bob.Created, person.Created );
        }

        /// <summary>
        ///     If we specify a record filter, those records should be automatically skipped while reading the document.
        /// </summary>
        [TestMethod]
        public void TestGetValues_WithRecordFilter_SkipAllRecords()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "id" ), new Window( 10 ) { Alignment = FixedAlignment.RightAligned } )
                  .AddColumn( new StringColumn( "name" ), new Window( 25 ) { Alignment = FixedAlignment.RightAligned } )
                  .AddColumn( new DateTimeColumn( "created" ) { InputFormat = "M/d/yyyy" }, new Window( 10 ) { Alignment = FixedAlignment.RightAligned } );

            const string lines = @"       123                Bob Smith 4/21/2017
        -1                Jay Smith 8/14/2017
       234                Jay Smith 5/21/2017";

            var stringReader = new StringReader( lines );
            var parser = new FixedLengthReader( stringReader, schema );
            parser.RecordRead += ( _, e ) =>
            {
                e.IsSkipped = true;
            };

            Assert.IsFalse( parser.Read(), "All records should have been skipped." );
        }

        /// <summary>
        ///     If we specify a record filter, those records should be automatically skipped while reading the document.
        /// </summary>
        [TestMethod]
        public void TestGetValues_WithPartitionedRecordFilter_SkipAllRecords()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "id" ), new Window( 10 ) { Alignment = FixedAlignment.RightAligned } )
                  .AddColumn( new StringColumn( "name" ), new Window( 25 ) { Alignment = FixedAlignment.RightAligned } )
                  .AddColumn( new DateTimeColumn( "created" ) { InputFormat = "M/d/yyyy" }, new Window( 10 ) { Alignment = FixedAlignment.RightAligned } );

            const string lines = @"       123                Bob Smith 4/21/2017
        -1                Jay Smith 8/14/2017
       234                Jay Smith 5/21/2017";

            var stringReader = new StringReader( lines );
            var parser = new FixedLengthReader( stringReader, schema );
            parser.RecordPartitioned += ( _, e ) =>
            {
                e.IsSkipped = true;
            };

            Assert.IsFalse( parser.Read(), "All records should have been skipped." );
        }

        internal class Person
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;

            public DateTime Created { get; set; }

            public bool? IsActive { get; set; }
        }

        [TestMethod]
        public void TestTypeMapper_NullableBoolean_RoundTripsNull()
        {
            var mapper = FixedLengthTypeMapper.Define<Person>();
            mapper.Property( x => x.IsActive, 10 ).ColumnName( "is_active" );

            var person = new Person { IsActive = null };

            var stringWriter = new StringWriter();
            mapper.Write( stringWriter, [ person ] );

            var stringReader = new StringReader( stringWriter.ToString() );
            Person[] people = [.. mapper.Read( stringReader )];
            Assert.AreEqual( 1, people.Length );
            var first = people.SingleOrDefault();
            Assert.IsNotNull( first );
            Assert.IsNull( first.IsActive );
        }

        [TestMethod]
        public void TestTypeMapper_NullableBoolean_RoundTripsFalse()
        {
            var mapper = FixedLengthTypeMapper.Define<Person>();
            mapper.Property( x => x.IsActive, 10 ).ColumnName( "is_active" );

            var person = new Person { IsActive = false };

            var stringWriter = new StringWriter();
            mapper.Write( stringWriter, [ person ] );

            var stringReader = new StringReader( stringWriter.ToString() );
            Person[] people = [.. mapper.Read( stringReader )];
            Assert.AreEqual( 1, people.Length );
            var first = people.SingleOrDefault();
            Assert.IsNotNull( first );
            Assert.AreNotEqual( true, first.IsActive );
        }

        [TestMethod]
        public void TestTypeMapper_NullableBoolean_RoundTripsTrue()
        {
            var mapper = FixedLengthTypeMapper.Define<Person>();
            mapper.Property( x => x.IsActive, 10 ).ColumnName( "is_active" );

            var person = new Person { IsActive = true };

            var stringWriter = new StringWriter();
            mapper.Write( stringWriter, [ person ] );

            var stringReader = new StringReader( stringWriter.ToString() );
            Person[] people = [.. mapper.Read( stringReader )];
            Assert.AreEqual( 1, people.Length );
            var first = people.SingleOrDefault();
            Assert.IsNotNull( first );
            Assert.IsTrue( first.IsActive );
        }

        [TestMethod]
        public void TestReader_BadDecimal_ThrowsException()
        {
            const string data = "bad";
            var schema = new DelimitedSchema();
            schema.AddColumn( new DecimalColumn( "value" ) );
            
            var reader = new DelimitedReader( new StringReader( data ), schema );
            Assert.ThrowsExactly<RecordProcessingException>( () => reader.Read() );
        }

        [TestMethod]
        public void TestTypeMapper_BadRecordColumn_SkipError()
        {
            const string data = @"         12017-06-11     John Smith
         22017-12-32    Tom Stallon
         32017-08-13     Walter Kay";
            var mapper = FixedLengthTypeMapper.Define<Person>();
            mapper.Property( x => x.Id, 10 );
            mapper.Property( x => x.Created, 10 );
            mapper.Property( x => x.Name, 15 );

            var stringReader = new StringReader( data );
            List<int> errorRecords = [];
            var reader = mapper.GetReader( stringReader );
            reader.RecordError += ( _, e ) =>
            {
                errorRecords.Add( e.RecordContext.PhysicalRecordNumber );
                e.IsHandled = true;
            };
            Person[] people = [.. reader.ReadAll()];
            Assert.AreEqual( 2, people.Length );
            Assert.AreEqual( 1, errorRecords.Count );
            Assert.AreEqual( 2, errorRecords[0] );
        }

        [TestMethod]
        public void TestTypeMapper_DefaultRecordSeparator_Intermixed()
        {
            var mapper = FixedLengthTypeMapper.Define<Person>();
            mapper.Property( p => p.Id, new Window( 25 ) ).ColumnName( "id" );
            mapper.Property( p => p.Name, new Window( 100 ) ).ColumnName( "name" );
            mapper.Property( p => p.Created, new Window( 8 ) ).ColumnName( "created" ).InputFormat( "yyyyMMdd" ).OutputFormat( "yyyyMMdd" );

            const string rawData = "123                      Bob                                                                                                 20130119\r\n234                      Sam                                                                                                 20130119\r345                      Ron                                                                                                 20130119\n456                      Carl                                                                                                20130119\r\n";
            var stringReader = new StringReader( rawData );

            var options = new FixedLengthOptions { HasRecordSeparator = true, RecordSeparator = null };
            Person[] people = [.. mapper.Read( stringReader, options )];

            Assert.AreEqual( 4, people.Length );
        }

        [TestMethod]
        public void TestReaderWriter_TrailingText_RoundTripsExtra()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new Int32Column( "id" ), new Window( 10 ) { Alignment = FixedAlignment.RightAligned } )
                  .AddColumn( new StringColumn( "name" ), new Window( 25 ) { Alignment = FixedAlignment.RightAligned } )
                  .AddColumn( new DateTimeColumn( "created" ) { InputFormat = "M/d/yyyy", OutputFormat = "M/d/yyyy" }, new Window( 10 ) { Alignment = FixedAlignment.RightAligned } )
                  .AddColumn( new StringColumn( "extra" ), Window.Trailing );

            const string lines = @"       123                Bob Smith 4/21/2017This
        -1                Jay Smith 8/14/2017is
       234                Jay Smith 5/21/2017extra
";

            var stringReader = new StringReader( lines );
            var parser = new FixedLengthReader( stringReader, schema );

            List<object?[]> records =
            [
                AssertExtra( parser, "This" ),
                AssertExtra( parser, "is" ),
                AssertExtra( parser, "extra" )
            ];
            Assert.IsFalse( parser.Read() );

            var stringWriter = new StringWriter();
            var writer = new FixedLengthWriter( stringWriter, schema );
            foreach (var record in records)
            {
                writer.Write( record );
            }

            var formatted = stringWriter.ToString();
            Assert.AreEqual( lines, formatted, "The records did not round-trip." );
        }

        private static object?[] AssertExtra( FixedLengthReader reader, string expected )
        {
            Assert.IsTrue( reader.Read(), "Could not read the next record." );
            var values = reader.GetValues();
            var schema = reader.GetSchema();
            Assert.AreEqual( schema!.ColumnDefinitions.Count, values.Length, "The wrong number of values were parsed." );
            var value = values[schema.ColumnDefinitions.Count - 1];
            Assert.AreEqual( expected, value, "The wrong extra value was found for the record." );
            return values;
        }

        [TestMethod]
        public void TestTypeMapper_TrailingText_RoundTripsExtra()
        {
            var mapper = new FixedLengthTypeMapper<ExtraPerson>( () => new ExtraPerson() );
            mapper.Property( p => p.Id, new Window( 10 ) { Alignment = FixedAlignment.RightAligned } )
                .ColumnName( "id" );
            mapper.Property( p => p.Name, new Window( 25 ) { Alignment = FixedAlignment.RightAligned } )
                .ColumnName( "name" );
            mapper.Property( p => p.Created, new Window( 10 ) { Alignment = FixedAlignment.RightAligned } )
                .ColumnName( "created" )
                .InputFormat( "M/d/yyyy" )
                .OutputFormat( "M/d/yyyy" );
            mapper.Property( p => p.Extra, Window.Trailing ).ColumnName( "extra" );

            const string lines = @"       123                Bob Smith 4/21/2017This
        -1                Jay Smith 8/14/2017is
       234                Jay Smith 5/21/2017extra
";

            var stringReader = new StringReader( lines );
            List<ExtraPerson> people = [.. mapper.Read( stringReader )];
            Assert.AreEqual( 3, people.Count, "The wrong number of records were read." );
            Assert.AreEqual( "This", people[0].Extra );
            Assert.AreEqual( "is", people[1].Extra );
            Assert.AreEqual( "extra", people[2].Extra );

            var stringWriter = new StringWriter();
            mapper.Write( stringWriter, people );

            var formatted = stringWriter.ToString();
            Assert.AreEqual( lines, formatted, "The records did not round-trip." );
        }

        internal class ExtraPerson : Person
        {
            public string Extra { get; set; } = string.Empty;
        }

        [TestMethod]
        public void TestTypeMapper_IsHandled_ContinuesExecution()
        {
            var mapper = FixedLengthTypeMapper.Define<Profile>();
            mapper.Property( x => x.Id, 3 );
            mapper.Property( x => x.Name, 3 );
            mapper.Property( x => x.DeptNo, 4 );

            string[] lines =
            [
                "123Bob171",
                "124Jor1233",
                "123Bob1714"
            ];
            var stringReader = new StringReader( string.Join( Environment.NewLine, lines ) );
            var reader = mapper.GetReader( stringReader );
            reader.RecordError += ( _, e ) =>
            {
                Assert.AreEqual( 1, e.RecordContext.PhysicalRecordNumber );
                e.IsHandled = true;
            };

            List<Profile> records = [.. reader.ReadAll()];
            Assert.AreEqual( 2, records.Count );
            var first = records[0];
            Assert.AreEqual( 124, first.Id );
            var second = records[1];
            Assert.AreEqual( 123, second.Id );
        }

        internal class Profile
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;

            public int DeptNo { get; set; }
        }
    }
}
