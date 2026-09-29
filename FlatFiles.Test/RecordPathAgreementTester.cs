using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Reads one file every way a reader can read it, and insists the records come out the same.
    /// </summary>
    /// <remarks>
    ///     A schema walks its columns in three places: over values copied out as strings, over the record where the
    ///     values lie, and straight onto an entity. Which one a record takes is not a choice anyone makes about
    ///     parsing - it follows from whether a handler is attached to the record being read, whether a selector
    ///     needs the values to choose a schema, and whether a type mapper is in use. Somebody adding a handler that
    ///     only counts records moves every value in the file onto a different loop.
    ///     <para>
    ///         The three had tests each. Nothing read the same file down more than one of them and compared, which
    ///         is the gap that let the column's own parse paths drift far enough to ship two defects.
    ///     </para>
    /// </remarks>
    [TestClass]
    public class RecordPathAgreementTester
    {
        /// <summary>
        ///     Deliberately awkward: a value only the hook can read, one it answers by writing nothing, values that
        ///     need trimming, an empty value and a blank one, and a short record for the padding to finish.
        /// </summary>
        private const string Records = """
                                       #1,  Alice  ,skip,7
                                       #2,Bob,skip,
                                       #3,   ,skip,  9
                                       OPEN,Dot,skip,-1
                                       #5,Eve,skip
                                       """;

        [TestMethod]
        public void TestEveryReadPathAgrees()
        {
            // Without setters the mapper reads through the array of objects like everything else, and two of the
            // four paths below are quietly the same path. The first draft of this test compared four things that
            // were three, and said nothing about it.
            Assert.IsNotNull( Setters( Mapper() ),
                "this mapper has no setters, so nothing here reads a record straight onto an entity" );

            Compare( withRecordNumber: false,
            [
                "values, read where they lie",
                "values, copied out first",
                "entities, read where they lie",
                "entities, copied out first"
            ] );
        }

        /// <summary>
        ///     The same, for a schema carrying a column whose value comes from the context rather than the record.
        ///     Such a column is reached by a branch of its own in each loop, which is where an off-by-one in one of
        ///     them would live.
        /// </summary>
        /// <remarks>
        ///     Only the two untyped loops are compared here. A mapping with a custom reader - the only way to put a
        ///     metadata column on a member - has no setters built for it, so the reader cannot take the entity
        ///     straight from the record whatever else is true of it.
        /// </remarks>
        [TestMethod]
        public void TestReadPathsAgreeWithAMetadataColumn()
        {
            // If this ever starts building setters, the reader can take this schema onto an entity too, and the
            // comparison below should be widened to the four paths the other test uses.
            Assert.IsNull( Setters( MetadataMapper() ),
                "this mapper now has setters, so the entity paths belong in this comparison as well" );

            Compare( withRecordNumber: true, ["values, read where they lie", "values, copied out first"] );
        }

        private static IColumnSetter<Person>[]? Setters( IDelimitedTypeMapper<Person> mapper )
        {
            return ( (IMapperSource<Person>) mapper ).GetMapper().GetColumnSetters();
        }

        /// <summary>
        ///     The same question on the writing side, where a schema walks its columns twice: over an array of
        ///     values, and over the entity they would have been taken from.
        /// </summary>
        /// <remarks>
        ///     Which one a record takes turns on whether an injector picks the schema per record, so here too a
        ///     caller changes the loop every value goes down by configuring something else entirely.
        /// </remarks>
        [TestMethod]
        public void TestEveryWritePathAgrees()
        {
            var mapper = Mapper();
            Assert.IsNotNull( ( (IMapperSource<Person>) mapper ).GetMapper().GetColumnGetters(),
                "this mapper has no getters, so nothing here writes a record straight from an entity" );

            var fromEntities = new StringWriter();
            var entityWriter = mapper.GetWriter( fromEntities, Options );
            foreach (var person in People)
            {
                entityWriter.Write( person );
            }

            var fromValues = new StringWriter();
            var valueWriter = new DelimitedWriter( fromValues, mapper.GetSchema(), Options );
            foreach (var person in People)
            {
                valueWriter.Write( [person.Id, person.Name, person.Score] );
            }

            Assert.AreEqual( fromValues.ToString(), fromEntities.ToString() );
        }

        private static Person[] People =>
        [
            new() { Id = 1, Name = "Alice", Score = 7 },
            new() { Id = 2, Name = null, Score = null },
            new() { Id = 0, Name = "", Score = -1 }
        ];

        private static void Compare( bool withRecordNumber, string[] paths )
        {
            var answers = paths.ToDictionary( path => path, path => Read( path, withRecordNumber ) );
            var expected = answers[paths[0]];
            var disagreements = answers
                .Where( answer => answer.Value != expected )
                .Select( answer => $"{answer.Key} gave{Environment.NewLine}{answer.Value}" )
                .ToList();

            Assert.IsEmpty( disagreements,
                $"{paths[0]} gave{Environment.NewLine}{expected}{Environment.NewLine}"
                + string.Join( Environment.NewLine, disagreements ) );
        }

        private static string Read( string path, bool withRecordNumber )
        {
            var copyValuesOut = path.EndsWith( "copied out first", StringComparison.Ordinal );
            try
            {
                var records = path.StartsWith( "values", StringComparison.Ordinal )
                    ? ReadValues( copyValuesOut, withRecordNumber )
                    : ReadEntities( copyValuesOut );
                return string.Join( Environment.NewLine, records );
            }
            catch (Exception exception)
            {
                // Reported rather than thrown, so that a path which starts answering something of the wrong shape
                // reads as a disagreement beside what the others said, not as a stack trace with no comparison.
                return exception.GetType().Name + ": " + exception.Message;
            }
        }

        /// <summary>
        ///     Reads through the schema alone, which yields an array of objects.
        /// </summary>
        /// <param name="copyValuesOut">
        ///     Whether to attach a handler for the record being read, which is what makes the reader copy every
        ///     value out as a string before the schema sees it. The handler itself does nothing.
        /// </param>
        /// <param name="withRecordNumber">Whether the schema carries a column fed by the context.</param>
        private static List<string> ReadValues( bool copyValuesOut, bool withRecordNumber )
        {
            var schema = withRecordNumber ? MetadataMapper().GetSchema() : Mapper().GetSchema();
            var reader = new DelimitedReader( new StringReader( Records ), schema, Options );
            if (copyValuesOut)
            {
                reader.RecordRead += ( _, _ ) => { };
            }
            var records = new List<string>();
            while (reader.Read())
            {
                var values = reader.GetValues();
                records.Add( withRecordNumber
                    ? Describe( values[0], values[1], values[2], values[3] )
                    : Describe( null, values[0], values[1], values[2] ) );
            }
            return records;
        }

        /// <summary>
        ///     Reads through a type mapper, which goes straight onto the entity unless something else wants the
        ///     values, in which case they are parsed as objects and assigned from those.
        /// </summary>
        /// <param name="copyValuesOut">As above.</param>
        private static List<string> ReadEntities( bool copyValuesOut )
        {
            var reader = Mapper().GetReader( new StringReader( Records ), Options );
            if (copyValuesOut)
            {
                reader.RecordRead += ( _, _ ) => { };
            }
            var records = new List<string>();
            while (reader.Read())
            {
                var person = reader.Current!;
                records.Add( Describe( null, person.Id, person.Name, person.Score ) );
            }
            return records;
        }

        private static string Describe( object? recordNumber, object? id, object? name, object? score )
        {
            return string.Join( "|", new[] { recordNumber, id, name, score }
                .Select( value => value is null
                    ? "null"
                    : Convert.ToString( value, CultureInfo.InvariantCulture ) ) );
        }

        private static DelimitedOptions Options => new()
        {
            // Padding is what makes the short record reach the schema at all, and it is filled in before either
            // loop is chosen, so both have to make the same thing of the value that was never there.
            ShortRecordHandling = ShortRecordHandling.Pad
        };

        private static IDelimitedTypeMapper<Person> Mapper()
        {
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( person => person.Id )
                .ColumnName( "id" )
                .OnParsingSpan( StripHashOrBlankOpen )
                .DefaultValue( DefaultValue.Use( 0 ) );
            mapper.Property( person => person.Name ).ColumnName( "name" );
            mapper.Ignored();
            mapper.Property( person => person.Score ).ColumnName( "score" );
            return mapper;
        }

        private static IDelimitedTypeMapper<Person> MetadataMapper()
        {
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.CustomMapping( new RecordNumberColumn( "rn" ) )
                .WithReader( ( person, value ) => person.RecordNumber = (int) value! )
                .WithWriter( person => person.RecordNumber );
            mapper.Property( person => person.Id )
                .ColumnName( "id" )
                .OnParsingSpan( StripHashOrBlankOpen )
                .DefaultValue( DefaultValue.Use( 0 ) );
            mapper.Property( person => person.Name ).ColumnName( "name" );
            mapper.Ignored();
            mapper.Property( person => person.Score ).ColumnName( "score" );
            return mapper;
        }

        /// <summary>Reads a value no ordinary column could, and answers one of them by writing nothing.</summary>
        private static int StripHashOrBlankOpen( IColumnContext? context, ReadOnlySpan<char> value,
            Span<char> destination )
        {
            var trimmed = value.Trim();
            if (trimmed.SequenceEqual( "OPEN" ))
            {
                return 0;
            }
            if (trimmed.StartsWith( "#" ))
            {
                trimmed[1..].CopyTo( destination );
                return trimmed.Length - 1;
            }
            return SpanParsingHooks.Unchanged;
        }

        private sealed class Person
        {
            public int RecordNumber { get; set; }

            public int Id { get; set; }

            public string? Name { get; set; }

            public int? Score { get; set; }
        }
    }
}
