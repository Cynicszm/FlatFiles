using System;
using System.IO;
using System.Linq;
using FlatFiles.CodeGeneration;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests the accessors a caller registers for an entity, which is how a mapping reads onto it without
    ///     boxing its values where the runtime cannot generate code.
    ///     <para>
    ///         The registry is process-wide and nothing removes a registration, so every test here maps onto an
    ///         entity of its own. Two tests sharing one would be two tests sharing a fixture that outlives them.
    ///     </para>
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class MappingAccessorsTester
    {
        [TestMethod]
        public void TestRegisteredSetter_IsUsed()
        {
            var used = 0;
            MappingAccessors.AddSetter<Counted, int>( nameof( Counted.Id ), ( e, v ) =>
            {
                ++used;
                e.Id = v;
            } );

            var read = Read<Counted>( m => m.Property( x => x.Id ), "7\r\n" );

            Assert.AreEqual( 7, read.Id );
            Assert.AreEqual( 1, used, "The mapping built its own setter instead of using the one registered." );
        }

        [TestMethod]
        public void TestRegisteredSetter_WithoutDynamicCode_IsStillUsed()
        {
            var used = 0;
            MappingAccessors.AddSetter<Fallback, int>( nameof( Fallback.Id ), ( e, v ) =>
            {
                ++used;
                e.Id = v;
            } );

            WithoutDynamicCode( () =>
            {
                var read = Read<Fallback>( m => m.Property( x => x.Id ), "9\r\n" );

                Assert.AreEqual( 9, read.Id );
                Assert.AreEqual( 1, used, "Without dynamic code the registration is the only way onto this path." );
            } );
        }

        [TestMethod]
        public void TestNothingRegistered_WithoutDynamicCode_StillReads()
        {
            WithoutDynamicCode( () =>
            {
                var read = Read<Plain>( m =>
                {
                    m.Property( x => x.Id );
                    m.Property( x => x.Name );
                }, "3,Bob\r\n" );

                Assert.AreEqual( 3, read.Id );
                Assert.AreEqual( "Bob", read.Name );
            } );
        }

        [TestMethod]
        public void TestRegistrationForAnotherColumnType_IsIgnored()
        {
            var used = 0;
            // Registered as a whole number, but the mapping gives the member a column of text.
            MappingAccessors.AddSetter<Mismatched, int>( nameof( Mismatched.Value ), ( e, v ) => ++used );

            var mapper = DelimitedTypeMapper.Define<Mismatched>();
            mapper.CustomMapping( new StringColumn( nameof( Mismatched.Value ) ) )
                .WithReader( ( e, v ) => e.Value = (string?) v );
            var read = mapper.Read( new StringReader( "text\r\n" ) ).Single();

            Assert.AreEqual( "text", read.Value );
            Assert.AreEqual( 0, used, "A registration written for another column type was used anyway." );
        }

        [TestMethod]
        public void TestNullableSetter_TakesAnEmptyField()
        {
            MappingAccessors.AddNullableSetter<Optional, int>( nameof( Optional.Id ), ( e, v ) => e.Id = v );

            var read = Read<Optional>( m => m.Property( x => x.Id ), "\r\n" );

            Assert.IsNull( read.Id, "An empty field should have left the member holding nothing." );
        }

        [TestMethod]
        public void TestHiddenMember_IsRegisteredAgainstWhatDeclaresIt()
        {
            var baseUsed = 0;
            var derivedUsed = 0;
            MappingAccessors.AddSetter<Base, int>( nameof( Base.Id ), ( e, v ) => ++baseUsed );
            MappingAccessors.AddSetter<Derived, int>( nameof( Derived.Id ), ( e, v ) =>
            {
                ++derivedUsed;
                e.Id = v;
            } );

            var read = Read<Derived>( m => m.Property( x => x.Id ), "5\r\n" );

            Assert.AreEqual( 5, read.Id );
            Assert.AreEqual( 1, derivedUsed, "The member the mapping named was the one the derived type declares." );
            Assert.AreEqual( 0, baseUsed, "The hidden member of the same name on the base was used instead." );
        }

        [TestMethod]
        public void TestRegisteredFactory_MakesTheEntity()
        {
            var made = 0;
            MappingAccessors.AddFactory( () =>
            {
                ++made;
                return new Built();
            } );
            MappingAccessors.AddSetter<Built, int>( nameof( Built.Id ), ( e, v ) => e.Id = v );

            var read = Read<Built>( m => m.Property( x => x.Id ), "1\r\n2\r\n" , 2 );

            Assert.AreEqual( 2, read.Id );
            Assert.AreEqual( 2, made, "Each record should have been read onto an entity from the registered factory." );
        }

        [TestMethod]
        public void TestAddSetter_RefusesNothing()
        {
            Assert.ThrowsExactly<ArgumentNullException>( () => MappingAccessors.AddSetter<Plain, int>( null!, ( e, v ) => { } ) );
            Assert.ThrowsExactly<ArgumentNullException>( () => MappingAccessors.AddSetter<Plain, int>( "Id", null! ) );
            Assert.ThrowsExactly<ArgumentNullException>( () => MappingAccessors.AddNullableSetter<Optional, int>( "Id", null! ) );
            Assert.ThrowsExactly<ArgumentNullException>( () => MappingAccessors.AddFactory<Plain>( null! ) );
        }

        [TestMethod]
        public void TestRegisteredGetter_IsUsed()
        {
            var used = 0;
            MappingAccessors.AddGetter<Written, int>( nameof( Written.Id ), e =>
            {
                ++used;
                return e.Id;
            } );

            var written = Write<Written>( m => m.Property( x => x.Id ), new Written { Id = 4 } );

            Assert.AreEqual( "4\r\n", written );
            Assert.AreEqual( 1, used, "The mapping built its own getter instead of using the one registered." );
        }

        [TestMethod]
        public void TestRegisteredGetter_WithoutDynamicCode_IsStillUsed()
        {
            var used = 0;
            MappingAccessors.AddGetter<WrittenWithout, int>( nameof( WrittenWithout.Id ), e =>
            {
                ++used;
                return e.Id;
            } );

            WithoutDynamicCode( () =>
            {
                var written = Write<WrittenWithout>( m => m.Property( x => x.Id ), new WrittenWithout { Id = 6 } );

                Assert.AreEqual( "6\r\n", written );
                Assert.AreEqual( 1, used, "Without dynamic code the registration is the only way onto this path." );
            } );
        }

        [TestMethod]
        public void TestRegisteredNullableGetter_WritesWhatTheMemberHolds()
        {
            MappingAccessors.AddNullableGetter<WrittenOptional, int>( nameof( WrittenOptional.Id ), e => e.Id );

            var held = Write<WrittenOptional>( m => m.Property( x => x.Id ), new WrittenOptional { Id = 8 } );
            var nothing = Write<WrittenOptional>( m => m.Property( x => x.Id ), new WrittenOptional { Id = null } );

            Assert.AreEqual( "8\r\n", held );
            Assert.AreEqual( "\r\n", nothing, "A member holding nothing should write what the null formatter says." );
        }

        [TestMethod]
        public void TestGetterRegisteredForAnotherColumnType_IsIgnored()
        {
            var used = 0;
            // Registered as a whole number, but the mapping gives the member a column of text.
            MappingAccessors.AddGetter<WrittenMismatch, int>( nameof( WrittenMismatch.Value ), e => ++used );

            var mapper = DelimitedTypeMapper.Define<WrittenMismatch>();
            mapper.Property( x => x.Value );
            var writer = new StringWriter();
            mapper.Write( writer, [new WrittenMismatch { Value = "text" }], new DelimitedOptions { RecordSeparator = "\r\n" } );

            Assert.AreEqual( "text\r\n", writer.ToString() );
            Assert.AreEqual( 0, used, "A registration written for another column type was used anyway." );
        }

        [TestMethod]
        public void TestAddGetter_RefusesNothing()
        {
            Assert.ThrowsExactly<ArgumentNullException>( () => MappingAccessors.AddGetter<Written, int>( null!, e => e.Id ) );
            Assert.ThrowsExactly<ArgumentNullException>( () => MappingAccessors.AddGetter<Written, int>( "Id", null! ) );
            Assert.ThrowsExactly<ArgumentNullException>( () => MappingAccessors.AddNullableGetter<WrittenOptional, int>( "Id", null! ) );
        }

        [TestMethod]
        public void TestAGetterRegisteredForTheNonNullableForm_IsIgnoredForANullableMember()
        {
            var used = 0;
            // The member holds a nullable whole number; this is registered for the plain one.
            MappingAccessors.AddGetter<WrittenShape, int>( nameof( WrittenShape.Id ), e =>
            {
                ++used;
                return 0;
            } );

            var written = Write<WrittenShape>( m => m.Property( x => x.Id ), new WrittenShape { Id = 5 } );

            Assert.AreEqual( "5\r\n", written );
            Assert.AreEqual( 0, used, "A registration of the wrong shape was used anyway." );
        }

        public sealed class WrittenShape
        {
            public int? Id { get; set; }
        }

        private static string Write<TEntity>( Action<IDelimitedTypeMapper<TEntity>> configure, TEntity entity )
            where TEntity : new()
        {
            var mapper = DelimitedTypeMapper.Define<TEntity>();
            configure( mapper );
            var writer = new StringWriter();
            mapper.Write( writer, [entity], new DelimitedOptions { RecordSeparator = "\r\n" } );
            return writer.ToString();
        }

        public sealed class Written
        {
            public int Id { get; set; }
        }

        public sealed class WrittenWithout
        {
            public int Id { get; set; }
        }

        public sealed class WrittenOptional
        {
            public int? Id { get; set; }
        }

        public sealed class WrittenMismatch
        {
            public string Value { get; set; } = string.Empty;
        }

        private static TEntity Read<TEntity>( Action<IDelimitedTypeMapper<TEntity>> configure, string text, int expected = 1 )
            where TEntity : new()
        {
            var mapper = DelimitedTypeMapper.Define<TEntity>();
            configure( mapper );
            var read = mapper.Read( new StringReader( text ) ).ToList();
            Assert.HasCount( expected, read );
            return read[^1];
        }

        private static void WithoutDynamicCode( Action test )
        {
            var supported = DynamicCode.IsSupported;
            DynamicCode.IsSupported = false;
            try
            {
                test();
            }
            finally
            {
                DynamicCode.IsSupported = supported;
            }
        }

        public sealed class Counted
        {
            public int Id { get; set; }
        }

        public sealed class Fallback
        {
            public int Id { get; set; }
        }

        public sealed class Plain
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;
        }

        public sealed class Mismatched
        {
            public string? Value { get; set; }
        }

        public sealed class Optional
        {
            public int? Id { get; set; }
        }

        public class Base
        {
            public int Id { get; set; }
        }

        public sealed class Derived : Base
        {
            public new int Id { get; set; }
        }

        public sealed class Built
        {
            public int Id { get; set; }
        }
    }
}
