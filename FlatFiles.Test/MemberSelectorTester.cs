using System;
using FlatFiles.TypeMapping;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests how property selectors are resolved into member accessors, in particular around members
    ///     that have no instance to read from.
    /// </summary>
    [TestClass]
    public class MemberSelectorTester
    {
        private sealed class Inner
        {
            public string Value { get; set; }
        }

        private sealed class Holder
        {
            public static Inner Instance { get; } = new();
        }

        private sealed class Entity
        {
            public string Name { get; set; }

            public Inner Child { get; set; }

            public static string Tag { get; } = "tag";
        }

        /// <summary>
        ///     An ordinary property on the entity resolves, which is the baseline the rest of this class
        ///     is measured against.
        /// </summary>
        [TestMethod]
        public void TestSelector_InstanceProperty_IsAccepted()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Entity() );

            mapper.Property( x => x.Name ).ColumnName( "name" );

            Assert.AreEqual( 1, mapper.GetSchema().ColumnDefinitions.Count, "The property was not mapped." );
        }

        /// <summary>
        ///     A static member declared on the entity itself is resolved without ever needing an instance
        ///     to read it from, so the guard against a missing instance must not reject it.
        /// </summary>
        /// <remarks>
        ///     A guard for the nested case was once written at the top of the resolver, where it rejected
        ///     this selector too. Nothing on this path dereferences the parent expression, so nothing here
        ///     should care that there isn't one.
        /// </remarks>
        [TestMethod]
        public void TestSelector_StaticMemberOnEntity_IsAccepted()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Entity() );

            mapper.Property( x => Entity.Tag ).ColumnName( "tag" );

            Assert.AreEqual( 1, mapper.GetSchema().ColumnDefinitions.Count, "The static member was not mapped." );
        }

        /// <summary>
        ///     A nested member reached through a static one has no instance to walk back to, so it is
        ///     rejected with an exception that names the problem rather than a NullReferenceException
        ///     thrown from inside the resolver.
        /// </summary>
        [TestMethod]
        public void TestSelector_NestedMemberBehindStatic_ThrowsArgumentException()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Entity() );

            Assert.ThrowsExactly<ArgumentException>( () => mapper.Property( x => Holder.Instance.Value ) );
        }

        /// <summary>
        ///     A genuinely nested member on the entity still resolves through its parent, so the guard has
        ///     not been placed anywhere that blocks the case it sits next to.
        /// </summary>
        [TestMethod]
        public void TestSelector_NestedMemberOnEntity_IsAccepted()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Entity() );

            mapper.Property( x => x.Child.Value ).ColumnName( "value" );

            Assert.AreEqual( 1, mapper.GetSchema().ColumnDefinitions.Count, "The nested member was not mapped." );
        }
    }
}
