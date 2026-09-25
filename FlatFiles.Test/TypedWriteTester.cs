using System;
using System.IO;
using System.Threading.Tasks;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests a record written straight from the entity it comes from, without its values being boxed into an
    ///     array first. What matters is that it produces exactly what the older path produces, and that every
    ///     mapping the path cannot take falls back to it rather than failing.
    /// </summary>
    [TestClass]
    public class TypedWriteTester
    {
        private const string Expected = "1,Bob,12.34,20260922,True\r\n2,Susan,13.88,20260923,False\r\n";

        [TestMethod]
        public void TestWritingFromTheEntity_MatchesWritingFromValues()
        {
            // The same mapping twice, but one carries a hook declared in terms of string, which is enough to put
            // it back on the values path without changing a character of what it writes.
            var straight = Write( Mapper( false ) );
            var throughValues = Write( Mapper( true ) );

            Assert.AreEqual( Expected, straight );
            Assert.AreEqual( straight, throughValues, "The two paths should not disagree about a single character." );
        }

        [TestMethod]
        public async Task TestWritingAsynchronously_MatchesWritingSynchronously()
        {
            var writer = new StringWriter();
            var typed = Mapper( false ).GetWriter( writer, new DelimitedOptions { RecordSeparator = "\r\n" } );
            foreach (var person in People())
            {
                await typed.WriteAsync( person );
            }

            Assert.AreEqual( Expected, writer.ToString() );
        }

        [TestMethod]
        public void TestAMemberHoldingNothing_IsWrittenAsTheNullFormatterSaysS()
        {
            var mapper = DelimitedTypeMapper.Define<Optional>();
            mapper.Property( x => x.Id );
            mapper.Property( x => x.Amount ).ColumnName( "Amount" );

            var writer = new StringWriter();
            mapper.Write( writer, [new Optional { Id = 1, Amount = null }, new Optional { Id = 2, Amount = 3.5m }],
                new DelimitedOptions { RecordSeparator = "\r\n" } );

            Assert.AreEqual( "1,\r\n2,3.5\r\n", writer.ToString() );
        }

        [TestMethod]
        public void TestACustomWriter_PutsTheMappingBackOnTheValuesPath()
        {
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( x => x.Id );
            mapper.CustomMapping( new StringColumn( "Name" ) ).WithWriter( ( Person entity, object?[] values ) => values[1] = entity.Name!.ToUpperInvariant() );

            var writer = new StringWriter();
            mapper.Write( writer, [new Person { Id = 1, Name = "Bob" }], new DelimitedOptions { RecordSeparator = "\r\n" } );

            Assert.AreEqual( "1,BOB\r\n", writer.ToString() );
        }

        [TestMethod]
        public void TestAnInjectorChoosingSchemas_PutsTheWriteBackOnTheValuesPath()
        {
            var injector = new DelimitedTypeMapperInjector();
            var one = DelimitedTypeMapper.Define<Person>();
            one.Property( x => x.Id );
            var two = DelimitedTypeMapper.Define<Optional>();
            two.Property( x => x.Id );
            two.Property( x => x.Amount );
            injector.When<Person>().Use( one );
            injector.When<Optional>().Use( two );

            var writer = new StringWriter();
            var typed = injector.GetWriter( writer, new DelimitedOptions { RecordSeparator = "\r\n" } );
            typed.Write( new Person { Id = 1 } );
            typed.Write( new Optional { Id = 2, Amount = 9m } );

            Assert.AreEqual( "1\r\n2,9\r\n", writer.ToString() );
        }

        [TestMethod]
        public void TestAColumnThatCannotFormatAValue_ReachesTheErrorHandlerWithIt()
        {
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( x => x.Id );
            // Not a numeric format, so formatting the value throws where the column tries it.
            mapper.Property( x => x.Amount ).OutputFormat( "Z" );

            var writer = new StringWriter();
            var typed = mapper.GetWriter( writer, new DelimitedOptions { RecordSeparator = "\r\n" } );
            object? offending = null;
            typed.ColumnError += ( _, e ) =>
            {
                offending = ( (ColumnProcessingException) e.Exception ).ColumnValue;
                e.IsHandled = true;
                e.Substitution = "recovered";
            };
            typed.Write( new Person { Id = 1, Amount = 12.34m } );

            Assert.AreEqual( "1,recovered\r\n", writer.ToString(), "The handler's substitution belongs in the column's place." );
            Assert.AreEqual( 12.34m, offending, "The handler should be told which value could not be formatted." );
        }

        [TestMethod]
        public void TestAFixedLengthWrite_FromTheEntity_MatchesWritingFromValues()
        {
            var straight = WriteFixed( false );
            var throughValues = WriteFixed( true );

            Assert.AreEqual( "1  Bob       12.34\r\n2  Susan     13.88\r\n", straight );
            Assert.AreEqual( straight, throughValues );
        }

        [TestMethod]
        public void TestAReferenceMemberHoldingNothing_IsWrittenAsNothing()
        {
            var writer = new StringWriter();
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( x => x.Id );
            mapper.Property( x => x.Name );

            mapper.Write( writer, [new Person { Id = 1, Name = null }], new DelimitedOptions { RecordSeparator = "\r\n" } );

            Assert.AreEqual( "1,\r\n", writer.ToString() );
        }

        [TestMethod]
        public void TestWithTheColumnContextDisabled_StillWritesFromTheEntity()
        {
            var writer = new StringWriter();
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( x => x.Id );
            mapper.Property( x => x.Amount );

            mapper.Write( writer, [new Person { Id = 1, Amount = 2.5m }],
                new DelimitedOptions { RecordSeparator = "\r\n", IsColumnContextDisabled = true } );

            Assert.AreEqual( "1,2.5\r\n", writer.ToString() );
        }

        [TestMethod]
        public void TestWithTheColumnContextDisabled_AFailureSaysWhichColumnAndValue()
        {
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( x => x.Id );
            mapper.Property( x => x.Amount ).OutputFormat( "Z" );

            var writer = new StringWriter();
            var column = Assert.ThrowsExactly<ColumnProcessingException>( () => mapper.Write( writer, [new Person { Id = 1, Amount = 12.34m }],
                new DelimitedOptions { RecordSeparator = "\r\n", IsColumnContextDisabled = true } ) );

            Assert.AreEqual( 12.34m, column.ColumnValue, "The failure should name the value it could not format." );
        }

        [TestMethod]
        public async Task TestAFixedLengthWriteAsynchronously_WritesFromTheEntity()
        {
            var mapper = FixedLengthTypeMapper.Define<Person>();
            mapper.Property( x => x.Id, 3 );
            mapper.Property( x => x.Name, 10 );

            var writer = new StringWriter();
            var typed = mapper.GetWriter( writer, new FixedLengthOptions { RecordSeparator = "\r\n" } );
            await typed.WriteAsync( new Person { Id = 1, Name = "Bob" } );

            Assert.AreEqual( "1  Bob       \r\n", writer.ToString() );
        }

        [TestMethod]
        public void TestAMemberWithoutAPublicGetter_PutsTheMappingBackOnTheValuesPath()
        {
            var mapper = DelimitedTypeMapper.Define<Guarded>();
            mapper.Property( x => x.Id );
            mapper.Property( x => x.Hidden );

            var writer = new StringWriter();
            mapper.Write( writer, [new Guarded { Id = 1 }], new DelimitedOptions { RecordSeparator = "\r\n" } );

            Assert.AreEqual( "1,kept\r\n", writer.ToString() );
        }

        [TestMethod]
        public void TestAMetadataColumn_PutsTheMappingBackOnTheValuesPath()
        {
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( x => x.Id );
            mapper.CustomMapping( new RecordNumberColumn( "Number" ) ).WithWriter( ( Person _, object?[] _ ) => { } );
            mapper.Property( x => x.Name );

            var writer = new StringWriter();
            mapper.Write( writer, [new Person { Id = 1, Name = "Bob" }, new Person { Id = 2, Name = "Ann" }],
                new DelimitedOptions { RecordSeparator = "\r\n" } );

            Assert.AreEqual( "1,1,Bob\r\n2,2,Ann\r\n", writer.ToString() );
        }

        public sealed class Guarded
        {
            public int Id { get; set; }

            public string Hidden { internal get; set; } = "kept";

            public string Show()
            {
                return Hidden;
            }
        }

        [TestMethod]
        public void TestAnEntityThatIsAValueType_PutsTheMappingBackOnTheValuesPath()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Boxed() );
            mapper.Property( x => x.Id );
            mapper.Property( x => x.Name );

            var writer = new StringWriter();
            mapper.Write( writer, [new Boxed { Id = 1, Name = "Bob" }], new DelimitedOptions { RecordSeparator = "\r\n" } );

            Assert.AreEqual( "1,Bob\r\n", writer.ToString() );
        }

        [TestMethod]
        public void TestANestedEntity_PutsTheMappingBackOnTheValuesPath()
        {
            var mapper = DelimitedTypeMapper.Define<Outer>();
            mapper.Property( x => x.Id );
            var inner = DelimitedTypeMapper.Define<Person>();
            inner.Property( x => x.Id );
            inner.Property( x => x.Name );
            mapper.ComplexProperty( x => x.Inner!, inner );

            var writer = new StringWriter();
            mapper.Write( writer, [new Outer { Id = 1, Inner = new Person { Id = 2, Name = "Bob" } }],
                new DelimitedOptions { RecordSeparator = "\r\n" } );

            StringAssert.StartsWith( writer.ToString(), "1," );
        }

        public struct Boxed
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        public sealed class Outer
        {
            public int Id { get; set; }

            public Person? Inner { get; set; }
        }

        [TestMethod]
        public void TestAnOnFormattingHook_AlsoPutsTheMappingBackOnTheValuesPath()
        {
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( x => x.Id );
            // Declared in terms of object, so the column can no longer be formatted from its own type.
            mapper.Property( x => x.Amount ).OnFormatting( ( _, value ) => value );

            var writer = new StringWriter();
            mapper.Write( writer, [new Person { Id = 1, Amount = 2.5m }], new DelimitedOptions { RecordSeparator = "\r\n" } );

            Assert.AreEqual( "1,2.5\r\n", writer.ToString() );
        }

        private static string Write( IDelimitedTypeMapper<Person> mapper )
        {
            var writer = new StringWriter();
            mapper.Write( writer, People(), new DelimitedOptions { RecordSeparator = "\r\n" } );
            return writer.ToString();
        }

        private static string WriteFixed( bool throughValues )
        {
            var mapper = FixedLengthTypeMapper.Define<Person>();
            mapper.Property( x => x.Id, 3 );
            mapper.Property( x => x.Name, 10 );
            var amount = mapper.Property( x => x.Amount, 5 );
            if (throughValues)
            {
                amount.OnFormatted( ( _, value ) => value );
            }

            var writer = new StringWriter();
            mapper.Write( writer, People(), new FixedLengthOptions { RecordSeparator = "\r\n" } );
            return writer.ToString();
        }

        private static IDelimitedTypeMapper<Person> Mapper( bool throughValues )
        {
            var mapper = DelimitedTypeMapper.Define<Person>();
            mapper.Property( x => x.Id );
            var name = mapper.Property( x => x.Name );
            mapper.Property( x => x.Amount );
            mapper.Property( x => x.When ).OutputFormat( "yyyyMMdd" );
            mapper.Property( x => x.Active );
            if (throughValues)
            {
                // Declared in terms of string, so the column can no longer be formatted from its own type.
                name.OnFormatted( ( _, value ) => value );
            }
            return mapper;
        }

        private static Person[] People()
        {
            return
            [
                new Person { Id = 1, Name = "Bob", Amount = 12.34m, When = new DateTime( 2026, 9, 22 ), Active = true },
                new Person { Id = 2, Name = "Susan", Amount = 13.88m, When = new DateTime( 2026, 9, 23 ), Active = false }
            ];
        }

        public sealed class Person
        {
            public int Id { get; set; }

            public string? Name { get; set; }

            public decimal Amount { get; set; }

            public DateTime When { get; set; }

            public bool Active { get; set; }
        }

        public sealed class Optional
        {
            public int Id { get; set; }

            public decimal? Amount { get; set; }
        }
    }
}
